using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace LondoSpark.Func.Analyzers;

/// <summary>Reports awaits of options and results that cannot short-circuit their method, and catch clauses that swallow a short-circuit.</summary>
/// <remarks>
/// Awaiting an option, a result, <c>none</c>, or a prelude <c>failure(...)</c> throws a <c>ShortCircuitException</c> for
/// the absent or failed case. Only the builders of <c>async Option&lt;T&gt;</c>, of <c>[AsyncResult&lt;E&gt;]</c> types,
/// and <c>ShortCircuitMethodBuilder</c> turn it back into a value, and only for the case they can return.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ShortCircuitAwaitAnalyzer : DiagnosticAnalyzer
{
    private const string HelpLink = "https://github.com/londospark/LondoSpark.Func/blob/main/README.md#analyzers";

    internal static readonly DiagnosticDescriptor CannotShortCircuit = new(
        "LSF0003",
        "Awaited value cannot short-circuit this method",
        "Awaiting '{0}' needs a method that can return its {1}; here its other case throws ShortCircuitException",
        "Usage",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Awaiting an option short-circuits only async Option<T> or a ShortCircuitMethodBuilder method returning an option; awaiting a result or failure only an [AsyncResult] type or a ShortCircuitMethodBuilder method with the same failure type.",
        helpLinkUri: HelpLink);

    internal static readonly DiagnosticDescriptor CatchesShortCircuit = new(
        "LSF0004",
        "Catch clause intercepts a short-circuit",
        "This catch clause also catches the short-circuit of an awaited '{0}'; add 'when (e is not ShortCircuitException)'",
        "Usage",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "An absent or failed await unwinds the method with ShortCircuitException. A catch clause that catches it stops the method from returning None or the failure.",
        helpLinkUri: HelpLink);

    internal static readonly DiagnosticDescriptor CannotSuspend = new(
        "LSF0005",
        "Synchronous async method awaits something that can suspend",
        "async '{0}' runs synchronously and cannot await '{1}'; return a ValueTask and add [AsyncMethodBuilder(typeof(ShortCircuitMethodBuilder<>))]",
        "Usage",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "async Option<T> and [AsyncResult] methods return their value before a suspended await could finish, so they throw NotSupportedException when an await suspends.",
        helpLinkUri: HelpLink);

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [CannotShortCircuit, CatchesShortCircuit, CannotSuspend];

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(start =>
        {
            var types = KnownTypes.From(start.Compilation);
            if (types is null)
                return;

            start.RegisterOperationAction(operation => AnalyzeAwait(operation, types), OperationKind.Await);
            start.RegisterOperationAction(operation => AnalyzeTry(operation, types), OperationKind.Try);
        });
    }

    private static void AnalyzeAwait(OperationAnalysisContext context, KnownTypes types)
    {
        var awaited = (IAwaitOperation)context.Operation;
        var function = EnclosingFunction(awaited, context.ContainingSymbol);
        if (function is null)
            return;

        var kind = types.Classify(awaited.Operation.Type);
        var handler = types.Handler(function);

        if (kind.IsShortCircuit)
        {
            if (!handler.Accepts(kind))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    CannotShortCircuit,
                    awaited.Syntax.GetLocation(),
                    awaited.Operation.Type!.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
                    kind.Describe()));
            }
        }
        else if (handler.IsSynchronous && awaited.Operation.Type is { } other)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                CannotSuspend,
                awaited.Syntax.GetLocation(),
                function.ReturnType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
                other.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)));
        }
    }

    private static void AnalyzeTry(OperationAnalysisContext context, KnownTypes types)
    {
        var attempt = (ITryOperation)context.Operation;
        var function = EnclosingFunction(attempt, context.ContainingSymbol);
        if (function is null || !types.Handler(function).IsShortCircuiting)
            return;

        var shortCircuiting = Awaits(attempt.Body)
            .FirstOrDefault(awaited => types.Classify(awaited.Operation.Type).IsShortCircuit);
        if (shortCircuiting is null)
            return;

        foreach (var clause in attempt.Catches)
        {
            if (clause.Filter is null && types.CatchesShortCircuit(clause.ExceptionType))
            {
                var location = clause.Syntax is Microsoft.CodeAnalysis.CSharp.Syntax.CatchClauseSyntax syntax
                    ? syntax.CatchKeyword.GetLocation()
                    : clause.Syntax.GetLocation();
                context.ReportDiagnostic(Diagnostic.Create(
                    CatchesShortCircuit,
                    location,
                    shortCircuiting.Operation.Type!.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)));
            }
        }
    }

    // Awaits in this function, not in lambdas or local functions nested inside it.
    private static IEnumerable<IAwaitOperation> Awaits(IOperation root)
    {
        var pending = new Stack<IOperation>();
        pending.Push(root);
        while (pending.Count > 0)
        {
            var operation = pending.Pop();
            if (operation is IAwaitOperation awaited)
                yield return awaited;

            foreach (var child in operation.ChildOperations)
            {
                if (child is not (IAnonymousFunctionOperation or ILocalFunctionOperation))
                    pending.Push(child);
            }
        }
    }

    private static IMethodSymbol? EnclosingFunction(IOperation operation, ISymbol containing)
    {
        for (var current = operation.Parent; current is not null; current = current.Parent)
        {
            switch (current)
            {
                case IAnonymousFunctionOperation lambda:
                    return lambda.Symbol;
                case ILocalFunctionOperation local:
                    return local.Symbol;
            }
        }

        return containing as IMethodSymbol;
    }

    private readonly struct Kind(bool isShortCircuit, ITypeSymbol? failure)
    {
        public bool IsShortCircuit { get; } = isShortCircuit;

        // Null for absence; the failure type for a result.
        public ITypeSymbol? Failure { get; } = failure;

        public static Kind Other => default;

        public static Kind Absent => new(true, null);

        public static Kind Failed(ITypeSymbol failure) => new(true, failure);

        public string Describe() =>
            Failure is null
                ? "absence (async Option<T>, or a ShortCircuitMethodBuilder method returning an option)"
                : $"failure '{Failure.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)}' (an [AsyncResult] type, or a ShortCircuitMethodBuilder method returning a result with that failure)";
    }

    private readonly struct Handler(Kind kind, bool isSynchronous)
    {
        public bool IsShortCircuiting => kind.IsShortCircuit;

        public bool IsSynchronous { get; } = isSynchronous;

        public bool Accepts(Kind awaited) =>
            kind.IsShortCircuit
            && (kind.Failure is null
                ? awaited.Failure is null
                : awaited.Failure is not null && SymbolEqualityComparer.Default.Equals(kind.Failure, awaited.Failure));
    }

    private sealed class KnownTypes(
        INamedTypeSymbol option,
        INamedTypeSymbol none,
        INamedTypeSymbol result,
        INamedTypeSymbol? failed,
        INamedTypeSymbol? asyncResult,
        INamedTypeSymbol? builder,
        INamedTypeSymbol? exception,
        INamedTypeSymbol? valueTask)
    {
        public static KnownTypes? From(Compilation compilation)
        {
            var option = compilation.GetTypeByMetadataName("LondoSpark.Func.Option`1");
            var none = compilation.GetTypeByMetadataName("LondoSpark.Func.None");
            var result = compilation.GetTypeByMetadataName("LondoSpark.Func.Result`2");
            if (option is null || none is null || result is null)
                return null;

            return new KnownTypes(
                option,
                none,
                result,
                compilation.GetTypeByMetadataName("LondoSpark.Func.Prelude+Failed`1"),
                compilation.GetTypeByMetadataName("LondoSpark.Func.AsyncResultAttribute`1"),
                compilation.GetTypeByMetadataName("LondoSpark.Func.ShortCircuitMethodBuilder`1"),
                compilation.GetTypeByMetadataName("LondoSpark.Func.ShortCircuitException"),
                compilation.GetTypeByMetadataName("System.Threading.Tasks.ValueTask`1"));
        }

        public Kind Classify(ITypeSymbol? type)
        {
            if (type is not INamedTypeSymbol named)
                return Kind.Other;

            var definition = named.OriginalDefinition;
            if (Same(definition, option) || Same(definition, none))
                return Kind.Absent;

            if (Same(definition, result) || Same(definition, failed))
                return Kind.Failed(named.TypeArguments[named.TypeArguments.Length - 1]);

            return AsyncResultFailure(named) is { } failure ? Kind.Failed(failure) : Kind.Other;
        }

        public Handler Handler(IMethodSymbol function)
        {
            if (!function.IsAsync || function.ReturnType is not INamedTypeSymbol returned)
                return default;

            if (Same(returned.OriginalDefinition, option))
                return new Handler(Kind.Absent, isSynchronous: true);

            if (AsyncResultFailure(returned) is { } failure)
                return new Handler(Kind.Failed(failure), isSynchronous: true);

            var overridden = function.GetAttributes().Any(attribute =>
                attribute.AttributeClass?.Name == "AsyncMethodBuilderAttribute"
                && attribute.ConstructorArguments.Length == 1
                && attribute.ConstructorArguments[0].Value is INamedTypeSymbol type
                && Same(type.OriginalDefinition, builder));

            if (overridden && Same(returned.OriginalDefinition, valueTask))
            {
                var kind = Classify(returned.TypeArguments[0]);
                if (kind.IsShortCircuit)
                    return new Handler(kind, isSynchronous: false);
            }

            return default;
        }

        public bool CatchesShortCircuit(ITypeSymbol? caught)
        {
            if (exception is null || caught is null)
                return false;

            for (ITypeSymbol? type = exception; type is not null; type = type.BaseType)
            {
                if (SymbolEqualityComparer.Default.Equals(type, caught))
                    return true;
            }

            return false;
        }

        private ITypeSymbol? AsyncResultFailure(INamedTypeSymbol type)
        {
            if (asyncResult is null)
                return null;

            var attribute = type.OriginalDefinition.GetAttributes()
                .FirstOrDefault(candidate => Same(candidate.AttributeClass?.OriginalDefinition, asyncResult));
            return attribute?.AttributeClass?.TypeArguments[0];
        }

        private static bool Same(ISymbol? left, ISymbol? right) =>
            right is not null && SymbolEqualityComparer.Default.Equals(left, right);
    }
}
