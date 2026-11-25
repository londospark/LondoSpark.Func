using System.Collections.Immutable;
using System.Linq;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace LondoSpark.Func.Analyzers;

/// <summary>Suppresses CA2012 on query expressions that build an asynchronous option or result.</summary>
/// <remarks>
/// <c>await (from x in FindAsync(id) from y in LoadAsync(x) select y)</c> awaits the query's <c>ValueTask</c> exactly
/// once, but the compiler wraps the final <c>SelectMany</c> in a translated-query operation, so CA2012 does not see the
/// await and reports the clause. The suppression applies only when the query's own type is a <c>ValueTask</c> of
/// <c>Option</c> or <c>Result</c>; any other misuse of a <c>ValueTask</c> is still reported.
/// </remarks>
[DiagnosticAnalyzer(Microsoft.CodeAnalysis.LanguageNames.CSharp)]
public sealed class AsyncQueryValueTaskSuppressor : DiagnosticSuppressor
{
    private static readonly SuppressionDescriptor Rule = new(
        "LSFSP001",
        "CA2012",
        "A query over an asynchronous option or result produces one ValueTask, which the surrounding await consumes once.");

    /// <inheritdoc/>
    public override ImmutableArray<SuppressionDescriptor> SupportedSuppressions => [Rule];

    /// <inheritdoc/>
    public override void ReportSuppressions(SuppressionAnalysisContext context)
    {
        foreach (var diagnostic in context.ReportedDiagnostics)
        {
            var tree = diagnostic.Location.SourceTree;
            if (tree is null)
            {
                continue;
            }

            var node = tree.GetRoot(context.CancellationToken).FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true);
            var query = node.AncestorsAndSelf().OfType<QueryExpressionSyntax>().FirstOrDefault();
            if (query is null)
            {
                continue;
            }

            var model = context.GetSemanticModel(tree);
            if (IsAsyncUnion(model.GetTypeInfo(query, context.CancellationToken).Type))
            {
                context.ReportSuppression(Suppression.Create(Rule, diagnostic));
            }
        }
    }

    private static bool IsAsyncUnion(ITypeSymbol? type) =>
        type is INamedTypeSymbol { Name: "ValueTask", TypeArguments.Length: 1 } task
        && task.ContainingNamespace?.ToDisplayString() == "System.Threading.Tasks"
        && task.TypeArguments[0] is INamedTypeSymbol { Name: "Option" or "Result" } union
        && union.ContainingNamespace?.ToDisplayString() == "LondoSpark.Func";
}
