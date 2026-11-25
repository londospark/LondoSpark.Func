using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using LondoSpark.Func.Generators;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace LondoSpark.Func.Tests;

/// <summary>The instruction-set generator's output and diagnostics; the generated code itself is exercised by <see cref="ProgramTests"/>.</summary>
/// <remarks>
/// These tests drive the generator through the Roslyn 4.14 API, which predates the <c>union</c> keyword, so the
/// instruction sets are hand-written <c>[Union]</c> structs: the generator reads both the same way, from the case
/// constructors.
/// </remarks>
public sealed class InstructionsGeneratorTests
{
    private const string Instructions = """
        using LondoSpark.Func;

        namespace Sample;

        public sealed record Ask(string Prompt) : IInstruction<string>;
        public sealed record Wait(int Seconds = 1, string? @class = null) : IInstruction<Unit>;

        [System.Runtime.CompilerServices.Union]
        public readonly struct Op
        {
            public Op(Ask value) => Value = value;
            public Op(Wait value) => Value = value;
            public object? Value { get; }
        }
        """;

    [Test]
    public async Task AValidInstructionSetGeneratesConstructorsHandlersAndInterpreters()
    {
        var (diagnostics, source) = Generate(Instructions + """

            [Instructions<Op>]
            public static partial class Script;
            """);

        await Assert.That(diagnostics).IsEmpty();
        await Assert.That(source).Contains("static partial class Script");
        await Assert.That(source).Contains("Program<global::Sample.Op, string> ask(string prompt)");
        await Assert.That(source).Contains("Program<global::Sample.Op, string> ask(global::Sample.Ask instruction)");
        await Assert.That(source).Contains("wait(int seconds = 1, string? @class = default)");
        await Assert.That(source).Contains("pure<T>(T value)");
        await Assert.That(source).Contains("string Handle(global::Sample.Ask instruction);");
        await Assert.That(source).Contains("ValueTask<global::LondoSpark.Func.Unit> HandleAsync(global::Sample.Wait instruction");
        await Assert.That(source).Contains("public static T Run<T>(this global::LondoSpark.Func.Program<global::Sample.Op, T> program, IHandler handler)");
        await Assert.That(source).Contains("RunAsync<T>(");
    }

    [Test]
    [Arguments("public partial class Script;")]
    [Arguments("public static class Script;")]
    [Arguments("public static partial class Script<T>;")]
    [Arguments("public static partial class Outer { [Instructions<Op>] public static partial class Script; }")]
    public async Task TheHolderMustBeATopLevelStaticPartialClass(string holder)
    {
        var declaration = holder.StartsWith("public static partial class Outer", StringComparison.Ordinal)
            ? holder
            : "[Instructions<Op>]\n" + holder;

        var (diagnostics, source) = Generate(Instructions + "\n" + declaration);

        await Assert.That(diagnostics.Select(static diagnostic => diagnostic.Id)).Contains("LSF0100");
        await Assert.That(source).IsEmpty();
    }

    [Test]
    public async Task TheInstructionSetMustBeAUnion()
    {
        var (diagnostics, source) = Generate("""
            using LondoSpark.Func;

            namespace Sample;

            public sealed class NotAUnion;

            [Instructions<NotAUnion>]
            public static partial class Script;
            """);

        await Assert.That(diagnostics.Select(static diagnostic => diagnostic.Id)).IsEquivalentTo(["LSF0101"]);
        await Assert.That(source).IsEmpty();
    }

    [Test]
    public async Task EveryInstructionMustNameOneResultType()
    {
        var (diagnostics, source) = Generate("""
            using LondoSpark.Func;

            namespace Sample;

            public sealed record Ask(string Prompt) : IInstruction<string>;
            public sealed record Shrug;

            [System.Runtime.CompilerServices.Union]
            public readonly struct Op
            {
                public Op(Ask value) => Value = value;
                public Op(Shrug value) => Value = value;
                public object? Value { get; }
            }

            [Instructions<Op>]
            public static partial class Script;
            """);

        var diagnostic = diagnostics.Single();
        await Assert.That(diagnostic.Id).IsEqualTo("LSF0102");
        await Assert.That(diagnostic.GetMessage(System.Globalization.CultureInfo.InvariantCulture)).Contains("Shrug");
        await Assert.That(source).IsEmpty();
    }

    private static (ImmutableArray<Diagnostic> Diagnostics, string Source) Generate(string source)
    {
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Append(typeof(Program<,>).Assembly.Location)
            .Distinct(StringComparer.Ordinal)
            .Select(static path => MetadataReference.CreateFromFile(path));

        var compilation = CSharpCompilation.Create(
            "GeneratorTest",
            [CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Preview))],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));

        var result = CSharpGeneratorDriver
            .Create(new InstructionsGenerator())
            .RunGenerators(compilation)
            .GetRunResult();

        return (result.Diagnostics, string.Concat(result.GeneratedTrees.Select(static tree => tree.ToString())));
    }
}
