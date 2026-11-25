using System;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using LondoSpark.Func.Analyzers;
using LondoSpark.Func.Generators;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace LondoSpark.Func.Tests;

/// <summary>LSF0003, LSF0004, LSF0005, and the async result generator's LSF0103: awaits that cannot short-circuit, catch clauses that swallow a short-circuit, and synchronous methods that would suspend.</summary>
public sealed class ShortCircuitAwaitAnalyzerTests
{
    private const string Header = """
        using System;
        using System.Runtime.CompilerServices;
        using System.Threading.Tasks;
        using LondoSpark.Func;
        using static LondoSpark.Func.Prelude;

        [AsyncResult<string>]
        public readonly partial struct Parsed<T>;

        [AsyncResult<int>]
        public readonly partial struct Coded<T>;

        public static class Sample
        {
            static Result<int, string> Parse(string text) => failure(text);
            static Option<int> Find(int id) => Option.Empty<int>();

        """;

    [Test]
    [Arguments("static async Option<int> M() => await Find(1);")]
    [Arguments("static async Option<int> M() { await none; return 1; }")]
    [Arguments("static async Parsed<int> M() => await Parse(\"x\");")]
    [Arguments("static async Parsed<int> M() { await failure(\"no\"); return 1; }")]
    [Arguments("static async Parsed<int> M() { Parsed<int> other = success(1); return await other; }")]
    [Arguments("[AsyncMethodBuilder(typeof(ShortCircuitMethodBuilder<>))] static async ValueTask<Option<int>> M() { await Task.Yield(); return some(await Find(1)); }")]
    [Arguments("[AsyncMethodBuilder(typeof(ShortCircuitMethodBuilder<>))] static async ValueTask<Result<int, string>> M() { await Task.Yield(); return success(await Parse(\"x\")); }")]
    [Arguments("[AsyncMethodBuilder(typeof(ShortCircuitMethodBuilder<>))] static async ValueTask<Parsed<int>> M() { await Task.Yield(); return success(await Parse(\"x\")); }")]
    [Arguments("static async Task<Option<int>> M() { await Task.Yield(); return Find(1); }")]
    public async Task AwaitsThatTheMethodCanShortCircuitAreAccepted(string method)
    {
        var diagnostics = await AnalyzeAsync(method);

        await Assert.That(diagnostics.Select(static diagnostic => diagnostic.Id)).IsEmpty();
    }

    [Test]
    [Arguments("static async Task<int> M() => await Find(1);")]
    [Arguments("static async Task M() { await none; }")]
    [Arguments("static async Task<int> M() => await Parse(\"x\");")]
    [Arguments("static async Option<int> M() => await Parse(\"x\");")]
    [Arguments("static async Parsed<int> M() => await Find(1);")]
    [Arguments("static async Coded<int> M() => await Parse(\"x\");")]
    [Arguments("static async ValueTask<Option<int>> M() { var found = await Find(1); return some(found); }")]
    [Arguments("static void M() { Func<Task<int>> f = async () => await Find(1); }")]
    public async Task AwaitsThatCannotShortCircuitTheMethodAreErrors(string method)
    {
        var diagnostics = await AnalyzeAsync(method);

        await Assert.That(diagnostics.Select(static diagnostic => diagnostic.Id)).IsEquivalentTo(["LSF0003"]);
    }

    [Test]
    [Arguments("catch (Exception) { return 0; }")]
    [Arguments("catch { return 0; }")]
    [Arguments("catch (ShortCircuitException) { return 0; }")]
    public async Task CatchClausesThatInterceptAShortCircuitAreReported(string clause)
    {
        var diagnostics = await AnalyzeAsync($"static async Option<int> M() {{ try {{ return await Find(1); }} {clause} }}");

        var diagnostic = diagnostics.Single();
        await Assert.That(diagnostic.Id).IsEqualTo("LSF0004");
        await Assert.That(diagnostic.Severity).IsEqualTo(DiagnosticSeverity.Warning);
    }

    [Test]
    [Arguments("catch (Exception e) when (e is not ShortCircuitException) { return 0; }")]
    [Arguments("catch (FormatException) { return 0; }")]
    [Arguments("finally { }")]
    public async Task CatchClausesThatLetAShortCircuitThroughAreAccepted(string clause)
    {
        var diagnostics = await AnalyzeAsync($"static async Option<int> M() {{ try {{ return await Find(1); }} {clause} }}");

        await Assert.That(diagnostics).IsEmpty();
    }

    [Test]
    [Arguments("static async Option<int> M() { await Task.Yield(); return 1; }")]
    [Arguments("static async Parsed<int> M() => await Task.FromResult(1);")]
    [Arguments("static async Option<int> M() => await await Task.FromResult(Find(1));")]
    public async Task SynchronousMethodsThatAwaitTasksAreErrors(string method)
    {
        var diagnostics = await AnalyzeAsync(method);

        await Assert.That(diagnostics.Select(static diagnostic => diagnostic.Id)).Contains("LSF0005");
    }

    [Test]
    public async Task GeneratedResultTypesHaveNoZeroValue()
    {
        var diagnostics = await AnalyzeAsync("static Parsed<int> M() => default;", new ResultHasNoDefaultValueAnalyzer());

        await Assert.That(diagnostics.Select(static diagnostic => diagnostic.Id)).IsEquivalentTo(["LSF0001"]);
    }

    [Test]
    [Arguments("[AsyncResult<string>] public partial struct Pair<A, B>;")]
    [Arguments("[AsyncResult<string>] public struct Plain<T>;")]
    [Arguments("public static class Outer { [AsyncResult<string>] public partial struct Inner<T>; }")]
    public async Task AnAsyncResultTypeMustBeATopLevelPartialStructWithOneTypeParameter(string declaration)
    {
        var compilation = CSharpCompilation.Create(
            "GeneratorTest",
            [CSharpSyntaxTree.ParseText("using LondoSpark.Func;\n" + declaration, new CSharpParseOptions(LanguageVersion.Preview))],
            References());

        var result = CSharpGeneratorDriver.Create(new AsyncResultGenerator()).RunGenerators(compilation).GetRunResult();

        await Assert.That(result.Diagnostics.Select(static diagnostic => diagnostic.Id)).IsEquivalentTo(["LSF0103"]);
        await Assert.That(result.GeneratedTrees).IsEmpty();
    }

    private static System.Collections.Generic.IEnumerable<MetadataReference> References() =>
        ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Append(typeof(Result<,>).Assembly.Location)
            .Distinct(StringComparer.Ordinal)
            .Select(static path => MetadataReference.CreateFromFile(path));

    private static async Task<ImmutableArray<Diagnostic>> AnalyzeAsync(string method, DiagnosticAnalyzer? analyzer = null)
    {
        var references = References();
        var parseOptions = new CSharpParseOptions(LanguageVersion.Preview);
        var compilation = CSharpCompilation.Create(
            "AnalyzerTest",
            [CSharpSyntaxTree.ParseText(Header + "    " + method + "\n}\n", parseOptions)],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));

        CSharpGeneratorDriver
            .Create([new AsyncResultGenerator().AsSourceGenerator()], parseOptions: parseOptions)
            .RunGeneratorsAndUpdateCompilation(compilation, out var generated, out _);

        var errors = generated.GetDiagnostics().Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).ToArray();
        if (errors.Length > 0)
            throw new InvalidOperationException(string.Join(Environment.NewLine, errors.Select(static error => error.ToString())));

        return await generated
            .WithAnalyzers([analyzer ?? new ShortCircuitAwaitAnalyzer()])
            .GetAnalyzerDiagnosticsAsync();
    }
}
