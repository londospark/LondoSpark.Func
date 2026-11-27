using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace LondoSpark.Func;

/// <summary>Waits for a task of an option or result and unwraps it in one <c>await</c>, inside a method built by <see cref="ShortCircuitMethodBuilder{TUnion}"/>.</summary>
/// <remarks>
/// <c>var total = await inventory.ReserveAsync(sku, quantity).Unwrap();</c> reads as <c>await await</c> does: it waits
/// for the task, then returns the present or successful value, or short-circuits the method with <see cref="None"/> or the
/// failure. The awaitable is a struct over the task's own awaiter, so it adds no allocation.
/// </remarks>
public static class UnwrapExtensions
{
    /// <summary>Waits for the option, then unwraps it or short-circuits the method.</summary>
    /// <typeparam name="TSome">The type of the present value.</typeparam>
    /// <param name="task">The task producing the option.</param>
    /// <returns>An awaitable whose result is the present value.</returns>
    public static OptionTaskAwaitable<TSome> Unwrap<TSome>(this ValueTask<Option<TSome>> task) => new(task);

    /// <summary>Waits for the option, then unwraps it or short-circuits the method.</summary>
    /// <typeparam name="TSome">The type of the present value.</typeparam>
    /// <param name="task">The task producing the option.</param>
    /// <returns>An awaitable whose result is the present value.</returns>
    public static OptionTaskAwaitable<TSome> Unwrap<TSome>(this Task<Option<TSome>> task) => new(new ValueTask<Option<TSome>>(task));

    /// <summary>Waits for the result, then unwraps its success or short-circuits the method with its failure.</summary>
    /// <typeparam name="TSuccess">The success type.</typeparam>
    /// <typeparam name="TFailure">The failure type.</typeparam>
    /// <param name="task">The task producing the result.</param>
    /// <returns>An awaitable whose result is the successful value.</returns>
    public static ResultTaskAwaitable<TSuccess, TFailure> Unwrap<TSuccess, TFailure>(this ValueTask<Result<TSuccess, TFailure>> task) => new(task);

    /// <summary>Waits for the result, then unwraps its success or short-circuits the method with its failure.</summary>
    /// <typeparam name="TSuccess">The success type.</typeparam>
    /// <typeparam name="TFailure">The failure type.</typeparam>
    /// <param name="task">The task producing the result.</param>
    /// <returns>An awaitable whose result is the successful value.</returns>
    public static ResultTaskAwaitable<TSuccess, TFailure> Unwrap<TSuccess, TFailure>(this Task<Result<TSuccess, TFailure>> task) =>
        new(new ValueTask<Result<TSuccess, TFailure>>(task));
}

/// <summary>The awaitable <see cref="UnwrapExtensions.Unwrap{TSome}(ValueTask{Option{TSome}})"/> returns.</summary>
/// <typeparam name="TSome">The type of the present value.</typeparam>
[EditorBrowsable(EditorBrowsableState.Never)]
public readonly struct OptionTaskAwaitable<TSome>
{
    private readonly ValueTask<Option<TSome>> _task;

    internal OptionTaskAwaitable(ValueTask<Option<TSome>> task) => _task = task;

    /// <summary>Gets the awaiter.</summary>
    /// <returns>An awaiter over the task's own awaiter.</returns>
    public OptionTaskAwaiter<TSome> GetAwaiter() => new(_task.GetAwaiter());
}

/// <summary>Waits for a task of an option, then unwraps it or short-circuits the method.</summary>
/// <typeparam name="TSome">The type of the present value.</typeparam>
[EditorBrowsable(EditorBrowsableState.Never)]
public readonly struct OptionTaskAwaiter<TSome> : ICriticalNotifyCompletion
{
    private readonly ValueTaskAwaiter<Option<TSome>> _inner;

    internal OptionTaskAwaiter(ValueTaskAwaiter<Option<TSome>> inner) => _inner = inner;

    /// <summary>Gets whether the task has finished.</summary>
    public bool IsCompleted => _inner.IsCompleted;

    /// <summary>Returns the present value, or short-circuits the method when the option is absent.</summary>
    /// <returns>The present value.</returns>
    /// <exception cref="ShortCircuitException">The option is absent.</exception>
    public TSome GetResult() =>
        _inner.GetResult().TryGetValue(out Option<TSome>.Some some) ? some.Value : throw ShortCircuitException.Absent();

    /// <inheritdoc/>
    public void OnCompleted(Action continuation) => _inner.OnCompleted(continuation);

    /// <inheritdoc/>
    public void UnsafeOnCompleted(Action continuation) => _inner.UnsafeOnCompleted(continuation);
}

/// <summary>The awaitable <see cref="UnwrapExtensions.Unwrap{TSuccess, TFailure}(ValueTask{Result{TSuccess, TFailure}})"/> returns.</summary>
/// <typeparam name="TSuccess">The success type.</typeparam>
/// <typeparam name="TFailure">The failure type.</typeparam>
[EditorBrowsable(EditorBrowsableState.Never)]
public readonly struct ResultTaskAwaitable<TSuccess, TFailure>
{
    private readonly ValueTask<Result<TSuccess, TFailure>> _task;

    internal ResultTaskAwaitable(ValueTask<Result<TSuccess, TFailure>> task) => _task = task;

    /// <summary>Gets the awaiter.</summary>
    /// <returns>An awaiter over the task's own awaiter.</returns>
    public ResultTaskAwaiter<TSuccess, TFailure> GetAwaiter() => new(_task.GetAwaiter());
}

/// <summary>Waits for a task of a result, then unwraps its success or short-circuits the method with its failure.</summary>
/// <typeparam name="TSuccess">The success type.</typeparam>
/// <typeparam name="TFailure">The failure type.</typeparam>
[EditorBrowsable(EditorBrowsableState.Never)]
public readonly struct ResultTaskAwaiter<TSuccess, TFailure> : ICriticalNotifyCompletion
{
    private readonly ValueTaskAwaiter<Result<TSuccess, TFailure>> _inner;

    internal ResultTaskAwaiter(ValueTaskAwaiter<Result<TSuccess, TFailure>> inner) => _inner = inner;

    /// <summary>Gets whether the task has finished.</summary>
    public bool IsCompleted => _inner.IsCompleted;

    /// <summary>Returns the success, or short-circuits the method with the failure.</summary>
    /// <returns>The successful value.</returns>
    /// <exception cref="ShortCircuitException{TFailure}">The result failed.</exception>
    /// <exception cref="InvalidOperationException">The result is <see langword="default"/> and contains no case.</exception>
    public TSuccess GetResult() => _inner.GetResult().GetAwaiter().GetResult();

    /// <inheritdoc/>
    public void OnCompleted(Action continuation) => _inner.OnCompleted(continuation);

    /// <inheritdoc/>
    public void UnsafeOnCompleted(Action continuation) => _inner.UnsafeOnCompleted(continuation);
}
