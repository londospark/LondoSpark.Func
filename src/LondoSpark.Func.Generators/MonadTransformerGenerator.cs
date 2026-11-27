using System.Collections.Generic;
using System.Linq;
using System.Text;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace LondoSpark.Func.Generators;

/// <summary>Makes a record struct marked <c>[MonadTransformer]</c>, which wraps <c>Outer&lt;Inner&lt;T&gt;&gt;</c>, a monad that binds both layers.</summary>
/// <remarks>
/// C# cannot abstract over type constructors, so there is no single generic transformer type; the generator writes one
/// per declaration instead. The inner monad must be <c>Option</c> or <c>Result</c>, whose short-circuit composes under
/// any outer monad. The outer monad is used only through query operators (<c>Select</c>, <c>SelectMany</c> with a
/// projection) and a <c>Pure</c>, which the generator supplies for <c>Program</c>, <c>Option</c>, and <c>Result</c>.
/// Over <c>Program</c>, it calls <c>TransformedProgram</c> instead, whose steps check the inner value themselves, so no
/// bind allocates a closure of its own.
/// </remarks>
[Generator(LanguageNames.CSharp)]
public sealed class MonadTransformerGenerator : IIncrementalGenerator
{
    private const string AttributeMetadataName = "LondoSpark.Func.MonadTransformerAttribute";

    internal static readonly DiagnosticDescriptor Shape = new(
        "LSF0104",
        "Monad transformer has the wrong shape",
        "'{0}' must be a top-level partial record struct with one type parameter and one positional parameter of type Outer<Inner<{1}>>, where Inner<{1}> is Option<{1}> or Result<{1}, E> and {1} appears nowhere else",
        "Usage",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "The generator binds the inner Option or Result through the outer monad's query operators, so it needs to find both layers in the wrapped type.");

    internal static readonly DiagnosticDescriptor NoPure = new(
        "LSF0105",
        "Monad transformer needs Pure for its outer monad",
        "The generator cannot wrap a value in '{1}'; declare 'private static {2} Pure<TPure>(TPure value)' on '{0}'",
        "Usage",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "The generator knows how to wrap a plain value in Program, Option, and Result. Any other outer monad needs its Pure declared on the struct.");

    private static readonly SymbolDisplayFormat TypeFormat =
        SymbolDisplayFormat.FullyQualifiedFormat.AddMiscellaneousOptions(
            SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier);

    /// <inheritdoc/>
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var monads = context.SyntaxProvider.ForAttributeWithMetadataName(
            AttributeMetadataName,
            static (node, _) => node is RecordDeclarationSyntax or StructDeclarationSyntax,
            static (attributeContext, cancellationToken) => Describe(attributeContext, cancellationToken));

        context.RegisterSourceOutput(monads, static (output, monad) =>
        {
            if (monad.Problem is { } problem)
            {
                output.ReportDiagnostic(Diagnostic.Create(problem.Descriptor, problem.Location.ToLocation(), problem.Arguments.ToArray<object>()));
                return;
            }

            output.AddSource($"{monad.HintName}.MonadTransformer.g.cs", Render(monad));
        });
    }

    private static MonadTransformer Describe(GeneratorAttributeSyntaxContext context, System.Threading.CancellationToken cancellationToken)
    {
        var symbol = (INamedTypeSymbol)context.TargetSymbol;
        var declaration = (TypeDeclarationSyntax)context.TargetNode;
        var location = LocationInfo.From(declaration.Identifier.GetLocation());
        var typeParameter = symbol.TypeParameters.FirstOrDefault();
        var shapeProblem = new Problem(Shape, location, new EquatableArray<string>([symbol.Name, typeParameter?.Name ?? "T"]));

        if (declaration is not RecordDeclarationSyntax { ClassOrStructKeyword.RawKind: (int)SyntaxKind.StructKeyword, ParameterList.Parameters.Count: 1 } record
            || !declaration.Modifiers.Any(SyntaxKind.PartialKeyword)
            || symbol.ContainingType is not null
            || typeParameter is null
            || symbol.TypeParameters.Length != 1
            || context.SemanticModel.GetDeclaredSymbol(record.ParameterList.Parameters[0], cancellationToken) is not { Type: INamedTypeSymbol wrapped } parameter)
        {
            return MonadTransformer.Failed(shapeProblem);
        }

        // Find the one type argument of the outer monad that is Option<T> or Result<T, E>.
        var slots = wrapped.TypeArguments
            .Select((argument, index) => (argument, index))
            .Where(slot => Inner(slot.argument, typeParameter) is not null)
            .ToList();
        if (wrapped.ContainingType is not null || slots.Count != 1)
            return MonadTransformer.Failed(shapeProblem);

        var (innerType, slot) = slots[0];
        var inner = Inner(innerType, typeParameter)!;
        var others = wrapped.TypeArguments.Where((_, index) => index != slot);
        if (others.Any(other => Mentions(other, typeParameter)) || (inner.Failure is { } failureType && Mentions(failureType, typeParameter)))
            return MonadTransformer.Failed(shapeProblem);

        var outerName = wrapped.OriginalDefinition.ToDisplayString(TypeFormat);
        outerName = outerName.Substring(0, outerName.IndexOf('<'));
        var outerArguments = wrapped.TypeArguments.Select(argument => argument.ToDisplayString(TypeFormat)).ToArray();

        string Outer(string argument)
        {
            var arguments = (string[])outerArguments.Clone();
            arguments[slot] = argument;
            return $"{outerName}<{string.Join(", ", arguments)}>";
        }

        var pure = KnownPure(wrapped, slot, outerArguments);
        var declaresPure = symbol.GetMembers("Pure").OfType<IMethodSymbol>().Any(static method => method.IsStatic && method.TypeParameters.Length == 1 && method.Parameters.Length == 1);
        if (pure is null && !declaresPure)
        {
            return MonadTransformer.Failed(new Problem(
                NoPure,
                location,
                new EquatableArray<string>([symbol.Name, wrapped.OriginalDefinition.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat), Outer("TPure").Replace("global::", string.Empty)])));
        }

        var usings = declaration.SyntaxTree.GetCompilationUnitRoot(cancellationToken).Usings
            .Concat(declaration.Ancestors().OfType<BaseNamespaceDeclarationSyntax>().SelectMany(static ns => ns.Usings))
            .Select(static directive => directive.ToString())
            .Distinct()
            .ToArray();

        return new MonadTransformer(
            symbol.ContainingNamespace.IsGlobalNamespace ? null : symbol.ContainingNamespace.ToDisplayString(),
            symbol.Name,
            typeParameter.Name,
            parameter.Name,
            symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat).Replace("global::", string.Empty).Replace('<', '_').Replace('>', '_'),
            symbol.DeclaredAccessibility == Accessibility.Public ? "public" : "internal",
            Outer("{0}"),
            inner.Failure?.ToDisplayString(TypeFormat),
            declaresPure ? null : pure,
            pure is not null,
            ProgramOps(wrapped, slot, outerArguments),
            new EquatableArray<string>([.. usings]),
            null);
    }

    private static InnerShape? Inner(ITypeSymbol type, ITypeParameterSymbol typeParameter)
    {
        if (type is not INamedTypeSymbol { ContainingNamespace: { } ns } named || ns.ToDisplayString() != "LondoSpark.Func"
            || !SymbolEqualityComparer.Default.Equals(named.TypeArguments.FirstOrDefault(), typeParameter))
        {
            return null;
        }

        return named.MetadataName switch
        {
            "Option`1" => new InnerShape(null),
            "Result`2" => new InnerShape(named.TypeArguments[1]),
            _ => null
        };
    }

    private static bool Mentions(ITypeSymbol type, ITypeParameterSymbol typeParameter) =>
        SymbolEqualityComparer.Default.Equals(type, typeParameter)
        || (type is INamedTypeSymbol named && named.TypeArguments.Any(argument => Mentions(argument, typeParameter)))
        || (type is IArrayTypeSymbol array && Mentions(array.ElementType, typeParameter));

    // How to wrap a value in the outer monads this library defines; {0} is the value's type.
    private static string? KnownPure(INamedTypeSymbol outer, int slot, string[] arguments)
    {
        if (outer.ContainingNamespace?.ToDisplayString() != "LondoSpark.Func")
            return null;

        return (outer.MetadataName, slot) switch
        {
            ("Program`2", 1) => $"global::LondoSpark.Func.Program.Return<{arguments[0]}, {{0}}>(value)",
            ("Option`1", 0) => "global::LondoSpark.Func.Option.Return<{0}>(value)",
            ("Result`2", 0) => $"global::LondoSpark.Func.Result.Return<{{0}}, {arguments[1]}>(value)",
            _ => null
        };
    }

    // The instruction set when the outer monad is Program<TOp, _>, whose transformer operations the library builds into
    // the program's own steps.
    private static string? ProgramOps(INamedTypeSymbol outer, int slot, string[] arguments) =>
        outer.ContainingNamespace?.ToDisplayString() == "LondoSpark.Func" && outer.MetadataName == "Program`2" && slot == 1
            ? arguments[0]
            : null;

    private static string Render(MonadTransformer monad)
    {
        var n = monad.Name;
        var t = monad.TypeParameter;
        var value = monad.Property;
        var self = $"{n}<{t}>";
        var isResult = monad.Failure is not null;
        string Outer(string argument) => string.Format(System.Globalization.CultureInfo.InvariantCulture, monad.Outer, argument);
        string Inner(string argument) => isResult
            ? $"global::LondoSpark.Func.Result<{argument}, {monad.Failure}>"
            : $"global::LondoSpark.Func.Option<{argument}>";
        string Succeed(string argument, string expression) => isResult
            ? $"global::LondoSpark.Func.Result.Return<{argument}, {monad.Failure}>({expression})"
            : $"global::LondoSpark.Func.Option.Return<{argument}>({expression})";
        string Case(string argument) => isResult ? $"{Inner(argument)}.Success" : $"{Inner(argument)}.Some";
        var wrapped = Outer(Inner(t));

        // The outer monads this library defines have Bind; any other is bound through its query operator, whose
        // projection only passes the result on.
        string Bound(string continuation) => monad.HasBind
            ? $"{value}.Bind({continuation})"
            : $"{value}.SelectMany({continuation}, static (_, result) => result)";

        // Over a program, the library's own steps check the inner value, so binding allocates no closure of its own.
        var ops = monad.ProgramOps;
        const string Transformed = "global::LondoSpark.Func.TransformedProgram";
        var failureArgument = isResult ? $", {monad.Failure}" : string.Empty;
        var lift = ops is null
            ? $"step.Select(static value => {Succeed(t, "value")})"
            : $"{Transformed}.Lift<{ops}, {t}{failureArgument}>(step)";
        var select = ops is null
            ? $"{value}.Select(inner => Map(inner, selector))"
            : $"{Transformed}.Select<{ops}, {t}{failureArgument}, TOut>({value}, selector)";
        var bind = ops is null
            ? Bound("inner => Continue(inner, binder)")
            : $"{Transformed}.Bind<{ops}, {t}{failureArgument}, TOut, {n}<TOut>>({value}, binder)";
        var selectMany = ops is null
            ? Bound($"inner => inner.TryGetValue(out {Case(t)} first) ? next(first.Value).{value}.Select(second => Combine(first.Value, second, project)) : Pure<{Inner("TOut")}>(Propagate<{t}, TOut>(inner))")
            : $"{Transformed}.SelectMany<{ops}, {t}{failureArgument}, TNext, TOut, {n}<TNext>>({value}, next, project)";
        var selectManyOuter = ops is null
            ? Bound($"inner => inner.TryGetValue(out {Case(t)} first) ? next(first.Value).Select(second => {Succeed("TOut", "project(first.Value, second)")}) : Pure<{Inner("TOut")}>(Propagate<{t}, TOut>(inner))")
            : $"{Transformed}.SelectManyLifted<{ops}, {t}{failureArgument}, TNext, TOut>({value}, next, project)";
        var selectManyInner = ops is null
            ? $"{value}.Select(inner => inner.TryGetValue(out {Case(t)} first) ? Combine(first.Value, next(first.Value), project) : Propagate<{t}, TOut>(inner))"
            : $"{Transformed}.SelectManyInner<{ops}, {t}{failureArgument}, TNext, TOut>({value}, next, project)";

        var builder = new StringBuilder();
        builder.AppendLine("// <auto-generated/>");
        builder.AppendLine("#nullable enable");
        builder.AppendLine();
        foreach (var directive in monad.Usings)
        {
            builder.AppendLine(directive);
        }

        if (!monad.Usings.IsEmpty)
            builder.AppendLine();

        if (monad.Namespace is { } ns)
        {
            builder.Append("namespace ").Append(ns).AppendLine(";");
            builder.AppendLine();
        }

        builder.AppendLine($$"""
            partial record struct {{self}} : global::LondoSpark.Func.IMonadTransformer<{{wrapped}}>
            {
                {{wrapped}} global::LondoSpark.Func.IMonadTransformer<{{wrapped}}>.Wrapped => {{value}};

                /// <summary>A step that produces <paramref name="value"/> without running anything or failing.</summary>
                public static {{self}} Return({{t}} value) => new(Pure<{{Inner(t)}}>({{Succeed(t, "value")}}));

                /// <summary>Lifts a step of the outer monad, which cannot fail, into this one.</summary>
                public static {{self}} Lift({{Outer(t)}} step) => new({{lift}});
            """);

        if (isResult)
        {
            builder.AppendLine($$"""

                    /// <summary>A step that fails with <paramref name="error"/>; later steps do not run.</summary>
                    public static {{self}} Failure({{monad.Failure}} error) => new(Pure<{{Inner(t)}}>(global::LondoSpark.Func.Result.Failure<{{t}}, {{monad.Failure}}>(error)));

                    /// <summary>Completes a prelude <c>failure(error)</c>.</summary>
                    public static implicit operator {{self}}(global::LondoSpark.Func.Prelude.Failed<{{monad.Failure}}> failed) => Failure(failed.Value);

                    /// <summary>Completes a prelude <c>success(value)</c>.</summary>
                    public static implicit operator {{self}}(global::LondoSpark.Func.Prelude.Succeeded<{{t}}> succeeded) => Return(succeeded.Value);
                """);
        }
        else
        {
            builder.AppendLine($$"""

                    /// <summary>A step that is absent; later steps do not run.</summary>
                    public static {{self}} None() => new(Pure<{{Inner(t)}}>(global::LondoSpark.Func.Option.Empty<{{t}}>()));

                    /// <summary>Converts the prelude <c>none</c>.</summary>
                    public static implicit operator {{self}}(global::LondoSpark.Func.None none) => None();
                """);
        }

        builder.AppendLine($$"""

                /// <summary>Wraps the composed value.</summary>
                public static implicit operator {{self}}({{wrapped}} value) => new(value);

                /// <summary>Unwraps the composed value, for an interpreter or the outer monad's own operations.</summary>
                public static implicit operator {{wrapped}}({{self}} monad) => monad.{{value}};

                /// <summary>A step that produces the inner value without running anything.</summary>
                public static implicit operator {{self}}({{Inner(t)}} inner) => new(Pure<{{Inner(t)}}>(inner));

                /// <summary>Transforms the value, carrying a failure through unchanged.</summary>
                public {{n}}<TOut> Select<TOut>(global::System.Func<{{t}}, TOut> selector)
                {
                    global::System.ArgumentNullException.ThrowIfNull(selector);
                    return new({{select}});
                }

                /// <summary>Continues with another step, unless this one failed.</summary>
                public {{n}}<TOut> Bind<TOut>(global::System.Func<{{t}}, {{n}}<TOut>> binder)
                {
                    global::System.ArgumentNullException.ThrowIfNull(binder);
                    return new({{bind}});
                }

                /// <summary>Composes two steps of this type in query syntax.</summary>
                public {{n}}<TOut> SelectMany<TNext, TOut>(global::System.Func<{{t}}, {{n}}<TNext>> next, global::System.Func<{{t}}, TNext, TOut> project)
                {
                    global::System.ArgumentNullException.ThrowIfNull(next);
                    global::System.ArgumentNullException.ThrowIfNull(project);
                    return new({{selectMany}});
                }

                /// <summary>Composes this type with a step of the outer monad, which cannot fail, in query syntax.</summary>
                public {{n}}<TOut> SelectMany<TNext, TOut>(global::System.Func<{{t}}, {{Outer("TNext")}}> next, global::System.Func<{{t}}, TNext, TOut> project)
                {
                    global::System.ArgumentNullException.ThrowIfNull(next);
                    global::System.ArgumentNullException.ThrowIfNull(project);
                    return new({{selectManyOuter}});
                }

                /// <summary>Composes this type with a plain {{(isResult ? "result" : "option")}} in query syntax.</summary>
                public {{n}}<TOut> SelectMany<TNext, TOut>(global::System.Func<{{t}}, {{Inner("TNext")}}> next, global::System.Func<{{t}}, TNext, TOut> project)
                {
                    global::System.ArgumentNullException.ThrowIfNull(next);
                    global::System.ArgumentNullException.ThrowIfNull(project);
                    return new({{selectManyInner}});
                }
            """);

        if (ops is null)
        {
            builder.AppendLine($$"""

                    private static {{Inner("TOut")}} Combine<TNext, TOut>({{t}} first, {{Inner("TNext")}} second, global::System.Func<{{t}}, TNext, TOut> project) =>
                        second.TryGetValue(out {{Case("TNext")}} present) ? {{Succeed("TOut", "project(first, present.Value)")}} : Propagate<TNext, TOut>(second);

                    private static {{Inner("TOut")}} Map<TOut>({{Inner(t)}} inner, global::System.Func<{{t}}, TOut> selector) =>
                        inner.TryGetValue(out {{Case(t)}} present) ? {{Succeed("TOut", "selector(present.Value)")}} : Propagate<{{t}}, TOut>(inner);

                    private static {{Outer(Inner("TOut"))}} Continue<TOut>({{Inner(t)}} inner, global::System.Func<{{t}}, {{n}}<TOut>> binder) =>
                        inner.TryGetValue(out {{Case(t)}} present) ? binder(present.Value).{{value}} : Pure<{{Inner("TOut")}}>(Propagate<{{t}}, TOut>(inner));
                """);

            // Over a program, TransformedProgram does all of this in the program's own steps.
            builder.AppendLine(isResult
                ? $$"""

                    private static {{Inner("TOut")}} Propagate<TIn, TOut>({{Inner("TIn")}} inner) =>
                        inner.TryGetValue(out {{Inner("TIn")}}.Failure failure)
                            ? global::LondoSpark.Func.Result.Failure<TOut, {{monad.Failure}}>(failure.Value)
                            : throw new global::System.InvalidOperationException("A default result contains no case. Construct results with Result.Return or Result.Failure.");
                """
                : $$"""

                    private static {{Inner("TOut")}} Propagate<TIn, TOut>({{Inner("TIn")}} inner) => global::LondoSpark.Func.Option.Empty<TOut>();
                """);
        }

        if (monad.Pure is { } pure)
        {
            builder.AppendLine($$"""

                    private static {{Outer("TPure")}} Pure<TPure>(TPure value) => {{string.Format(System.Globalization.CultureInfo.InvariantCulture, pure, "TPure")}};
                """);
        }

        builder.AppendLine("}");
        builder.AppendLine();
        builder.AppendLine($$"""
            /// <summary>Lets a query over <see cref="{{n}}{T}"/> start from a step of the outer or inner monad.</summary>
            {{monad.Accessibility}} static class {{n}}Queries
            {
                /// <summary>Continues a step of the outer monad, which cannot fail, with a step of <see cref="{{n}}{T}"/>.</summary>
                public static {{n}}<TOut> SelectMany<{{t}}, TNext, TOut>(this {{Outer(t)}} source, global::System.Func<{{t}}, {{n}}<TNext>> next, global::System.Func<{{t}}, TNext, TOut> project) =>
                    {{self}}.Lift(source).SelectMany(next, project);

                /// <summary>Continues a step of the outer monad, which cannot fail, with a plain {{(isResult ? "result" : "option")}}, giving a <see cref="{{n}}{T}"/>.</summary>
                public static {{n}}<TOut> SelectMany<{{t}}, TNext, TOut>(this {{Outer(t)}} source, global::System.Func<{{t}}, {{Inner("TNext")}}> next, global::System.Func<{{t}}, TNext, TOut> project) =>
                    {{self}}.Lift(source).SelectMany(next, project);

                /// <summary>Continues a plain {{(isResult ? "result" : "option")}} with a step of <see cref="{{n}}{T}"/>.</summary>
                public static {{n}}<TOut> SelectMany<{{t}}, TNext, TOut>(this {{Inner(t)}} source, global::System.Func<{{t}}, {{n}}<TNext>> next, global::System.Func<{{t}}, TNext, TOut> project) =>
                    (({{self}})source).SelectMany(next, project);
            }
            """);

        return builder.ToString();
    }

    private sealed record InnerShape(ITypeSymbol? Failure);

    private sealed record Problem(DiagnosticDescriptor Descriptor, LocationInfo Location, EquatableArray<string> Arguments);

    private sealed record MonadTransformer(
        string? Namespace,
        string Name,
        string TypeParameter,
        string Property,
        string HintName,
        string Accessibility,
        string Outer,
        string? Failure,
        string? Pure,
        bool HasBind,
        string? ProgramOps,
        EquatableArray<string> Usings,
        Problem? Problem)
    {
        public static MonadTransformer Failed(Problem problem) =>
            new(null, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, null, null, false, null, new EquatableArray<string>([]), problem);
    }
}
