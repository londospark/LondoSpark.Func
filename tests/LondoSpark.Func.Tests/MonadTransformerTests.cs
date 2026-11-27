using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

using LondoSpark.Func.Tests.Scripting;
using Microsoft.CodeAnalysis.Diagnostics;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;
using TUnit.FsCheck;

using static LondoSpark.Func.Prelude;
using static LondoSpark.Func.Tests.Scripting.Terminal;

namespace LondoSpark.Func.Tests;

/// <summary>A terminal script whose steps can fail: a program composed with a result.</summary>
[MonadTransformer]
public readonly partial record struct Session<T>(Program<ConsoleOp, Result<T, string>> Value);

/// <summary>A terminal script whose steps can be absent: a program composed with an option.</summary>
[MonadTransformer]
public readonly partial record struct Lookup<T>(Program<ConsoleOp, Option<T>> Value);

/// <summary>A value with a log, the outer monad of <see cref="Audited{T}"/>; the library knows nothing about it.</summary>
public sealed record Logged<T>(T Value, IReadOnlyList<string> Log);

/// <summary>Query operators for <see cref="Logged{T}"/>, so it can be an outer monad.</summary>
public static class LoggedQueries
{
    public static Logged<TOut> Select<T, TOut>(this Logged<T> source, Func<T, TOut> selector) =>
        new(selector(source.Value), source.Log);

    public static Logged<TOut> SelectMany<T, TNext, TOut>(this Logged<T> source, Func<T, Logged<TNext>> next, Func<T, TNext, TOut> project)
    {
        var second = next(source.Value);
        return new(project(source.Value, second.Value), [.. source.Log, .. second.Log]);
    }
}

/// <summary>A logged computation that can fail: an outer monad the generator does not know, so it declares Pure.</summary>
[MonadTransformer]
public readonly partial record struct Audited<T>(Logged<Result<T, string>> Value)
{
    private static Logged<TPure> Pure<TPure>(TPure value) => new(value, []);
}

/// <summary>A monad transformer binds both layers: a failure or absence skips every later step of the outer monad.</summary>
public sealed class MonadTransformerTests
{
    // ----- Program composed with Result -----

    [Test]
    public async Task StepsOfEveryShapeComposeInOneQuery()
    {
        var handler = new Scripted("Ada", "42");

        var outcome = (from name in ask("Name?")            // a program step that cannot fail
                       from age in AskNumber("Age?")        // a session step that may fail
                       from checkedAge in Adult(age)        // a plain result
                       from _ in tell($"{name} is {checkedAge}")
                       select $"{name}:{checkedAge}").Value.Run(handler);

        await Assert.That(outcome).IsEqualTo(Result.Return<string, string>("Ada:42"));
        await Assert.That(handler.Log).IsEquivalentTo(["ask Name?", "ask Age?", "tell Ada is 42"]);
    }

    [Test]
    public async Task AFailureSkipsEveryLaterInstruction()
    {
        var handler = new Scripted("Ada", "twelve");

        var outcome = (from name in ask("Name?")
                       from age in AskNumber("Age?")
                       from _ in tell($"{name} is {age}")
                       select age).Value.Run(handler);

        await Assert.That(outcome).IsEqualTo(Result.Failure<int, string>("'twelve' is not a number"));
        await Assert.That(handler.Log).IsEquivalentTo(["ask Name?", "ask Age?"]);
    }

    [Test]
    public async Task APlainResultFailureAlsoStopsTheProgram()
    {
        var handler = new Scripted("7");

        var outcome = (from age in AskNumber("Age?")
                       from adult in Adult(age)
                       from _ in tell("never")
                       select adult).Value.Run(handler);

        await Assert.That(outcome).IsEqualTo(Result.Failure<int, string>("7 is too young"));
        await Assert.That(handler.Log).IsEquivalentTo(["ask Age?"]);
    }

    [Test]
    public async Task QueriesCanStartFromAPlainResult()
    {
        var handler = new Scripted("30");

        var outcome = (from greeting in Result.Return<string, string>("hi")
                       from age in AskNumber(greeting)
                       select age).Value.Run(handler);

        await Assert.That(outcome).IsEqualTo(Result.Return<int, string>(30));
    }

    [Test]
    public async Task ConstructorsAndConversionsBuildEachCase()
    {
        Session<int> fromPrelude = failure("no");
        Session<int> fromSuccess = success(1);
        Session<int> fromResult = Result.Return<int, string>(2);
        var lifted = Session<int>.Lift(roll(6));
        Program<ConsoleOp, Result<int, string>> unwrapped = Session<int>.Return(3);

        var handler = new Scripted();
        await Assert.That(fromPrelude.Value.Run(handler)).IsEqualTo(Result.Failure<int, string>("no"));
        await Assert.That(fromSuccess.Value.Run(handler)).IsEqualTo(Result.Return<int, string>(1));
        await Assert.That(fromResult.Value.Run(handler)).IsEqualTo(Result.Return<int, string>(2));
        await Assert.That(lifted.Value.Run(handler)).IsEqualTo(Result.Return<int, string>(6));
        await Assert.That(unwrapped.Run(handler)).IsEqualTo(Result.Return<int, string>(3));
        await Assert.That(handler.Log).IsEquivalentTo(["roll 6"]);
    }

    [Test]
    public async Task ATransformedProgramIsStillAProgram()
    {
        var session = from age in AskNumber("Age?")
                      from _ in tell($"age {age}")
                      select age;

        // Step to the first instruction and answer it two ways, as with any program.
        _ = session.Value.TryGetValue(out Program<ConsoleOp, Result<int, string>>.Pending pending);
        var valid = pending.Next.Resume("40").Run(new Scripted());
        var invalid = pending.Next.Resume("forty").Run(new Scripted());

        await Assert.That(valid).IsEqualTo(Result.Return<int, string>(40));
        await Assert.That(invalid).IsEqualTo(Result.Failure<int, string>("'forty' is not a number"));
    }

    [Test]
    public async Task LiftingALongerProgramWrapsTheValueItFinishesWith()
    {
        var handler = new Scripted("a", "b");

        var outcome = Session<string>.Lift(from first in ask("1?")
                                           from second in ask("2?")
                                           select first + second).Value.Run(handler);

        await Assert.That(outcome).IsEqualTo(Result.Return<string, string>("ab"));
        await Assert.That(handler.Log).IsEquivalentTo(["ask 1?", "ask 2?"]);
    }

    [Test]
    public async Task ALiftedInstructionStillChecksTheAnswersType()
    {
        _ = Session<int>.Lift(roll(6)).Value.TryGetValue(out Program<ConsoleOp, Result<int, string>>.Pending pending);

        var exception = Assert.Throws<InvalidCastException>(() => _ = pending.Next.Resume("six"));

        await Assert.That(exception.Message).Contains("expects Int32");
    }

    // ----- Program composed with Option -----

    [Test]
    public async Task AnAbsentStepSkipsTheRest()
    {
        var found = (from name in ask("Name?")
                     from id in Known(name)
                     from _ in tell($"found {id}")
                     select id).Value.Run(new Scripted("Ada"));
        var handler = new Scripted("Bob");
        var missing = (from name in ask("Name?")
                       from id in Known(name)
                       from _ in tell($"found {id}")
                       select id).Value.Run(handler);

        await Assert.That(found).IsEqualTo(some(1));
        await Assert.That(missing).IsEqualTo(Option.Empty<int>());
        await Assert.That(handler.Log).IsEquivalentTo(["ask Name?"]);
        await Assert.That(Lookup<int>.None().Value.Run(handler)).IsEqualTo(Option.Empty<int>());
    }

    [Test]
    public async Task OptionStepsOfEveryShapeCompose()
    {
        var both = (from first in FindId("first?")
                    from second in FindId("second?")
                    select first + second).Value.Run(new Scripted("Ada", "Ada"));
        var bound = FindId("who?").Bind(id => FindId($"after {id}?")).Select(static id => id * 10).Value.Run(new Scripted("Ada", "Ada"));
        var lifted = Lookup<string>.Lift(from first in ask("1?") from second in ask("2?") select first + second).Value.Run(new Scripted("a", "b"));
        var handler = new Scripted("Bob", "Ada");
        var stopped = (from first in FindId("first?")
                       from second in FindId("second?")
                       select first + second).Value.Run(handler);

        await Assert.That(both).IsEqualTo(some(2));
        await Assert.That(bound).IsEqualTo(some(10));
        await Assert.That(lifted).IsEqualTo(some("ab"));
        await Assert.That(stopped).IsEqualTo(Option.Empty<int>());
        await Assert.That(handler.Log).IsEquivalentTo(["ask first?"]);
    }

    // ----- A user-defined outer monad -----

    [Test]
    public async Task AnyOuterMonadWithQueryOperatorsAndPureComposes()
    {
        var total = from a in Audit(2, "two")
                    from b in Audit(3, "three")
                    select a * b;
        var failed = from a in Audit(2, "two")
                     from b in (Audited<int>)failure("no three")
                     from c in Audit(4, "four")
                     select a + b + c;

        await Assert.That(total.Value.Value).IsEqualTo(Result.Return<int, string>(6));
        await Assert.That(total.Value.Log).IsEquivalentTo(["two", "three"]);
        await Assert.That(failed.Value.Value).IsEqualTo(Result.Failure<int, string>("no three"));
        await Assert.That(failed.Value.Log).IsEquivalentTo(["two"]);
    }

    // ----- Laws -----

    [Test, FsCheckProperty]
    public bool LeftIdentity(int value, string answer) =>
        Same(Session<int>.Return(value).Bind(a => Step(a, answer)), Step(value, answer), answer);

    [Test, FsCheckProperty]
    public bool RightIdentity(int value, string answer) =>
        Same(Step(value, answer).Bind(static b => Session<int>.Return(b)), Step(value, answer), answer);

    [Test, FsCheckProperty]
    public bool Associativity(int value, string answer)
    {
        var left = Step(value, answer).Bind(a => Step(a, answer)).Bind(b => Step(b, answer));
        var right = Step(value, answer).Bind(a => Step(a, answer).Bind(b => Step(b, answer)));
        return Same(left, right, answer);
    }

    // ----- Generator diagnostics -----

    [Test]
    [Arguments("[MonadTransformer] public partial struct S<T>;")]
    [Arguments("[MonadTransformer] public readonly record struct S<T>(Program<int, Result<T, string>> Value);")]
    [Arguments("[MonadTransformer] public readonly partial record struct S<A, B>(Program<int, Result<A, B>> Value);")]
    [Arguments("[MonadTransformer] public readonly partial record struct S<T>(Program<int, T> Value);")]
    [Arguments("[MonadTransformer] public readonly partial record struct S<T>(Program<T, Result<T, string>> Value);")]
    [Arguments("[MonadTransformer] public readonly partial record struct S<T>(Program<int, Result<T, T>> Value);")]
    [Arguments("public static class Outer { [MonadTransformer] public readonly partial record struct S<T>(Program<int, Result<T, string>> Value); }")]
    public async Task AMonadTransformerMustWrapOuterOfInnerOfItsTypeParameter(string declaration)
    {
        var (diagnostics, generated) = Generate(declaration);

        await Assert.That(diagnostics).IsEquivalentTo(["LSF0104"]);
        await Assert.That(generated).IsEmpty();
    }

    [Test]
    public async Task AnUnknownOuterMonadNeedsPure()
    {
        var (missing, _) = Generate("[MonadTransformer] public readonly partial record struct S<T>(System.Lazy<Result<T, string>> Value);");
        var (declared, generated) = Generate(
            "[MonadTransformer] public readonly partial record struct S<T>(System.Lazy<Result<T, string>> Value)"
            + " { private static System.Lazy<TPure> Pure<TPure>(TPure value) => new(value); }");

        await Assert.That(missing).IsEquivalentTo(["LSF0105"]);
        await Assert.That(declared).IsEmpty();
        await Assert.That(generated).Contains("public static S<TOut> SelectMany<T, TNext, TOut>(this global::System.Lazy<T> source");
    }

    [Test]
    public async Task AMonadTransformerHasNoZeroValue()
    {
        var compilation = Compile(
            "[MonadTransformer] public readonly partial record struct S<T>(Program<int, Result<T, string>> Value);"
            + " public static class Sample { public static S<int> Zero() => default; }");

        var diagnostics = await compilation
            .WithAnalyzers([new LondoSpark.Func.Analyzers.ResultHasNoDefaultValueAnalyzer()])
            .GetAnalyzerDiagnosticsAsync();

        await Assert.That(diagnostics.Select(static diagnostic => diagnostic.Id)).IsEquivalentTo(["LSF0001"]);
    }

    private static (string[] Diagnostics, string Generated) Generate(string declaration)
    {
        var result = Microsoft.CodeAnalysis.CSharp.CSharpGeneratorDriver
            .Create(new LondoSpark.Func.Generators.MonadTransformerGenerator())
            .RunGenerators(Compile(declaration))
            .GetRunResult();

        return (
            [.. result.Diagnostics.Select(static diagnostic => diagnostic.Id)],
            string.Concat(result.GeneratedTrees.Select(static tree => tree.ToString())));
    }

    private static Microsoft.CodeAnalysis.CSharp.CSharpCompilation Compile(string declaration) =>
        Microsoft.CodeAnalysis.CSharp.CSharpCompilation.Create(
            "MonadTransformerTest",
            [Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree.ParseText(
                "using LondoSpark.Func;\n" + declaration,
                new Microsoft.CodeAnalysis.CSharp.CSharpParseOptions(Microsoft.CodeAnalysis.CSharp.LanguageVersion.Preview))],
            ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
                .Split(System.IO.Path.PathSeparator)
                .Append(typeof(Result<,>).Assembly.Location)
                .Distinct(StringComparer.Ordinal)
                .Select(static path => Microsoft.CodeAnalysis.MetadataReference.CreateFromFile(path)),
            new Microsoft.CodeAnalysis.CSharp.CSharpCompilationOptions(Microsoft.CodeAnalysis.OutputKind.DynamicallyLinkedLibrary));

    // ----- Helpers -----

    private static Session<int> AskNumber(string prompt) =>
        from text in ask(prompt)
        from number in Parse(text)
        select number;

    private static Result<int, string> Parse(string text) =>
        int.TryParse(text, CultureInfo.InvariantCulture, out var number) ? success(number) : failure($"'{text}' is not a number");

    private static Result<int, string> Adult(int age) =>
        age >= 18 ? success(age) : failure($"{age} is too young");

    private static Option<int> Known(string name) =>
        name == "Ada" ? some(1) : none;

    private static Lookup<int> FindId(string prompt) =>
        from name in ask(prompt)
        from id in Known(name)
        select id;

    private static Audited<int> Audit(int value, string entry) =>
        new(new Logged<Result<int, string>>(Result.Return<int, string>(value), [entry]));

    // Asks a question, then fails when the answer is not a number; otherwise adds it to the value.
    private static Session<int> Step(int value, string answer) =>
        from number in AskNumber($"after {value}")
        select unchecked(value + number);

    private static bool Same(Session<int> left, Session<int> right, string answer)
    {
        var leftHandler = new Scripted(Enumerable.Repeat(answer ?? string.Empty, 4).ToArray());
        var rightHandler = new Scripted(Enumerable.Repeat(answer ?? string.Empty, 4).ToArray());
        return left.Value.Run(leftHandler).Equals(right.Value.Run(rightHandler))
            && leftHandler.Log.SequenceEqual(rightHandler.Log);
    }

    private sealed class Scripted(params string[] answers) : Terminal.IHandler
    {
        private readonly Queue<string> _answers = new(answers);

        public List<string> Log { get; } = [];

        public string Handle(Ask instruction)
        {
            Log.Add($"ask {instruction.Prompt}");
            return _answers.Dequeue();
        }

        public Unit Handle(Tell instruction)
        {
            Log.Add($"tell {instruction.Message}");
            return Unit.Value;
        }

        public int Handle(Roll instruction)
        {
            Log.Add($"roll {instruction.Sides}");
            return instruction.Sides;
        }
    }
}
