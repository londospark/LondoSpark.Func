using System.Collections.Immutable;
using System.Linq;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace LondoSpark.Func.Analyzers;

/// <summary>Suggests a <c>using static</c> import for case names spelled through their union, such as <c>Result&lt;A, B&gt;.Success</c>.</summary>
/// <remarks>
/// Importing the closed union type lets switch arms read <c>Success(var value)</c>. The rule is a suggestion: it skips
/// names written through an alias, unions over type parameters (which a static import cannot name), and files that
/// already import a different union with a case of the same name, where an import would be ambiguous.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class UnionCasesCanBeImportedAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The identifier of the diagnostic produced by this analyzer.</summary>
    public const string DiagnosticId = "LSF0002";

    /// <summary>The diagnostic property holding the union to import, rendered for a <c>using static</c> directive.</summary>
    public const string ImportProperty = "Import";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Union cases can be imported",
        "Import '{0}' to write '{1}' without its union type",
        "Style",
        DiagnosticSeverity.Info,
        isEnabledByDefault: true,
        description: "A 'using static' directive for the closed union type brings its case types into scope, so switch arms and constructions name the case alone.",
        helpLinkUri: "https://github.com/londospark/LondoSpark.Func/blob/main/README.md#analyzers");

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(
            AnalyzeQualifiedName,
            SyntaxKind.QualifiedName,
            SyntaxKind.SimpleMemberAccessExpression);
    }

    private static void AnalyzeQualifiedName(SyntaxNodeAnalysisContext context)
    {
        // A case is a qualified name in type position and a member access in a constant pattern; only the outermost
        // name of a chain names the case, and using directives name types, not cases.
        if (!UnionCaseNames.TryGetQualifiedCase(context.Node, out var unionName, out _))
            return;

        var name = context.Node;
        if (context.SemanticModel.GetSymbolInfo(name, context.CancellationToken).Symbol is not INamedTypeSymbol caseType
            || !UnionCaseNames.IsCase(caseType)
            || caseType.ContainingType is not { } union
            || !UnionCaseNames.CanImport(union))
        {
            return;
        }

        // An alias is the documented way to match several unions in one file; leave it alone.
        if (unionName is IdentifierNameSyntax left && context.SemanticModel.GetAliasInfo(left, context.CancellationToken) is not null)
            return;

        if (ImportConflicts(context.SemanticModel, name, union, context.CancellationToken))
            return;

        var import = union.ToDisplayString(UnionCaseNames.ImportFormat);
        context.ReportDiagnostic(Diagnostic.Create(
            Rule,
            name.GetLocation(),
            ImmutableDictionary<string, string?>.Empty.Add(ImportProperty, import),
            import,
            caseType.Name));
    }

    /// <summary>Determines whether a static import of <paramref name="union"/> would make a case name ambiguous.</summary>
    /// <param name="model">The semantic model of the file.</param>
    /// <param name="node">A node in the file.</param>
    /// <param name="union">The union that would be imported.</param>
    /// <param name="cancellationToken">Cancels the lookup.</param>
    /// <returns><see langword="true"/> when another imported union already supplies a case with the same name.</returns>
    public static bool ImportConflicts(
        SemanticModel model,
        SyntaxNode node,
        INamedTypeSymbol union,
        System.Threading.CancellationToken cancellationToken)
    {
        var root = node.SyntaxTree.GetRoot(cancellationToken);
        var caseNames = UnionCaseNames.CaseNames(union).ToImmutableHashSet();

        foreach (var directive in root.DescendantNodes().OfType<UsingDirectiveSyntax>())
        {
            if (directive.StaticKeyword.IsKind(SyntaxKind.None) || directive.Alias is not null || directive.NamespaceOrType is null)
                continue;

            if (model.GetSymbolInfo(directive.NamespaceOrType, cancellationToken).Symbol is not INamedTypeSymbol imported
                || SymbolEqualityComparer.Default.Equals(imported, union))
            {
                continue;
            }

            if (UnionCaseNames.CaseNames(imported).Any(caseNames.Contains))
                return true;
        }

        return false;
    }
}
