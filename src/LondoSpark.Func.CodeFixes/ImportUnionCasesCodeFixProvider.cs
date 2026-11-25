using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using LondoSpark.Func.Analyzers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;

using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace LondoSpark.Func.CodeFixes;

/// <summary>Brings a union's nested case types into scope, so switch arms and constructions name the case alone.</summary>
/// <remarks>
/// <list type="bullet">
/// <item>A case name typed without its import (<c>Success(var c)</c>, compiler error CS0246 or CS0103) is resolved
/// from the switch input, <c>is</c> operand, return type, or declared variable type. The fix offers
/// <c>using static LondoSpark.Func.Result&lt;Customer, CustomerError&gt;;</c>, or qualifies the name in place when an
/// import would be ambiguous or the union is over type parameters.</item>
/// <item>A case already spelled through its union (LSF0002) is shortened throughout the file once the import is added.</item>
/// </list>
/// </remarks>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(ImportUnionCasesCodeFixProvider))]
[Shared]
public sealed class ImportUnionCasesCodeFixProvider : CodeFixProvider
{
    private const string UndefinedType = "CS0246";
    private const string UndefinedName = "CS0103";

    // The name resolved, but to a case of a different union imported into the same file.
    private const string WrongPatternType = "CS8121";

    // A prelude success(x) or failure(x) inferred the payload's own type, such as a union case, where the result
    // declares a wider one; there is no conversion between the two pending wrappers.
    private const string NoImplicitConversion = "CS0029";
    private const string NoImplicitConversionCast = "CS0266";

    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds =>
        [UndefinedType, UndefinedName, WrongPatternType, NoImplicitConversion, NoImplicitConversionCast, UnionCasesCanBeImportedAnalyzer.DiagnosticId];

    /// <summary>One import fixes every name of a union in the file, so batching separate fixes would duplicate it.</summary>
    /// <returns><see langword="null"/>; each union is imported by its own action.</returns>
    public override FixAllProvider? GetFixAllProvider() => null;

    /// <inheritdoc/>
    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        var model = await context.Document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null || model is null)
            return;

        foreach (var diagnostic in context.Diagnostics)
        {
            var node = root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true);

            if (diagnostic.Id is NoImplicitConversion or NoImplicitConversionCast)
            {
                RegisterPreludeTypeArgument(context, model, node, diagnostic, context.CancellationToken);
            }
            else if (diagnostic.Id == UndefinedName
                && node is IdentifierNameSyntax { Parent: not MemberAccessExpressionSyntax } preludeName
                && UnionCaseNames.IsPreludeMember(model.Compilation, preludeName.Identifier.ValueText))
            {
                RegisterPreludeImport(context, diagnostic);
            }
            else if (diagnostic.Id == UnionCasesCanBeImportedAnalyzer.DiagnosticId)
            {
                RegisterImportOfQualifiedCases(context, model, node, diagnostic);
            }
            else if (CaseIdentifier(node) is { } identifier
                && (diagnostic.Id != WrongPatternType
                    || UnionCaseNames.IsCase(model.GetSymbolInfo(identifier, context.CancellationToken).Symbol as ITypeSymbol)))
            {
                RegisterFixesForMissingCase(context, model, identifier, diagnostic, context.CancellationToken);
            }
        }
    }

    /// <summary>Offers <c>failure&lt;CustomerError&gt;(error)</c> when <c>failure(error)</c> inferred a narrower type than the result needs.</summary>
    private static void RegisterPreludeTypeArgument(
        CodeFixContext context,
        SemanticModel model,
        SyntaxNode node,
        Diagnostic diagnostic,
        CancellationToken cancellationToken)
    {
        if (node.DescendantNodesAndSelf().OfType<InvocationExpressionSyntax>().FirstOrDefault() is not
            {
                Expression: IdentifierNameSyntax { Identifier.ValueText: "success" or "failure" } name,
                ArgumentList.Arguments.Count: 1
            } invocation
            || model.GetSymbolInfo(invocation, cancellationToken).Symbol is not IMethodSymbol { ContainingType: { } prelude }
            || prelude.ToDisplayString() != UnionCaseNames.PreludeMetadataName
            || CreationTarget(model, invocation, cancellationToken) is not INamedTypeSymbol { TypeArguments.Length: 2 } union
            || !UnionCaseNames.IsUnion(union))
        {
            return;
        }

        // success fills the first type argument; failure fills the second, which for a validation is the error type.
        var payloadType = union.TypeArguments[name.Identifier.ValueText == "success" ? 0 : 1];
        if (!model.ClassifyConversion(invocation.ArgumentList.Arguments[0].Expression, payloadType).IsImplicit)
            return;

        var typeArgument = payloadType.ToMinimalDisplayString(model, invocation.SpanStart);
        var title = $"Use '{name.Identifier.ValueText}<{typeArgument}>'";
        context.RegisterCodeFix(
            CodeAction.Create(
                title,
                async token =>
                {
                    var root = await context.Document.GetSyntaxRootAsync(token).ConfigureAwait(false);
                    var replacement = GenericName(name.Identifier, TypeArgumentList(SingletonSeparatedList(ParseTypeName(typeArgument))))
                        .WithTriviaFrom(name);
                    return context.Document.WithSyntaxRoot(root!.ReplaceNode(name, replacement));
                },
                equivalenceKey: "PreludeTypeArgument:" + title),
            diagnostic);
    }

    private static void RegisterPreludeImport(CodeFixContext context, Diagnostic diagnostic) =>
        context.RegisterCodeFix(
            CodeAction.Create(
                "Add 'using static LondoSpark.Func.Prelude;'",
                async cancellationToken =>
                {
                    var root = (CompilationUnitSyntax?)await context.Document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
                    return root is null
                        ? context.Document
                        : await AddStaticUsingAsync(context.Document, root, UnionCaseNames.PreludeMetadataName, cancellationToken).ConfigureAwait(false);
                },
                equivalenceKey: "ImportPrelude"),
            diagnostic);

    /// <summary>Finds the bare case name a diagnostic points at, which may be reported on the whole pattern.</summary>
    private static IdentifierNameSyntax? CaseIdentifier(SyntaxNode node) =>
        node switch
        {
            IdentifierNameSyntax identifier => identifier,
            RecursivePatternSyntax { Type: IdentifierNameSyntax identifier } => identifier,
            DeclarationPatternSyntax { Type: IdentifierNameSyntax identifier } => identifier,
            TypePatternSyntax { Type: IdentifierNameSyntax identifier } => identifier,
            ConstantPatternSyntax { Expression: IdentifierNameSyntax identifier } => identifier,
            _ => null
        };

    private static void RegisterImportOfQualifiedCases(
        CodeFixContext context,
        SemanticModel model,
        SyntaxNode node,
        Diagnostic diagnostic)
    {
        if (!UnionCaseNames.TryGetQualifiedCase(node, out _, out _)
            || UnionCaseNames.ImportingUnion(model.GetSymbolInfo(node, context.CancellationToken).Symbol) is not { } union)
        {
            return;
        }

        var import = union.ToDisplayString(UnionCaseNames.ImportFormat);
        context.RegisterCodeFix(
            CodeAction.Create(
                $"Add 'using static {import};' and shorten its cases",
                cancellationToken => ImportAndShortenAsync(context.Document, union, import, cancellationToken),
                equivalenceKey: "ImportUnionCases:" + import),
            diagnostic);
    }

    private static void RegisterFixesForMissingCase(
        CodeFixContext context,
        SemanticModel model,
        IdentifierNameSyntax identifier,
        Diagnostic diagnostic,
        CancellationToken cancellationToken)
    {
        var caseName = identifier.Identifier.ValueText;
        var union = FindTargetUnion(model, identifier, cancellationToken);
        if (union is null || !UnionCaseNames.DeclaresImportable(union, caseName))
            return;

        if (UnionCaseNames.CanImport(union)
            && !UnionCasesCanBeImportedAnalyzer.ImportConflicts(model, identifier, union, cancellationToken))
        {
            var import = union.ToDisplayString(UnionCaseNames.ImportFormat);
            context.RegisterCodeFix(
                CodeAction.Create(
                    $"Add 'using static {import};'",
                    token => ImportAndShortenAsync(context.Document, union, import, token),
                    equivalenceKey: "ImportUnionCases:" + import),
                diagnostic);
        }

        var qualified = union.ToMinimalDisplayString(model, identifier.SpanStart) + "." + caseName;
        context.RegisterCodeFix(
            CodeAction.Create(
                $"Use '{qualified}'",
                async token =>
                {
                    var root = await context.Document.GetSyntaxRootAsync(token).ConfigureAwait(false);
                    var replacement = ParseName(qualified).WithTriviaFrom(identifier);
                    return context.Document.WithSyntaxRoot(root!.ReplaceNode(identifier, replacement));
                },
                equivalenceKey: "QualifyUnionCase:" + qualified),
            diagnostic);
    }

    /// <summary>Finds the union a bare case name refers to, from the context the name appears in.</summary>
    private static INamedTypeSymbol? FindTargetUnion(
        SemanticModel model,
        IdentifierNameSyntax identifier,
        CancellationToken cancellationToken)
    {
        var target = identifier.Parent switch
        {
            ObjectCreationExpressionSyntax creation when creation.Type == identifier =>
                CreationTarget(model, creation, cancellationToken),
            RecursivePatternSyntax pattern when pattern.Type == identifier => PatternInput(model, pattern, cancellationToken),
            DeclarationPatternSyntax pattern when pattern.Type == identifier => PatternInput(model, pattern, cancellationToken),
            TypePatternSyntax pattern => PatternInput(model, pattern, cancellationToken),
            ConstantPatternSyntax pattern => PatternInput(model, pattern, cancellationToken),
            BinaryExpressionSyntax binary when binary.IsKind(SyntaxKind.IsExpression) && binary.Right == identifier =>
                model.GetTypeInfo(binary.Left, cancellationToken).Type,
            _ => null
        };

        return UnionCaseNames.IsUnion(target) ? (INamedTypeSymbol)target! : null;
    }

    /// <summary>Finds the type a pattern is matched against, looking through combinators and into a case's payload.</summary>
    private static ITypeSymbol? PatternInput(SemanticModel model, PatternSyntax pattern, CancellationToken cancellationToken)
    {
        SyntaxNode current = pattern;
        while (current.Parent is BinaryPatternSyntax or UnaryPatternSyntax or ParenthesizedPatternSyntax)
            current = current.Parent;

        return current.Parent switch
        {
            SwitchExpressionArmSyntax { Parent: SwitchExpressionSyntax switchExpression } =>
                model.GetTypeInfo(switchExpression.GoverningExpression, cancellationToken).Type,
            CasePatternSwitchLabelSyntax { Parent.Parent: SwitchStatementSyntax switchStatement } =>
                model.GetTypeInfo(switchStatement.Expression, cancellationToken).Type,
            IsPatternExpressionSyntax isPattern =>
                model.GetTypeInfo(isPattern.Expression, cancellationToken).Type,

            // A case nested in another case's payload, such as Some(Success(var value)): the payload's type.
            SubpatternSyntax { Parent: PositionalPatternClauseSyntax { Parent: RecursivePatternSyntax { Type: { } outer } } clause } subpattern
                when clause.Subpatterns.IndexOf(subpattern) == 0
                    && model.GetSymbolInfo(outer, cancellationToken).Symbol is INamedTypeSymbol outerCase
                    && UnionCaseNames.IsCase(outerCase) =>
                outerCase.GetMembers().OfType<IPropertySymbol>().FirstOrDefault(static property => !property.IsStatic)?.Type,
            _ => null
        };
    }

    /// <summary>Finds the type a <c>new Case(...)</c> expression produces, from where its value goes.</summary>
    /// <remarks>
    /// The value may pass through a conditional (<c>taken ? new Failure(e) : new Success(c)</c>), parentheses, or a switch
    /// expression arm before it reaches the return, declaration, cast, or assignment that names the union.
    /// </remarks>
    private static ITypeSymbol? CreationTarget(
        SemanticModel model,
        ExpressionSyntax expression,
        CancellationToken cancellationToken)
    {
        if (model.GetTypeInfo(expression, cancellationToken).ConvertedType is { TypeKind: not TypeKind.Error } converted
            && UnionCaseNames.IsUnion(converted))
        {
            return converted;
        }

        SyntaxNode current = expression;
        while (true)
        {
            switch (current.Parent)
            {
                case ParenthesizedExpressionSyntax parenthesized:
                    current = parenthesized;
                    continue;
                case ConditionalExpressionSyntax conditional when conditional.Condition != current:
                    current = conditional;
                    continue;
                case SwitchExpressionArmSyntax { Parent: SwitchExpressionSyntax switchExpression } arm when arm.Expression == current:
                    current = switchExpression;
                    continue;
            }

            break;
        }

        return current.Parent switch
        {
            ReturnStatementSyntax or ArrowExpressionClauseSyntax => EnclosingReturnType(model, current, cancellationToken),
            EqualsValueClauseSyntax { Parent: VariableDeclaratorSyntax { Parent: VariableDeclarationSyntax { Type: { IsVar: false } type } } } =>
                model.GetTypeInfo(type, cancellationToken).Type,
            CastExpressionSyntax cast => model.GetTypeInfo(cast.Type, cancellationToken).Type,
            AssignmentExpressionSyntax assignment when assignment.Right == current =>
                model.GetTypeInfo(assignment.Left, cancellationToken).Type,
            _ => null
        };
    }

    private static ITypeSymbol? EnclosingReturnType(SemanticModel model, SyntaxNode node, CancellationToken cancellationToken)
    {
        foreach (var ancestor in node.Ancestors())
        {
            var symbol = ancestor switch
            {
                AnonymousFunctionExpressionSyntax lambda => model.GetSymbolInfo(lambda, cancellationToken).Symbol,
                LocalFunctionStatementSyntax or BaseMethodDeclarationSyntax or BasePropertyDeclarationSyntax =>
                    model.GetDeclaredSymbol(ancestor, cancellationToken),
                _ => null
            };

            switch (symbol)
            {
                case IMethodSymbol method:
                    return method.IsAsync ? UnwrapTask(method.ReturnType) : method.ReturnType;
                case IPropertySymbol property:
                    return property.Type;
            }
        }

        return null;
    }

    private static ITypeSymbol UnwrapTask(ITypeSymbol type) =>
        type is INamedTypeSymbol { Name: "Task" or "ValueTask", TypeArguments.Length: 1 } task
            ? task.TypeArguments[0]
            : type;

    /// <summary>Adds the static import, unless present, and shortens every case of <paramref name="union"/> spelled through it.</summary>
    private static async Task<Document> ImportAndShortenAsync(
        Document document,
        INamedTypeSymbol union,
        string import,
        CancellationToken cancellationToken)
    {
        var root = (CompilationUnitSyntax?)await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        var model = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
        if (root is null || model is null)
            return document;

        var qualifiedCases = new List<(SyntaxNode Node, string Case)>();
        foreach (var node in root.DescendantNodes())
        {
            if (UnionCaseNames.TryGetQualifiedCase(node, out var unionName, out var caseName)
                && !(unionName is IdentifierNameSyntax left && model.GetAliasInfo(left, cancellationToken) is not null)
                && UnionCaseNames.ImportingUnion(model.GetSymbolInfo(node, cancellationToken).Symbol) is { } containing
                && SymbolEqualityComparer.Default.Equals(containing, union))
            {
                qualifiedCases.Add((node, caseName.Identifier.ValueText));
            }
        }

        var caseNames = qualifiedCases.ToDictionary(static item => item.Node, static item => item.Case);

        var alreadyImported = root.Usings.Any(directive =>
            !directive.StaticKeyword.IsKind(SyntaxKind.None)
            && directive.Alias is null
            && directive.NamespaceOrType is not null
            && SymbolEqualityComparer.Default.Equals(
                model.GetSymbolInfo(directive.NamespaceOrType, cancellationToken).Symbol, union));

        var updated = root.ReplaceNodes(
            caseNames.Keys,
            (original, _) => IdentifierName(caseNames[original]).WithTriviaFrom(original));

        return alreadyImported
            ? document.WithSyntaxRoot(updated)
            : await AddStaticUsingAsync(document, updated, import, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<Document> AddStaticUsingAsync(
        Document document,
        CompilationUnitSyntax root,
        string import,
        CancellationToken cancellationToken)
    {
        var directive = UsingDirective(Token(SyntaxKind.StaticKeyword), null, ParseName(import))
            .WithAdditionalAnnotations(Formatter.Annotation);

        return await Formatter.FormatAsync(
            document.WithSyntaxRoot(root.AddUsings(directive)),
            Formatter.Annotation,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }
}
