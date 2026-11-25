using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace LondoSpark.Func.Analyzers;

/// <summary>Reports struct unions in this library that are created without a case.</summary>
/// <remarks>
/// <c>Result&lt;TSuccess, TFailure&gt;</c>, <c>Validation&lt;TSuccess, TValidationError&gt;</c>, and
/// <c>ErrorResult&lt;TSuccess, TFailure&gt;</c>, and <c>Program&lt;TOp, T&gt;</c> are structs with no case that zero-initialisation can represent:
/// their case patterns never match and their operations throw. The library never produces such a value, so the
/// only way to obtain one is <c>default</c>, <c>new()</c>, or an array created without an initializer, which this
/// analyzer reports. Zeroing that the analyzer cannot see - unassigned fields, zeroed elements of a resized array,
/// and generic <c>default</c> - is still rejected at runtime. <c>Option&lt;TSome&gt;</c> is not reported because its
/// zero value is <c>None</c>.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ResultHasNoDefaultValueAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The identifier of the diagnostic produced by this analyzer.</summary>
    public const string DiagnosticId = "LSF0001";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Union has no zero value",
        "'{0}' has no zero value; construct it with {1}",
        "Usage",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "A default, empty new, or zero-initialised Result, Validation, ErrorResult, or Program contains no case: neither case pattern matches and its operations throw.",
        helpLinkUri: "https://github.com/londospark/LondoSpark.Func/blob/main/README.md#analyzers");

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(compilationContext =>
        {
            var compilation = compilationContext.Compilation;
            var builder = ImmutableDictionary.CreateBuilder<INamedTypeSymbol, string>(SymbolEqualityComparer.Default);
            Add(compilation, builder, "LondoSpark.Func.Result`2", "'Result.Return' or 'Result.Failure'");
            Add(compilation, builder, "LondoSpark.Func.Validation`2", "'Validation.Return' or 'Validation.Failure'");
            Add(compilation, builder, "LondoSpark.Func.ErrorResult`2", "'ErrorResult.Return' or 'AsErrorResult()'");
            Add(compilation, builder, "LondoSpark.Func.Program`2", "'Program.Return', 'Program.Lift', or a generated instruction constructor");

            if (builder.Count == 0)
                return;

            var guarded = builder.ToImmutable();

            compilationContext.RegisterOperationAction(
                operationContext => Report(operationContext, operationContext.Operation.Type, guarded),
                OperationKind.DefaultValue);

            compilationContext.RegisterOperationAction(
                operationContext =>
                {
                    if (operationContext.Operation is IObjectCreationOperation { Arguments.IsEmpty: true } creation)
                    {
                        Report(operationContext, creation.Type, guarded);
                    }
                },
                OperationKind.ObjectCreation);

            compilationContext.RegisterOperationAction(
                operationContext =>
                {
                    // An initializer assigns every element, and a zero-length array has no elements to leave zeroed.
                    if (operationContext.Operation is IArrayCreationOperation { Type: IArrayTypeSymbol arrayType, Initializer: null } creation
                        && !IsEmpty(creation))
                    {
                        Report(operationContext, arrayType.ElementType, guarded);
                    }
                },
                OperationKind.ArrayCreation);
        });
    }

    private static void Add(
        Compilation compilation,
        ImmutableDictionary<INamedTypeSymbol, string>.Builder builder,
        string metadataName,
        string constructionHint)
    {
        if (compilation.GetTypeByMetadataName(metadataName) is { } type)
        {
            builder[type] = constructionHint;
        }
    }

    private static bool IsEmpty(IArrayCreationOperation creation)
    {
        foreach (var size in creation.DimensionSizes)
        {
            if (size.ConstantValue is { HasValue: true, Value: 0 })
                return true;
        }

        return false;
    }

    private static void Report(
        OperationAnalysisContext context,
        ITypeSymbol? type,
        ImmutableDictionary<INamedTypeSymbol, string> guarded)
    {
        if (type is not INamedTypeSymbol namedType
            || !guarded.TryGetValue(namedType.OriginalDefinition, out var constructionHint))
        {
            return;
        }

        context.ReportDiagnostic(
            Diagnostic.Create(
                Rule,
                context.Operation.Syntax.GetLocation(),
                namedType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
                constructionHint));
    }
}
