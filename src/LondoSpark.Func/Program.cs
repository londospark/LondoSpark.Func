using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace LondoSpark.Func;

/// <summary>A program over the instruction set <typeparamref name="TOp"/>: finished with a value, or waiting on one instruction and the code after it.</summary>
/// <typeparam name="TOp">The instruction set, usually a domain union such as <c>union ConsoleOp(Ask, Tell);</c>.</typeparam>
/// <typeparam name="T">The value the program finishes with.</typeparam>
/// <remarks>
/// <para>
/// This is the freer (operational) monad: the free monad over any instruction set, with no functor to write. A program
/// is data, so the same program can be run by a real interpreter, a scripted test interpreter, or a tracer, and can be
/// stepped one instruction at a time. Query syntax is its do-notation:
/// <code>
/// var greet = from name in ask("What is your name?")
///             from _ in tell($"Hello, {name}")
///             select name;
/// </code>
/// Declare the instruction set with <see cref="InstructionsAttribute{TOp}"/> and the generator writes the lowercase
/// constructors (<c>ask</c>, <c>tell</c>) and typed handler interfaces whose <c>Run</c> and <c>RunAsync</c> switch
/// exhaustively over the instructions.
/// </para>
/// <para>
/// A program is a union of <see cref="Done"/> and <see cref="Pending"/>, so an interpreter can also be a plain loop over
/// a switch. <see cref="Pending.Next"/> may be resumed any number of times, which makes replaying, forking, and
/// backtracking possible. Finished programs do not allocate; each pending step allocates its continuation.
/// </para>
/// <para>
/// Because this is a struct, <see langword="default"/> contains no case: <see cref="HasValue"/> is
/// <see langword="false"/> and every operation throws <see cref="InvalidOperationException"/>.
/// </para>
/// </remarks>
[Union]
public readonly struct Program<TOp, T> : IUnion
{
    private const byte DoneTag = 1;
    private const byte PendingTag = 2;

    private readonly T _value;
    private readonly TOp _instruction;
    private readonly Continuation<TOp, T>? _next;
    private readonly byte _tag;

    /// <summary>Creates a finished program.</summary>
    /// <param name="value">The finished case.</param>
    public Program(Done value)
    {
        _value = value.Value;
        _instruction = default!;
        _next = null;
        _tag = DoneTag;
    }

    /// <summary>Creates a program waiting on an instruction.</summary>
    /// <param name="value">The pending case.</param>
    public Program(Pending value)
    {
        ArgumentNullException.ThrowIfNull(value.Next);
        _value = default!;
        _instruction = value.Instruction;
        _next = value.Next;
        _tag = PendingTag;
    }

    /// <summary>Gets the boxed active case; the union contract requires this <c>object</c> property, so reading it allocates.</summary>
    /// <exception cref="InvalidOperationException">The program is a zero value with no case.</exception>
    public object Value =>
        _tag switch
        {
            DoneTag => new Done(_value),
            PendingTag => new Pending(_instruction, _next!),
            _ => throw NoCase.Program()
        };

    /// <summary>Gets a value indicating whether this program contains a case; <see langword="false"/> only for a zero value.</summary>
    public bool HasValue => _tag != 0;

    /// <summary>Gets the finished case when this program has finished.</summary>
    /// <param name="value">Receives the finished case, or its default value.</param>
    /// <returns><see langword="true"/> when the program has finished.</returns>
    public bool TryGetValue(out Done value)
    {
        value = new Done(_value);
        return _tag == DoneTag;
    }

    /// <summary>Gets the pending case when this program is waiting on an instruction.</summary>
    /// <param name="value">Receives the pending case, or its default value.</param>
    /// <returns><see langword="true"/> when the program is waiting on an instruction.</returns>
    public bool TryGetValue(out Pending value)
    {
        value = new Pending(_instruction, _next!);
        return _tag == PendingTag;
    }

    /// <summary>Renders the active case.</summary>
    /// <returns><c>Done(value)</c>, <c>Pending(instruction)</c>, or <c>Program(default)</c> for a zero value. An instruction set that is a union shows its active case.</returns>
    public override string ToString() =>
        _tag switch
        {
            DoneTag => $"Done({_value})",
            PendingTag => $"Pending({(_instruction is IUnion union ? union.Value : _instruction)})",
            _ => "Program(default)"
        };

    /// <summary>The finished case, holding the program's value.</summary>
    /// <param name="Value">The value the program finished with.</param>
    public readonly record struct Done(T Value);

    /// <summary>The waiting case: one instruction for an interpreter, and the rest of the program.</summary>
    /// <param name="Instruction">The instruction to interpret.</param>
    /// <param name="Next">Resumes the program with the instruction's result.</param>
    public readonly record struct Pending(TOp Instruction, Continuation<TOp, T> Next);
}

/// <summary>The rest of a program after an instruction, resumed with that instruction's result.</summary>
/// <typeparam name="TOp">The instruction set.</typeparam>
/// <typeparam name="T">The value the program finishes with.</typeparam>
/// <remarks>
/// <para>A continuation holds no mutable state, so it can be resumed more than once, with different results.</para>
/// <para>
/// It is a sequence of steps that <see cref="Resume{TResult}"/> runs in a loop rather than by recursion, so a program
/// may run any number of instructions, including recursive loops that compute after their recursive call, in constant
/// stack space. Values pass between steps as <see cref="object"/>, so each step boxes a value-typed result.
/// </para>
/// </remarks>
public sealed class Continuation<TOp, T>
{
    // One continuation per instruction set and result type serves every lifted instruction: it only checks the answer.
    internal static readonly Continuation<TOp, T> Lifted = new(new LiftedStep<TOp, T>());

    private readonly Steps<TOp> _steps;

    internal Continuation(Steps<TOp> steps) => _steps = steps;

    internal Steps<TOp> Steps => _steps;

    /// <summary>Resumes the program with the result of its pending instruction.</summary>
    /// <typeparam name="TResult">The type the interpreter answered with; it should match the instruction's result type.</typeparam>
    /// <param name="result">The instruction's result.</param>
    /// <returns>The rest of the program, finished or waiting on its next instruction.</returns>
    /// <exception cref="InvalidCastException"><paramref name="result"/> is not of the type the instruction promised.</exception>
    public Program<TOp, T> Resume<TResult>(TResult result)
    {
        var value = (object?)result;
        var steps = (Steps<TOp>?)_steps;
        while (steps is not null)
        {
            var step = Steps<TOp>.Uncons(steps, out var rest);
            var next = step.Apply(value);
            if (next.IsDone)
            {
                value = next.Value;
                steps = rest;
            }
            else
            {
                return new Program<TOp, T>.Pending(
                    next.Instruction,
                    new Continuation<TOp, T>(rest is null ? next.Steps! : new Both<TOp>(next.Steps!, rest)));
            }
        }

        return new Program<TOp, T>.Done((T)value!);
    }

    // Appends a step that turns this continuation's value into the next program's.
    internal Continuation<TOp, TOut> Then<TOut>(Step<TOp> step) =>
        new(new Both<TOp>(_steps, step));
}

/// <summary>Builds programs.</summary>
public static class Program
{
    /// <summary>Creates a program that finishes with <paramref name="value"/> without running any instruction.</summary>
    /// <typeparam name="TOp">The instruction set.</typeparam>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="value">The value.</param>
    /// <returns>A finished program.</returns>
    public static Program<TOp, T> Return<TOp, T>(T value) =>
        new Program<TOp, T>.Done(value);

    /// <summary>Creates a program that runs one instruction and finishes with its result.</summary>
    /// <typeparam name="TOp">The instruction set.</typeparam>
    /// <typeparam name="TResult">The type of the instruction's result.</typeparam>
    /// <param name="instruction">The instruction.</param>
    /// <returns>A program waiting on <paramref name="instruction"/>.</returns>
    /// <remarks>The generator writes one typed constructor per instruction on top of this, so code rarely calls it.</remarks>
    public static Program<TOp, TResult> Lift<TOp, TResult>(TOp instruction) =>
        new Program<TOp, TResult>.Pending(instruction, Continuation<TOp, TResult>.Lifted);
}

/// <summary>Composes and interprets <see cref="Program{TOp, T}"/>.</summary>
public static class ProgramExtensions
{
    extension<TOp, T>(Program<TOp, T> program)
    {
        /// <summary>Transforms the value the program finishes with.</summary>
        /// <typeparam name="TOut">The transformed type.</typeparam>
        /// <param name="selector">The transformation.</param>
        /// <returns>A program that runs the same instructions and finishes with the transformed value.</returns>
        public Program<TOp, TOut> Select<TOut>(Func<T, TOut> selector) =>
            program switch
            {
                Program<TOp, T>.Done(var value) => new Program<TOp, TOut>.Done(selector(value)),
                Program<TOp, T>.Pending(var instruction, var next) =>
                    new Program<TOp, TOut>.Pending(instruction, next.Then<TOut>(new Mapped<TOp, T, TOut>(selector))),
                null => throw NoCase.Program()
            };

        /// <summary>Runs this program, then the program <paramref name="binder"/> builds from its value.</summary>
        /// <typeparam name="TOut">The value type of the second program.</typeparam>
        /// <param name="binder">Builds the rest of the program.</param>
        /// <returns>The sequenced program.</returns>
        public Program<TOp, TOut> Bind<TOut>(Func<T, Program<TOp, TOut>> binder) =>
            program switch
            {
                Program<TOp, T>.Done(var value) => binder(value),
                Program<TOp, T>.Pending(var instruction, var next) =>
                    new Program<TOp, TOut>.Pending(instruction, next.Then<TOut>(new Bound<TOp, T, TOut>(binder))),
                null => throw NoCase.Program()
            };

        /// <summary>Provides query-syntax composition; use <c>Bind</c> for fluent code.</summary>
        /// <typeparam name="TOut">The value type of the second program.</typeparam>
        /// <param name="selector">Builds the rest of the program.</param>
        /// <returns>The sequenced program.</returns>
        public Program<TOp, TOut> SelectMany<TOut>(Func<T, Program<TOp, TOut>> selector) =>
            program.Bind(selector);

        /// <summary>Sequences two programs and projects both values; this is what each <c>from</c> clause compiles to.</summary>
        /// <typeparam name="TNext">The value type of the second program.</typeparam>
        /// <typeparam name="TResult">The projected type.</typeparam>
        /// <param name="next">Builds the second program from the first value.</param>
        /// <param name="project">Combines both values.</param>
        /// <returns>The sequenced, projected program.</returns>
        public Program<TOp, TResult> SelectMany<TNext, TResult>(
            Func<T, Program<TOp, TNext>> next,
            Func<T, TNext, TResult> project) =>
            program switch
            {
                Program<TOp, T>.Done(var value) => Projected<TOp, T, TNext, TResult>.Project(value, next(value), project),
                Program<TOp, T>.Pending(var instruction, var rest) =>
                    new Program<TOp, TResult>.Pending(instruction, rest.Then<TResult>(new Sequenced<TOp, T, TNext, TResult>(next, project))),
                null => throw NoCase.Program()
            };

        /// <summary>Runs the program, answering each instruction with an untyped interpreter.</summary>
        /// <param name="interpreter">Answers an instruction with its result, boxed.</param>
        /// <returns>The value the program finishes with.</returns>
        /// <exception cref="InvalidCastException">The interpreter answered an instruction with the wrong type.</exception>
        /// <remarks>Prefer the generated <c>Run</c>, whose handler is typed per instruction and does not box.</remarks>
        public T Run(Func<TOp, object?> interpreter)
        {
            ArgumentNullException.ThrowIfNull(interpreter);
            var current = program;
            while (true)
            {
                switch (current)
                {
                    case Program<TOp, T>.Done(var value):
                        return value;
                    case Program<TOp, T>.Pending(var instruction, var next):
                        current = next.Resume(interpreter(instruction));
                        break;
                    case null:
                        throw NoCase.Program();
                }
            }
        }

        /// <summary>Runs the program, answering each instruction with an untyped asynchronous interpreter.</summary>
        /// <param name="interpreter">Answers an instruction with its result, boxed.</param>
        /// <returns>The value the program finishes with.</returns>
        /// <exception cref="InvalidCastException">The interpreter answered an instruction with the wrong type.</exception>
        /// <remarks>Prefer the generated <c>RunAsync</c>, whose handler is typed per instruction and does not box.</remarks>
        public async ValueTask<T> RunAsync(Func<TOp, ValueTask<object?>> interpreter)
        {
            ArgumentNullException.ThrowIfNull(interpreter);
            var current = program;
            while (true)
            {
                switch (current)
                {
                    case Program<TOp, T>.Done(var value):
                        return value;
                    case Program<TOp, T>.Pending(var instruction, var next):
                        current = next.Resume(await interpreter(instruction).ConfigureAwait(false));
                        break;
                    case null:
                        throw NoCase.Program();
                }
            }
        }
    }
}

// ----- Steps -----
//
// A continuation is a sequence of steps over erased values. Each step turns the previous step's value into the next
// program: finished, so its value feeds the following step, or pending, so its own steps run before the remaining
// ones. Appending is O(1) (a Both node); taking the first step re-associates left-nested nodes to the right as it
// goes, so every step is taken in amortised O(1) and nothing recurses.

internal abstract class Steps<TOp>
{
    internal static Step<TOp> Uncons(Steps<TOp> steps, out Steps<TOp>? rest)
    {
        rest = null;
        while (steps is Both<TOp> both)
        {
            rest = rest is null ? both.Right : new Both<TOp>(both.Right, rest);
            steps = both.Left;
        }

        return (Step<TOp>)steps;
    }
}

internal sealed class Both<TOp>(Steps<TOp> left, Steps<TOp> right) : Steps<TOp>
{
    internal Steps<TOp> Left { get; } = left;

    internal Steps<TOp> Right { get; } = right;
}

internal abstract class Step<TOp> : Steps<TOp>
{
    internal abstract Erased<TOp> Apply(object? value);

    // Erases a typed program so the loop in Continuation.Resume can carry on with it.
    protected static Erased<TOp> Erase<T>(Program<TOp, T> program) =>
        program switch
        {
            Program<TOp, T>.Done(var value) => Erased<TOp>.Done(value),
            Program<TOp, T>.Pending(var instruction, var next) => Erased<TOp>.Pending(instruction, next.Steps),
            null => throw NoCase.Program()
        };
}

internal readonly struct Erased<TOp>
{
    private Erased(bool isDone, object? value, TOp instruction, Steps<TOp>? steps)
    {
        IsDone = isDone;
        Value = value;
        Instruction = instruction;
        Steps = steps;
    }

    internal bool IsDone { get; }

    internal object? Value { get; }

    internal TOp Instruction { get; }

    internal Steps<TOp>? Steps { get; }

    internal static Erased<TOp> Done(object? value) => new(true, value, default!, null);

    internal static Erased<TOp> Pending(TOp instruction, Steps<TOp> steps) => new(false, null, instruction, steps);
}

// The first step of a lifted instruction: checks the interpreter answered with the promised type.
internal sealed class LiftedStep<TOp, TResult> : Step<TOp>
{
    internal override Erased<TOp> Apply(object? value) => Erased<TOp>.Done(Checked(value));

    internal static object? Checked(object? value) =>
        value is TResult || (value is null && default(TResult) is null)
            ? value
            : throw new InvalidCastException(
                $"An interpreter of {typeof(TOp).Name} answered with {value?.GetType().Name ?? "null"}, but the instruction expects {typeof(TResult).Name}.");
}

internal sealed class Mapped<TOp, T, TOut>(Func<T, TOut> selector) : Step<TOp>
{
    internal override Erased<TOp> Apply(object? value) =>
        Erased<TOp>.Done(selector((T)value!));
}

internal sealed class Bound<TOp, T, TOut>(Func<T, Program<TOp, TOut>> binder) : Step<TOp>
{
    internal override Erased<TOp> Apply(object? value) =>
        Erase(binder((T)value!));
}

internal sealed class Sequenced<TOp, T, TNext, TResult>(
    Func<T, Program<TOp, TNext>> next,
    Func<T, TNext, TResult> project) : Step<TOp>
{
    internal override Erased<TOp> Apply(object? value)
    {
        var first = (T)value!;
        return Erase(Projected<TOp, T, TNext, TResult>.Project(first, next(first), project));
    }
}

// The second half of a from clause once the first value is known.
internal sealed class Projected<TOp, T, TNext, TResult>(T first, Func<T, TNext, TResult> project) : Step<TOp>
{
    internal override Erased<TOp> Apply(object? value) =>
        Erased<TOp>.Done(project(first, (TNext)value!));

    internal static Program<TOp, TResult> Project(T first, Program<TOp, TNext> second, Func<T, TNext, TResult> project) =>
        second switch
        {
            Program<TOp, TNext>.Done(var value) => new Program<TOp, TResult>.Done(project(first, value)),
            Program<TOp, TNext>.Pending(var instruction, var rest) =>
                new Program<TOp, TResult>.Pending(instruction, rest.Then<TResult>(new Projected<TOp, T, TNext, TResult>(first, project))),
            null => throw NoCase.Program()
        };
}
