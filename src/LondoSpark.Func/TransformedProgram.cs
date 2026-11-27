using System;
using System.ComponentModel;

namespace LondoSpark.Func;

/// <summary>The operations of a <see cref="MonadTransformerAttribute"/> type over <see cref="Program{TOp, T}"/>, built into the program's own steps.</summary>
/// <remarks>
/// <para>
/// The generator calls these for a transformer whose outer monad is a program, so code rarely does. Written against
/// <see cref="Program{TOp, T}"/>'s query operators, every bind would hand the program a closure that checks the inner
/// value and then calls the next step: a closure object and a delegate on top of the program's own step. These
/// operations add one step that holds the caller's delegates and does the check itself, and <c>Lift</c> of a single
/// instruction reuses one continuation per instruction set and type, so it allocates nothing.
/// </para>
/// <para>A failure or absence skips the rest of the program's steps: each one passes it on without running its delegate.</para>
/// </remarks>
[EditorBrowsable(EditorBrowsableState.Never)]
public static class TransformedProgram
{
    // ----- Result -----

    /// <summary>Lifts a program that cannot fail into one whose value is a successful result.</summary>
    /// <typeparam name="TOp">The instruction set.</typeparam>
    /// <typeparam name="T">The value type.</typeparam>
    /// <typeparam name="TFailure">The failure type.</typeparam>
    /// <param name="step">The program.</param>
    /// <returns>The same instructions, finishing with a successful result.</returns>
    public static Program<TOp, Result<T, TFailure>> Lift<TOp, T, TFailure>(Program<TOp, T> step) =>
        step switch
        {
            Program<TOp, T>.Done(var value) => new Program<TOp, Result<T, TFailure>>.Done(Result.Return<T, TFailure>(value)),
            Program<TOp, T>.Pending(var instruction, var next) => new Program<TOp, Result<T, TFailure>>.Pending(
                instruction,
                ReferenceEquals(next, Continuation<TOp, T>.Lifted)
                    ? LiftedResult<TOp, T, TFailure>.Continuation
                    : next.Then<Result<T, TFailure>>(ResultSucceeded<TOp, T, TFailure>.Instance)),
            null => throw NoCase.Program()
        };

    /// <summary>Transforms a successful value, carrying a failure through.</summary>
    /// <typeparam name="TOp">The instruction set.</typeparam>
    /// <typeparam name="T">The value type.</typeparam>
    /// <typeparam name="TFailure">The failure type.</typeparam>
    /// <typeparam name="TOut">The transformed type.</typeparam>
    /// <param name="program">The program.</param>
    /// <param name="selector">The transformation.</param>
    /// <returns>The transformed program.</returns>
    public static Program<TOp, Result<TOut, TFailure>> Select<TOp, T, TFailure, TOut>(
        Program<TOp, Result<T, TFailure>> program,
        Func<T, TOut> selector)
    {
        ArgumentNullException.ThrowIfNull(selector);
        return program switch
        {
            Program<TOp, Result<T, TFailure>>.Done(var inner) => new Program<TOp, Result<TOut, TFailure>>.Done(ResultSteps.Map(inner, selector)),
            Program<TOp, Result<T, TFailure>>.Pending(var instruction, var next) =>
                new Program<TOp, Result<TOut, TFailure>>.Pending(instruction, next.Then<Result<TOut, TFailure>>(new ResultMapped<TOp, T, TFailure, TOut>(selector))),
            null => throw NoCase.Program()
        };
    }

    /// <summary>Continues with the step <paramref name="binder"/> builds from a successful value, or skips it after a failure.</summary>
    /// <typeparam name="TOp">The instruction set.</typeparam>
    /// <typeparam name="T">The value type.</typeparam>
    /// <typeparam name="TFailure">The failure type.</typeparam>
    /// <typeparam name="TOut">The value type of the next step.</typeparam>
    /// <typeparam name="TNext">The transformer type of the next step.</typeparam>
    /// <param name="program">The program.</param>
    /// <param name="binder">Builds the next step.</param>
    /// <returns>The sequenced program.</returns>
    public static Program<TOp, Result<TOut, TFailure>> Bind<TOp, T, TFailure, TOut, TNext>(
        Program<TOp, Result<T, TFailure>> program,
        Func<T, TNext> binder)
        where TNext : IMonadTransformer<Program<TOp, Result<TOut, TFailure>>>
    {
        ArgumentNullException.ThrowIfNull(binder);
        return program switch
        {
            Program<TOp, Result<T, TFailure>>.Done(var inner) => inner.TryGetValue(out Result<T, TFailure>.Success success)
                ? binder(success.Value).Wrapped
                : new Program<TOp, Result<TOut, TFailure>>.Done(ResultSteps.Propagate<T, TOut, TFailure>(inner)),
            Program<TOp, Result<T, TFailure>>.Pending(var instruction, var next) =>
                new Program<TOp, Result<TOut, TFailure>>.Pending(instruction, next.Then<Result<TOut, TFailure>>(new ResultBound<TOp, T, TFailure, TOut, TNext>(binder))),
            null => throw NoCase.Program()
        };
    }

    /// <summary>Sequences two steps of a transformer type and projects both values; this is what each <c>from</c> clause compiles to.</summary>
    /// <typeparam name="TOp">The instruction set.</typeparam>
    /// <typeparam name="T">The value type.</typeparam>
    /// <typeparam name="TFailure">The failure type.</typeparam>
    /// <typeparam name="TSecond">The value type of the second step.</typeparam>
    /// <typeparam name="TOut">The projected type.</typeparam>
    /// <typeparam name="TNext">The transformer type of the second step.</typeparam>
    /// <param name="program">The program.</param>
    /// <param name="next">Builds the second step from the first value.</param>
    /// <param name="project">Combines both values.</param>
    /// <returns>The sequenced, projected program.</returns>
    public static Program<TOp, Result<TOut, TFailure>> SelectMany<TOp, T, TFailure, TSecond, TOut, TNext>(
        Program<TOp, Result<T, TFailure>> program,
        Func<T, TNext> next,
        Func<T, TSecond, TOut> project)
        where TNext : IMonadTransformer<Program<TOp, Result<TSecond, TFailure>>>
    {
        ArgumentNullException.ThrowIfNull(next);
        ArgumentNullException.ThrowIfNull(project);
        return program switch
        {
            Program<TOp, Result<T, TFailure>>.Done(var inner) => inner.TryGetValue(out Result<T, TFailure>.Success first)
                ? ResultProjected<TOp, T, TFailure, TSecond, TOut>.Project(first.Value, next(first.Value).Wrapped, project)
                : new Program<TOp, Result<TOut, TFailure>>.Done(ResultSteps.Propagate<T, TOut, TFailure>(inner)),
            Program<TOp, Result<T, TFailure>>.Pending(var instruction, var rest) =>
                new Program<TOp, Result<TOut, TFailure>>.Pending(instruction, rest.Then<Result<TOut, TFailure>>(new ResultSequenced<TOp, T, TFailure, TSecond, TOut, TNext>(next, project))),
            null => throw NoCase.Program()
        };
    }

    /// <summary>Sequences a step with a program that cannot fail and projects both values.</summary>
    /// <typeparam name="TOp">The instruction set.</typeparam>
    /// <typeparam name="T">The value type.</typeparam>
    /// <typeparam name="TFailure">The failure type.</typeparam>
    /// <typeparam name="TSecond">The value type of the second program.</typeparam>
    /// <typeparam name="TOut">The projected type.</typeparam>
    /// <param name="program">The program.</param>
    /// <param name="next">Builds the second program from the first value.</param>
    /// <param name="project">Combines both values.</param>
    /// <returns>The sequenced, projected program.</returns>
    public static Program<TOp, Result<TOut, TFailure>> SelectManyLifted<TOp, T, TFailure, TSecond, TOut>(
        Program<TOp, Result<T, TFailure>> program,
        Func<T, Program<TOp, TSecond>> next,
        Func<T, TSecond, TOut> project)
    {
        ArgumentNullException.ThrowIfNull(next);
        ArgumentNullException.ThrowIfNull(project);
        return program switch
        {
            Program<TOp, Result<T, TFailure>>.Done(var inner) => inner.TryGetValue(out Result<T, TFailure>.Success first)
                ? ResultProjectedLifted<TOp, T, TFailure, TSecond, TOut>.Project(first.Value, next(first.Value), project)
                : new Program<TOp, Result<TOut, TFailure>>.Done(ResultSteps.Propagate<T, TOut, TFailure>(inner)),
            Program<TOp, Result<T, TFailure>>.Pending(var instruction, var rest) =>
                new Program<TOp, Result<TOut, TFailure>>.Pending(instruction, rest.Then<Result<TOut, TFailure>>(new ResultSequencedLifted<TOp, T, TFailure, TSecond, TOut>(next, project))),
            null => throw NoCase.Program()
        };
    }

    /// <summary>Sequences a step with a plain result and projects both values.</summary>
    /// <typeparam name="TOp">The instruction set.</typeparam>
    /// <typeparam name="T">The value type.</typeparam>
    /// <typeparam name="TFailure">The failure type.</typeparam>
    /// <typeparam name="TSecond">The success type of the result.</typeparam>
    /// <typeparam name="TOut">The projected type.</typeparam>
    /// <param name="program">The program.</param>
    /// <param name="next">Builds the result from the first value.</param>
    /// <param name="project">Combines both values.</param>
    /// <returns>The sequenced, projected program.</returns>
    public static Program<TOp, Result<TOut, TFailure>> SelectManyInner<TOp, T, TFailure, TSecond, TOut>(
        Program<TOp, Result<T, TFailure>> program,
        Func<T, Result<TSecond, TFailure>> next,
        Func<T, TSecond, TOut> project)
    {
        ArgumentNullException.ThrowIfNull(next);
        ArgumentNullException.ThrowIfNull(project);
        return program switch
        {
            Program<TOp, Result<T, TFailure>>.Done(var inner) => new Program<TOp, Result<TOut, TFailure>>.Done(ResultSteps.Then(inner, next, project)),
            Program<TOp, Result<T, TFailure>>.Pending(var instruction, var rest) =>
                new Program<TOp, Result<TOut, TFailure>>.Pending(instruction, rest.Then<Result<TOut, TFailure>>(new ResultSequencedInner<TOp, T, TFailure, TSecond, TOut>(next, project))),
            null => throw NoCase.Program()
        };
    }

    // ----- Option -----

    /// <summary>Lifts a program that cannot be absent into one whose value is a present option.</summary>
    /// <typeparam name="TOp">The instruction set.</typeparam>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="step">The program.</param>
    /// <returns>The same instructions, finishing with a present option.</returns>
    public static Program<TOp, Option<T>> Lift<TOp, T>(Program<TOp, T> step) =>
        step switch
        {
            Program<TOp, T>.Done(var value) => new Program<TOp, Option<T>>.Done(Option.Return(value)),
            Program<TOp, T>.Pending(var instruction, var next) => new Program<TOp, Option<T>>.Pending(
                instruction,
                ReferenceEquals(next, Continuation<TOp, T>.Lifted)
                    ? LiftedOption<TOp, T>.Continuation
                    : next.Then<Option<T>>(OptionPresent<TOp, T>.Instance)),
            null => throw NoCase.Program()
        };

    /// <summary>Transforms a present value, carrying an absence through.</summary>
    /// <typeparam name="TOp">The instruction set.</typeparam>
    /// <typeparam name="T">The value type.</typeparam>
    /// <typeparam name="TOut">The transformed type.</typeparam>
    /// <param name="program">The program.</param>
    /// <param name="selector">The transformation.</param>
    /// <returns>The transformed program.</returns>
    public static Program<TOp, Option<TOut>> Select<TOp, T, TOut>(Program<TOp, Option<T>> program, Func<T, TOut> selector)
    {
        ArgumentNullException.ThrowIfNull(selector);
        return program switch
        {
            Program<TOp, Option<T>>.Done(var inner) => new Program<TOp, Option<TOut>>.Done(OptionSteps.Map(inner, selector)),
            Program<TOp, Option<T>>.Pending(var instruction, var next) =>
                new Program<TOp, Option<TOut>>.Pending(instruction, next.Then<Option<TOut>>(new OptionMapped<TOp, T, TOut>(selector))),
            null => throw NoCase.Program()
        };
    }

    /// <summary>Continues with the step <paramref name="binder"/> builds from a present value, or skips it after an absence.</summary>
    /// <typeparam name="TOp">The instruction set.</typeparam>
    /// <typeparam name="T">The value type.</typeparam>
    /// <typeparam name="TOut">The value type of the next step.</typeparam>
    /// <typeparam name="TNext">The transformer type of the next step.</typeparam>
    /// <param name="program">The program.</param>
    /// <param name="binder">Builds the next step.</param>
    /// <returns>The sequenced program.</returns>
    public static Program<TOp, Option<TOut>> Bind<TOp, T, TOut, TNext>(Program<TOp, Option<T>> program, Func<T, TNext> binder)
        where TNext : IMonadTransformer<Program<TOp, Option<TOut>>>
    {
        ArgumentNullException.ThrowIfNull(binder);
        return program switch
        {
            Program<TOp, Option<T>>.Done(var inner) => inner.TryGetValue(out Option<T>.Some some)
                ? binder(some.Value).Wrapped
                : new Program<TOp, Option<TOut>>.Done(Option.Empty<TOut>()),
            Program<TOp, Option<T>>.Pending(var instruction, var next) =>
                new Program<TOp, Option<TOut>>.Pending(instruction, next.Then<Option<TOut>>(new OptionBound<TOp, T, TOut, TNext>(binder))),
            null => throw NoCase.Program()
        };
    }

    /// <summary>Sequences two steps of a transformer type and projects both values; this is what each <c>from</c> clause compiles to.</summary>
    /// <typeparam name="TOp">The instruction set.</typeparam>
    /// <typeparam name="T">The value type.</typeparam>
    /// <typeparam name="TSecond">The value type of the second step.</typeparam>
    /// <typeparam name="TOut">The projected type.</typeparam>
    /// <typeparam name="TNext">The transformer type of the second step.</typeparam>
    /// <param name="program">The program.</param>
    /// <param name="next">Builds the second step from the first value.</param>
    /// <param name="project">Combines both values.</param>
    /// <returns>The sequenced, projected program.</returns>
    public static Program<TOp, Option<TOut>> SelectMany<TOp, T, TSecond, TOut, TNext>(
        Program<TOp, Option<T>> program,
        Func<T, TNext> next,
        Func<T, TSecond, TOut> project)
        where TNext : IMonadTransformer<Program<TOp, Option<TSecond>>>
    {
        ArgumentNullException.ThrowIfNull(next);
        ArgumentNullException.ThrowIfNull(project);
        return program switch
        {
            Program<TOp, Option<T>>.Done(var inner) => inner.TryGetValue(out Option<T>.Some first)
                ? OptionProjected<TOp, T, TSecond, TOut>.Project(first.Value, next(first.Value).Wrapped, project)
                : new Program<TOp, Option<TOut>>.Done(Option.Empty<TOut>()),
            Program<TOp, Option<T>>.Pending(var instruction, var rest) =>
                new Program<TOp, Option<TOut>>.Pending(instruction, rest.Then<Option<TOut>>(new OptionSequenced<TOp, T, TSecond, TOut, TNext>(next, project))),
            null => throw NoCase.Program()
        };
    }

    /// <summary>Sequences a step with a program that cannot be absent and projects both values.</summary>
    /// <typeparam name="TOp">The instruction set.</typeparam>
    /// <typeparam name="T">The value type.</typeparam>
    /// <typeparam name="TSecond">The value type of the second program.</typeparam>
    /// <typeparam name="TOut">The projected type.</typeparam>
    /// <param name="program">The program.</param>
    /// <param name="next">Builds the second program from the first value.</param>
    /// <param name="project">Combines both values.</param>
    /// <returns>The sequenced, projected program.</returns>
    public static Program<TOp, Option<TOut>> SelectManyLifted<TOp, T, TSecond, TOut>(
        Program<TOp, Option<T>> program,
        Func<T, Program<TOp, TSecond>> next,
        Func<T, TSecond, TOut> project)
    {
        ArgumentNullException.ThrowIfNull(next);
        ArgumentNullException.ThrowIfNull(project);
        return program switch
        {
            Program<TOp, Option<T>>.Done(var inner) => inner.TryGetValue(out Option<T>.Some first)
                ? OptionProjectedLifted<TOp, T, TSecond, TOut>.Project(first.Value, next(first.Value), project)
                : new Program<TOp, Option<TOut>>.Done(Option.Empty<TOut>()),
            Program<TOp, Option<T>>.Pending(var instruction, var rest) =>
                new Program<TOp, Option<TOut>>.Pending(instruction, rest.Then<Option<TOut>>(new OptionSequencedLifted<TOp, T, TSecond, TOut>(next, project))),
            null => throw NoCase.Program()
        };
    }

    /// <summary>Sequences a step with a plain option and projects both values.</summary>
    /// <typeparam name="TOp">The instruction set.</typeparam>
    /// <typeparam name="T">The value type.</typeparam>
    /// <typeparam name="TSecond">The value type of the option.</typeparam>
    /// <typeparam name="TOut">The projected type.</typeparam>
    /// <param name="program">The program.</param>
    /// <param name="next">Builds the option from the first value.</param>
    /// <param name="project">Combines both values.</param>
    /// <returns>The sequenced, projected program.</returns>
    public static Program<TOp, Option<TOut>> SelectManyInner<TOp, T, TSecond, TOut>(
        Program<TOp, Option<T>> program,
        Func<T, Option<TSecond>> next,
        Func<T, TSecond, TOut> project)
    {
        ArgumentNullException.ThrowIfNull(next);
        ArgumentNullException.ThrowIfNull(project);
        return program switch
        {
            Program<TOp, Option<T>>.Done(var inner) => new Program<TOp, Option<TOut>>.Done(OptionSteps.Then(inner, next, project)),
            Program<TOp, Option<T>>.Pending(var instruction, var rest) =>
                new Program<TOp, Option<TOut>>.Pending(instruction, rest.Then<Option<TOut>>(new OptionSequencedInner<TOp, T, TSecond, TOut>(next, project))),
            null => throw NoCase.Program()
        };
    }
}

/// <summary>A value that wraps another monad, as a <see cref="MonadTransformerAttribute"/> type wraps <c>Outer&lt;Inner&lt;T&gt;&gt;</c>.</summary>
/// <typeparam name="TWrapped">The wrapped type.</typeparam>
/// <remarks>The generator implements this explicitly, so <see cref="TransformedProgram"/> can unwrap a step without a closure or boxing.</remarks>
[EditorBrowsable(EditorBrowsableState.Never)]
public interface IMonadTransformer<out TWrapped>
{
    /// <summary>Gets the wrapped value.</summary>
    TWrapped Wrapped { get; }
}

// ----- Result steps -----

internal static class ResultSteps
{
    internal static Result<TOut, TFailure> Propagate<T, TOut, TFailure>(Result<T, TFailure> inner) =>
        inner.TryGetValue(out Result<T, TFailure>.Failure failure)
            ? Result.Failure<TOut, TFailure>(failure.Value)
            : throw NoCase.Result();

    internal static Result<TOut, TFailure> Map<T, TFailure, TOut>(Result<T, TFailure> inner, Func<T, TOut> selector) =>
        inner.TryGetValue(out Result<T, TFailure>.Success success)
            ? Result.Return<TOut, TFailure>(selector(success.Value))
            : Propagate<T, TOut, TFailure>(inner);

    internal static Result<TOut, TFailure> Combine<T, TFailure, TSecond, TOut>(T first, Result<TSecond, TFailure> second, Func<T, TSecond, TOut> project) =>
        second.TryGetValue(out Result<TSecond, TFailure>.Success success)
            ? Result.Return<TOut, TFailure>(project(first, success.Value))
            : Propagate<TSecond, TOut, TFailure>(second);

    internal static Result<TOut, TFailure> Then<T, TFailure, TSecond, TOut>(
        Result<T, TFailure> inner,
        Func<T, Result<TSecond, TFailure>> next,
        Func<T, TSecond, TOut> project) =>
        inner.TryGetValue(out Result<T, TFailure>.Success first)
            ? Combine(first.Value, next(first.Value), project)
            : Propagate<T, TOut, TFailure>(inner);
}

// One continuation per instruction set, value type, and failure type serves every lifted instruction. It is one step,
// not the lifted step followed by ResultSucceeded, so resuming it inside a longer program re-links no nodes.
internal sealed class LiftedResult<TOp, T, TFailure> : Step<TOp>
{
    internal static readonly Continuation<TOp, Result<T, TFailure>> Continuation = new(new LiftedResult<TOp, T, TFailure>());

    internal override Erased<TOp> Apply(object? value) =>
        Erased<TOp>.Done(Result.Return<T, TFailure>((T)LiftedStep<TOp, T>.Checked(value)!));
}

internal sealed class ResultSucceeded<TOp, T, TFailure> : Step<TOp>
{
    internal static readonly ResultSucceeded<TOp, T, TFailure> Instance = new();

    internal override Erased<TOp> Apply(object? value) =>
        Erased<TOp>.Done(Result.Return<T, TFailure>((T)value!));
}

internal sealed class ResultMapped<TOp, T, TFailure, TOut>(Func<T, TOut> selector) : Step<TOp>
{
    internal override Erased<TOp> Apply(object? value) =>
        Erased<TOp>.Done(ResultSteps.Map((Result<T, TFailure>)value!, selector));
}

internal sealed class ResultBound<TOp, T, TFailure, TOut, TNext>(Func<T, TNext> binder) : Step<TOp>
    where TNext : IMonadTransformer<Program<TOp, Result<TOut, TFailure>>>
{
    internal override Erased<TOp> Apply(object? value)
    {
        var inner = (Result<T, TFailure>)value!;
        return inner.TryGetValue(out Result<T, TFailure>.Success success)
            ? Erase(binder(success.Value).Wrapped)
            : Erased<TOp>.Done(ResultSteps.Propagate<T, TOut, TFailure>(inner));
    }
}

internal sealed class ResultSequenced<TOp, T, TFailure, TSecond, TOut, TNext>(
    Func<T, TNext> next,
    Func<T, TSecond, TOut> project) : Step<TOp>
    where TNext : IMonadTransformer<Program<TOp, Result<TSecond, TFailure>>>
{
    internal override Erased<TOp> Apply(object? value)
    {
        var inner = (Result<T, TFailure>)value!;
        return inner.TryGetValue(out Result<T, TFailure>.Success first)
            ? Erase(ResultProjected<TOp, T, TFailure, TSecond, TOut>.Project(first.Value, next(first.Value).Wrapped, project))
            : Erased<TOp>.Done(ResultSteps.Propagate<T, TOut, TFailure>(inner));
    }
}

// The second half of a from clause once the first value is known.
internal sealed class ResultProjected<TOp, T, TFailure, TSecond, TOut>(T first, Func<T, TSecond, TOut> project) : Step<TOp>
{
    internal override Erased<TOp> Apply(object? value) =>
        Erased<TOp>.Done(ResultSteps.Combine(first, (Result<TSecond, TFailure>)value!, project));

    internal static Program<TOp, Result<TOut, TFailure>> Project(T first, Program<TOp, Result<TSecond, TFailure>> second, Func<T, TSecond, TOut> project) =>
        second switch
        {
            Program<TOp, Result<TSecond, TFailure>>.Done(var inner) => new Program<TOp, Result<TOut, TFailure>>.Done(ResultSteps.Combine(first, inner, project)),
            Program<TOp, Result<TSecond, TFailure>>.Pending(var instruction, var rest) =>
                new Program<TOp, Result<TOut, TFailure>>.Pending(instruction, rest.Then<Result<TOut, TFailure>>(new ResultProjected<TOp, T, TFailure, TSecond, TOut>(first, project))),
            null => throw NoCase.Program()
        };
}

internal sealed class ResultSequencedLifted<TOp, T, TFailure, TSecond, TOut>(
    Func<T, Program<TOp, TSecond>> next,
    Func<T, TSecond, TOut> project) : Step<TOp>
{
    internal override Erased<TOp> Apply(object? value)
    {
        var inner = (Result<T, TFailure>)value!;
        return inner.TryGetValue(out Result<T, TFailure>.Success first)
            ? Erase(ResultProjectedLifted<TOp, T, TFailure, TSecond, TOut>.Project(first.Value, next(first.Value), project))
            : Erased<TOp>.Done(ResultSteps.Propagate<T, TOut, TFailure>(inner));
    }
}

internal sealed class ResultProjectedLifted<TOp, T, TFailure, TSecond, TOut>(T first, Func<T, TSecond, TOut> project) : Step<TOp>
{
    internal override Erased<TOp> Apply(object? value) =>
        Erased<TOp>.Done(Result.Return<TOut, TFailure>(project(first, (TSecond)value!)));

    internal static Program<TOp, Result<TOut, TFailure>> Project(T first, Program<TOp, TSecond> second, Func<T, TSecond, TOut> project) =>
        second switch
        {
            Program<TOp, TSecond>.Done(var value) => new Program<TOp, Result<TOut, TFailure>>.Done(Result.Return<TOut, TFailure>(project(first, value))),
            Program<TOp, TSecond>.Pending(var instruction, var rest) =>
                new Program<TOp, Result<TOut, TFailure>>.Pending(instruction, rest.Then<Result<TOut, TFailure>>(new ResultProjectedLifted<TOp, T, TFailure, TSecond, TOut>(first, project))),
            null => throw NoCase.Program()
        };
}

internal sealed class ResultSequencedInner<TOp, T, TFailure, TSecond, TOut>(
    Func<T, Result<TSecond, TFailure>> next,
    Func<T, TSecond, TOut> project) : Step<TOp>
{
    internal override Erased<TOp> Apply(object? value) =>
        Erased<TOp>.Done(ResultSteps.Then((Result<T, TFailure>)value!, next, project));
}

// ----- Option steps -----

internal static class OptionSteps
{
    internal static Option<TOut> Map<T, TOut>(Option<T> inner, Func<T, TOut> selector) =>
        inner.TryGetValue(out Option<T>.Some some) ? Option.Return(selector(some.Value)) : Option.Empty<TOut>();

    internal static Option<TOut> Combine<T, TSecond, TOut>(T first, Option<TSecond> second, Func<T, TSecond, TOut> project) =>
        second.TryGetValue(out Option<TSecond>.Some some) ? Option.Return(project(first, some.Value)) : Option.Empty<TOut>();

    internal static Option<TOut> Then<T, TSecond, TOut>(Option<T> inner, Func<T, Option<TSecond>> next, Func<T, TSecond, TOut> project) =>
        inner.TryGetValue(out Option<T>.Some first) ? Combine(first.Value, next(first.Value), project) : Option.Empty<TOut>();
}

internal sealed class LiftedOption<TOp, T> : Step<TOp>
{
    internal static readonly Continuation<TOp, Option<T>> Continuation = new(new LiftedOption<TOp, T>());

    internal override Erased<TOp> Apply(object? value) =>
        Erased<TOp>.Done(Option.Return((T)LiftedStep<TOp, T>.Checked(value)!));
}

internal sealed class OptionPresent<TOp, T> : Step<TOp>
{
    internal static readonly OptionPresent<TOp, T> Instance = new();

    internal override Erased<TOp> Apply(object? value) =>
        Erased<TOp>.Done(Option.Return((T)value!));
}

internal sealed class OptionMapped<TOp, T, TOut>(Func<T, TOut> selector) : Step<TOp>
{
    internal override Erased<TOp> Apply(object? value) =>
        Erased<TOp>.Done(OptionSteps.Map((Option<T>)value!, selector));
}

internal sealed class OptionBound<TOp, T, TOut, TNext>(Func<T, TNext> binder) : Step<TOp>
    where TNext : IMonadTransformer<Program<TOp, Option<TOut>>>
{
    internal override Erased<TOp> Apply(object? value) =>
        ((Option<T>)value!).TryGetValue(out Option<T>.Some some)
            ? Erase(binder(some.Value).Wrapped)
            : Erased<TOp>.Done(Option.Empty<TOut>());
}

internal sealed class OptionSequenced<TOp, T, TSecond, TOut, TNext>(
    Func<T, TNext> next,
    Func<T, TSecond, TOut> project) : Step<TOp>
    where TNext : IMonadTransformer<Program<TOp, Option<TSecond>>>
{
    internal override Erased<TOp> Apply(object? value) =>
        ((Option<T>)value!).TryGetValue(out Option<T>.Some first)
            ? Erase(OptionProjected<TOp, T, TSecond, TOut>.Project(first.Value, next(first.Value).Wrapped, project))
            : Erased<TOp>.Done(Option.Empty<TOut>());
}

internal sealed class OptionProjected<TOp, T, TSecond, TOut>(T first, Func<T, TSecond, TOut> project) : Step<TOp>
{
    internal override Erased<TOp> Apply(object? value) =>
        Erased<TOp>.Done(OptionSteps.Combine(first, (Option<TSecond>)value!, project));

    internal static Program<TOp, Option<TOut>> Project(T first, Program<TOp, Option<TSecond>> second, Func<T, TSecond, TOut> project) =>
        second switch
        {
            Program<TOp, Option<TSecond>>.Done(var inner) => new Program<TOp, Option<TOut>>.Done(OptionSteps.Combine(first, inner, project)),
            Program<TOp, Option<TSecond>>.Pending(var instruction, var rest) =>
                new Program<TOp, Option<TOut>>.Pending(instruction, rest.Then<Option<TOut>>(new OptionProjected<TOp, T, TSecond, TOut>(first, project))),
            null => throw NoCase.Program()
        };
}

internal sealed class OptionSequencedLifted<TOp, T, TSecond, TOut>(
    Func<T, Program<TOp, TSecond>> next,
    Func<T, TSecond, TOut> project) : Step<TOp>
{
    internal override Erased<TOp> Apply(object? value) =>
        ((Option<T>)value!).TryGetValue(out Option<T>.Some first)
            ? Erase(OptionProjectedLifted<TOp, T, TSecond, TOut>.Project(first.Value, next(first.Value), project))
            : Erased<TOp>.Done(Option.Empty<TOut>());
}

internal sealed class OptionProjectedLifted<TOp, T, TSecond, TOut>(T first, Func<T, TSecond, TOut> project) : Step<TOp>
{
    internal override Erased<TOp> Apply(object? value) =>
        Erased<TOp>.Done(Option.Return(project(first, (TSecond)value!)));

    internal static Program<TOp, Option<TOut>> Project(T first, Program<TOp, TSecond> second, Func<T, TSecond, TOut> project) =>
        second switch
        {
            Program<TOp, TSecond>.Done(var value) => new Program<TOp, Option<TOut>>.Done(Option.Return(project(first, value))),
            Program<TOp, TSecond>.Pending(var instruction, var rest) =>
                new Program<TOp, Option<TOut>>.Pending(instruction, rest.Then<Option<TOut>>(new OptionProjectedLifted<TOp, T, TSecond, TOut>(first, project))),
            null => throw NoCase.Program()
        };
}

internal sealed class OptionSequencedInner<TOp, T, TSecond, TOut>(
    Func<T, Option<TSecond>> next,
    Func<T, TSecond, TOut> project) : Step<TOp>
{
    internal override Erased<TOp> Apply(object? value) =>
        Erased<TOp>.Done(OptionSteps.Then((Option<T>)value!, next, project));
}
