using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace LondoSpark.Func.Analyzers;

/// <summary>Reports <c>Result&lt;TSuccess, TFailure&gt;</c> values that are created without a case.</summary>
/// <remarks>
/// A struct always has a zero value, and <c>Result</c> has no case that zero-initialisation can represent:
/// its case patterns never match and <see cref="Result{TSuccess, TFailure}.Value"/> throws. The library never
/// produces such a value, so the only way to obtain one is <c>default</c>, <c>new()</c>, or an empty array,
/// which this analyzer reports. Zeroing that the analyzer cannot see - unassigned fields, zeroed array
/// elements, and generic <c>default</c> - is still rejected at runtime.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ResultHasNoDefaultValueAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The identifier of the diagnostic produced by this analyzer.</summary>
    public const string DiagnosticId = "LSF0001";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Result has no zero value",
        "'{0}' has no zero value; construct it with 'Result.Return' or 'Result.Failure'",
        "Usage",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "A default, empty new, or zero-initialised Result contains no case: neither case pattern matches and reading Value throws.",
        helpLinkUri: "https://github.com/londospark/LondoSpark.Func/blob/main/README.md#analyzers");

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(compilationContext =>
        {
            var resultType = compilationContext.Compilation.GetTypeByMetadataName("LondoSpark.Func.Result`2");
            if (resultType is null)
                return;

            compilationContext.RegisterOperationAction(
                operationContext => Report(operationContext, operationContext.Operation.Type, resultType),
                OperationKind.DefaultValue);

            compilationContext.RegisterOperationAction(
                operationContext => Report(operationContext, operationContext.Operation.Type, resultType),
                OperationKind.ObjectCreation);

            compilationContext.RegisterOperationAction(
                operationContext =>
                {
                    if (operationContext.Operation is IArrayCreationOperation { Type: IArrayTypeSymbol arrayType })
                    {
                        Report(operationContext, arrayType.ElementType, resultType);
                    }
                },
                OperationKind.ArrayCreation);
        });
    }

    private static void Report(OperationAnalysisContext context, ITypeSymbol? type, INamedTypeSymbol resultType)
    {
        if (type is not INamedTypeSymbol namedType
            || !SymbolEqualityComparer.Default.Equals(namedType.OriginalDefinition, resultType))
        {
            return;
        }

        if (context.Operation is IObjectCreationOperation { Arguments.IsEmpty: false })
            return;

        context.ReportDiagnostic(
            Diagnostic.Create(
                Rule,
                context.Operation.Syntax.GetLocation(),
                namedType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)));
    }
}
