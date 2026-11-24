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

    /// <inheritdoc/>
    public override ImmutableArray<string> FixableDiagnosticIds =>
        [UndefinedType, UndefinedName, WrongPatternType, UnionCasesCanBeImportedAnalyzer.DiagnosticId];

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

            if (diagnostic.Id == UnionCasesCanBeImportedAnalyzer.DiagnosticId)
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
            || model.GetSymbolInfo(node, context.CancellationToken).Symbol is not INamedTypeSymbol { ContainingType: { } union })
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
        if (union is null || union.GetTypeMembers(caseName).IsEmpty)
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

    /// <summary>Finds the type a <c>new Case(...)</c> expression converts to, from its surrounding statement.</summary>
    private static ITypeSymbol? CreationTarget(
        SemanticModel model,
        ObjectCreationExpressionSyntax creation,
        CancellationToken cancellationToken)
    {
        if (model.GetTypeInfo(creation, cancellationToken).ConvertedType is { TypeKind: not TypeKind.Error } converted
            && UnionCaseNames.IsUnion(converted))
        {
            return converted;
        }

        return creation.Parent switch
        {
            ReturnStatementSyntax or ArrowExpressionClauseSyntax => EnclosingReturnType(model, creation, cancellationToken),
            EqualsValueClauseSyntax { Parent: VariableDeclaratorSyntax { Parent: VariableDeclarationSyntax { Type: { IsVar: false } type } } } =>
                model.GetTypeInfo(type, cancellationToken).Type,
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
                && model.GetSymbolInfo(node, cancellationToken).Symbol is INamedTypeSymbol { ContainingType: { } containing }
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

        if (!alreadyImported)
        {
            var directive = UsingDirective(Token(SyntaxKind.StaticKeyword), null, ParseName(import))
                .WithAdditionalAnnotations(Formatter.Annotation);
            updated = updated.AddUsings(directive);
        }

        return await Formatter.FormatAsync(
            document.WithSyntaxRoot(updated),
            Formatter.Annotation,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }
}
