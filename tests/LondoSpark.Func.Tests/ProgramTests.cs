using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;

using LondoSpark.Func.Tests.Scripting;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;
using TUnit.FsCheck;

using static LondoSpark.Func.Program<LondoSpark.Func.Tests.Scripting.ConsoleOp, string>;
using static LondoSpark.Func.Tests.Scripting.Terminal;

namespace LondoSpark.Func.Tests;

/// <summary>Programs over a generated instruction set are data: built with query syntax, run by any interpreter, and steppable.</summary>
public sealed class ProgramTests
{
    private static Program<ConsoleOp, string> Greet =>
        from name in ask("What is your name?")
        from _ in tell($"Hello, {name}")
        select name;

    // ----- Running -----

    [Test]
    public async Task AQueryRunsItsInstructionsInOrder()
    {
        var handler = new Scripted("Ada");

        var name = Greet.Run(handler);

        await Assert.That(name).IsEqualTo("Ada");
        await Assert.That(handler.Log).IsEquivalentTo(["ask What is your name?", "tell Hello, Ada"]);
    }

    [Test]
    public async Task TheSameProgramRunsUnderDifferentInterpreters()
    {
        var first = new Scripted("Ada");
        var second = new Scripted("Grace");
        var program = Greet;

        await Assert.That(program.Run(first)).IsEqualTo("Ada");
        await Assert.That(program.Run(second)).IsEqualTo("Grace");
        await Assert.That(first.Log[1]).IsEqualTo("tell Hello, Ada");
        await Assert.That(second.Log[1]).IsEqualTo("tell Hello, Grace");
    }

    [Test]
    public async Task GeneratedConstructorsKeepDefaultArgumentsAndAcceptInstances()
    {
        var handler = new Scripted();

        var total = (from a in roll()
                     from b in roll(20)
                     from c in roll(new Roll(4))
                     select a + b + c).Run(handler);

        await Assert.That(total).IsEqualTo(30);
        await Assert.That(handler.Log).IsEquivalentTo(["roll 6", "roll 20", "roll 4"]);
    }

    [Test]
    public async Task PureProgramsRunNoInstructions()
    {
        var handler = new Scripted();

        await Assert.That(pure(5).Run(handler)).IsEqualTo(5);
        await Assert.That(pure("x").Select(static text => text + "y").Run(handler)).IsEqualTo("xy");
        await Assert.That(handler.Log).IsEmpty();
    }

    [Test]
    public async Task ProgramsRunAsynchronously()
    {
        var handler = new ScriptedAsync("Ada");

        var name = await Greet.RunAsync(handler);

        await Assert.That(name).IsEqualTo("Ada");
        await Assert.That(handler.Log).IsEquivalentTo(["ask What is your name?", "tell Hello, Ada"]);
    }

    [Test]
    public async Task AsynchronousRunsObserveCancellation()
    {
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await Greet.RunAsync(new ScriptedAsync("Ada"), cancelled.Token));
    }

    [Test]
    public async Task UntypedInterpretersAnswerWithObjects()
    {
        var log = new List<string>();

        var name = Greet.Run(instruction => instruction switch
        {
            Ask => "Ada",
            Tell(var message) => Record(log, message),
            Roll(var sides) => sides
        });
        var asynchronous = await Greet.RunAsync(static instruction => ValueTask.FromResult<object?>(instruction switch
        {
            Ask => "Grace",
            Tell => Unit.Value,
            Roll(var sides) => sides
        }));

        await Assert.That(name).IsEqualTo("Ada");
        await Assert.That(log).IsEquivalentTo(["Hello, Ada"]);
        await Assert.That(asynchronous).IsEqualTo("Grace");

        static object Record(List<string> log, string message)
        {
            log.Add(message);
            return Unit.Value;
        }
    }

    [Test]
    public async Task AnAnswerOfTheWrongTypeIsReported()
    {
        var exception = Assert.Throws<InvalidCastException>(() => Greet.Run(static _ => 42));

        await Assert.That(exception.Message).Contains("ConsoleOp");
        await Assert.That(exception.Message).Contains("String");
    }

    // ----- Stepping -----

    [Test]
    public async Task ProgramsAreUnionsThatCanBeSteppedByHand()
    {
        var program = Greet;

        var first = program switch
        {
            Pending(Ask(var prompt), _) => prompt,
            Pending(var other, _) => $"unexpected {other}",
            Done(var value) => $"finished {value}"
        };

        await Assert.That(first).IsEqualTo("What is your name?");
        await Assert.That(program.ToString()).StartsWith("Pending(Ask");
        await Assert.That(pure("x").ToString()).IsEqualTo("Done(x)");
    }

    [Test]
    public async Task AContinuationCanBeResumedMoreThanOnce()
    {
        var program = Greet;
        _ = program.TryGetValue(out Pending pending);

        var asAda = pending.Next.Resume("Ada");
        var asGrace = pending.Next.Resume("Grace");

        await Assert.That(asAda.ToString()).IsEqualTo("Pending(Tell { Message = Hello, Ada })");
        await Assert.That(asGrace.ToString()).IsEqualTo("Pending(Tell { Message = Hello, Grace })");
        await Assert.That(asAda.Run(new Scripted())).IsEqualTo("Ada");
        await Assert.That(asGrace.Run(new Scripted())).IsEqualTo("Grace");
    }

    [Test]
    public async Task LongRecursiveProgramsRunWithoutOverflowingTheStack()
    {
        var handler = new Scripted();

        var counted = Countdown(200_000).Run(handler);

        await Assert.That(counted).IsEqualTo(200_000);
        await Assert.That(handler.Log.Count).IsEqualTo(200_000);

        static Program<ConsoleOp, int> Countdown(int remaining) =>
            remaining == 0
                ? pure(0)
                : from _ in tell(remaining.ToString(CultureInfo.InvariantCulture))
                  from rest in Countdown(remaining - 1)
                  select rest + 1;
    }

#pragma warning disable LSF0001 // The zero value is the subject of this test.
    [Test]
    public async Task ADefaultProgramContainsNoCase()
    {
        Program<ConsoleOp, int> program = default;

        await Assert.That(program.HasValue).IsFalse();
        await Assert.That(program.ToString()).IsEqualTo("Program(default)");
        await AssertNoCase(() => program.Select(static value => value));
        await AssertNoCase(() => program.Bind(static value => pure(value)));
        await AssertNoCase(() => program.SelectMany(static value => pure(value), static (a, b) => a + b));
        await AssertNoCase(() => program.Run(static _ => null));
        await AssertNoCase(() => program.Run(new Scripted()));
        await AssertNoCase(() => program.Value);
    }
#pragma warning restore LSF0001

    // ----- Laws -----

    [Test, FsCheckProperty]
    public bool LeftIdentity(int value, int sides) =>
        Same(pure(value).Bind(a => Step(a, sides)), Step(value, sides));

    [Test, FsCheckProperty]
    public bool RightIdentity(int value, int sides) =>
        Same(Step(value, sides).Bind(static b => pure(b)), Step(value, sides));

    [Test, FsCheckProperty]
    public bool Associativity(int value, int sides, int more)
    {
        var program = Step(value, sides);
        var left = program.Bind(a => Step(a, more)).Bind(b => Step(b, sides));
        var right = program.Bind(a => Step(a, more).Bind(b => Step(b, sides)));
        return Same(left, right);
    }

    [Test, FsCheckProperty]
    public bool SelectManyMatchesBindThenSelect(int value, int sides)
    {
        var query = Step(value, sides).SelectMany(a => Step(a, sides), static (a, b) => a - b);
        var fluent = Step(value, sides).Bind(a => Step(a, sides).Select(b => a - b));
        return Same(query, fluent);
    }

    private static Program<ConsoleOp, int> Step(int value, int sides) =>
        from rolled in roll(Math.Abs(sides % 100) + 1)
        from _ in tell(value.ToString(CultureInfo.InvariantCulture))
        select unchecked(value + rolled);

    private static bool Same<T>(Program<ConsoleOp, T> left, Program<ConsoleOp, T> right)
    {
        var leftHandler = new Scripted();
        var rightHandler = new Scripted();
        return EqualityComparer<T>.Default.Equals(left.Run(leftHandler), right.Run(rightHandler))
            && string.Join("|", leftHandler.Log) == string.Join("|", rightHandler.Log);
    }

    private static async Task AssertNoCase<T>(Func<T> operation)
    {
        var exception = Assert.Throws<InvalidOperationException>(() => _ = operation());
        await Assert.That(exception.Message).Contains("no case");
    }

    // ----- Interpreters -----

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

    private sealed class ScriptedAsync(params string[] answers) : Terminal.IAsyncHandler
    {
        private readonly Queue<string> _answers = new(answers);

        public List<string> Log { get; } = [];

        public async ValueTask<string> HandleAsync(Ask instruction, CancellationToken cancellationToken)
        {
            await Task.Yield();
            Log.Add($"ask {instruction.Prompt}");
            return _answers.Dequeue();
        }

        public async ValueTask<Unit> HandleAsync(Tell instruction, CancellationToken cancellationToken)
        {
            await Task.Delay(1, cancellationToken);
            Log.Add($"tell {instruction.Message}");
            return Unit.Value;
        }

        public ValueTask<int> HandleAsync(Roll instruction, CancellationToken cancellationToken) =>
            ValueTask.FromResult(instruction.Sides);
    }
}
