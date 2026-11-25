using System;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Threading.Tasks;

namespace LondoSpark.Func;

/// <summary>Carries an awaited <see cref="None"/> or failure out of an async method to the builder that turns it back into a value.</summary>
/// <remarks>
/// <para>
/// Inside <c>async Option&lt;T&gt;</c>, a generated result type such as <c>async Registration&lt;T&gt;</c>, or a method
/// built by <see cref="ShortCircuitMethodBuilder{TUnion}"/>, <c>await</c> unwraps a present or successful value and
/// throws this exception for the other case. The exception unwinds the method, so its <see langword="finally"/>
/// blocks and <see langword="using"/> declarations run, and the method's builder catches it and returns
/// <see cref="None"/> or the failure. It never escapes such a method.
/// </para>
/// <para>
/// A <c>catch (Exception)</c> around such an await intercepts it as well; filter it out with
/// <c>catch (Exception e) when (e is not ShortCircuitException)</c>. The LSF0004 analyzer reports the unfiltered case,
/// and LSF0003 reports an await whose method's builder cannot catch it.
/// </para>
/// </remarks>
[SuppressMessage("Design", "CA1032:Implement standard exception constructors", Justification = "Only awaiters in this library create it.")]
public class ShortCircuitException : Exception
{
    private const string AbsentMessage =
        "An awaited option was absent. Await options only inside async Option<T> or a method built by ShortCircuitMethodBuilder.";

    private protected ShortCircuitException(string message)
        : base(message)
    {
    }

    internal static ShortCircuitException Absent() => new(AbsentMessage);

    internal static ShortCircuitException<TFailure> Failed<TFailure>(TFailure failure) => new(failure);
}

/// <summary>Carries an awaited failure out of an async method to the builder that returns it as a failed result.</summary>
/// <typeparam name="TFailure">The failure type.</typeparam>
[SuppressMessage("Design", "CA1032:Implement standard exception constructors", Justification = "Only awaiters in this library create it.")]
public sealed class ShortCircuitException<TFailure> : ShortCircuitException
{
    internal ShortCircuitException(TFailure failure)
        : base("An awaited result failed. Await results only inside a generated result type's async method or one built by ShortCircuitMethodBuilder.") =>
        Failure = failure;

    /// <summary>Gets the failure that was awaited.</summary>
    public TFailure Failure { get; }
}

/// <summary>A union that an async method can return after an awaited case short-circuits it.</summary>
/// <typeparam name="TSelf">The union type.</typeparam>
/// <remarks>
/// <see cref="Option{TSome}"/>, <see cref="Result{TSuccess, TFailure}"/>, and generated result types implement it, which
/// is what lets <see cref="ShortCircuitMethodBuilder{TUnion}"/> build <c>async ValueTask</c> methods returning them.
/// </remarks>
[EditorBrowsable(EditorBrowsableState.Never)]
[SuppressMessage("Design", "CA1000:Do not declare static members on generic types", Justification = "Static abstract members are the point.")]
public interface IShortCircuit<TSelf>
    where TSelf : IShortCircuit<TSelf>
{
    /// <summary>Turns a short-circuit into the value the method returns, when it belongs to this union.</summary>
    /// <param name="exception">The short-circuit an await threw.</param>
    /// <param name="value">Receives <see cref="None"/> or the failed result.</param>
    /// <returns><see langword="true"/> when this union represents the short-circuit; otherwise it is a real error.</returns>
    static abstract bool TryShortCircuit(ShortCircuitException exception, out TSelf value);
}

/// <summary>Awaits an option inside a short-circuiting async method: a present value is the await's result.</summary>
/// <typeparam name="TSome">The type of the present value.</typeparam>
/// <remarks>It always completes synchronously, so the method never suspends on it.</remarks>
[EditorBrowsable(EditorBrowsableState.Never)]
public readonly struct OptionAwaiter<TSome> : ICriticalNotifyCompletion
{
    private readonly Option<TSome> _option;

    internal OptionAwaiter(Option<TSome> option) => _option = option;

    /// <summary>Gets <see langword="true"/>: an option is already a value.</summary>
    public bool IsCompleted => true;

    /// <summary>Returns the present value, or short-circuits the method when the option is absent.</summary>
    /// <returns>The present value.</returns>
    /// <exception cref="ShortCircuitException">The option is absent.</exception>
    public TSome GetResult() =>
        _option.TryGetValue(out Option<TSome>.Some some) ? some.Value : throw ShortCircuitException.Absent();

    /// <inheritdoc/>
    public void OnCompleted(Action continuation) => continuation();

    /// <inheritdoc/>
    public void UnsafeOnCompleted(Action continuation) => continuation();
}

/// <summary>Awaits a result inside a short-circuiting async method: a success is the await's result.</summary>
/// <typeparam name="TSuccess">The success type.</typeparam>
/// <typeparam name="TFailure">The failure type.</typeparam>
/// <remarks>It always completes synchronously, so the method never suspends on it.</remarks>
[EditorBrowsable(EditorBrowsableState.Never)]
public readonly struct ResultAwaiter<TSuccess, TFailure> : ICriticalNotifyCompletion
{
    private readonly Result<TSuccess, TFailure> _result;

    internal ResultAwaiter(Result<TSuccess, TFailure> result) => _result = result;

    /// <summary>Gets <see langword="true"/>: a result is already a value.</summary>
    public bool IsCompleted => true;

    /// <summary>Returns the success, or short-circuits the method with the failure.</summary>
    /// <returns>The successful value.</returns>
    /// <exception cref="ShortCircuitException{TFailure}">The result failed.</exception>
    /// <exception cref="InvalidOperationException">The result is <see langword="default"/> and contains no case.</exception>
    public TSuccess GetResult()
    {
        if (_result.TryGetValue(out Result<TSuccess, TFailure>.Success success))
            return success.Value;

        throw _result.TryGetValue(out Result<TSuccess, TFailure>.Failure failure)
            ? ShortCircuitException.Failed(failure.Value)
            : NoCase.Result();
    }

    /// <inheritdoc/>
    public void OnCompleted(Action continuation) => continuation();

    /// <inheritdoc/>
    public void UnsafeOnCompleted(Action continuation) => continuation();
}

/// <summary>Awaits <see cref="None"/>: <c>await none;</c> always short-circuits the method.</summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public readonly struct NoneAwaiter : ICriticalNotifyCompletion
{
    /// <summary>Gets <see langword="true"/>.</summary>
    public bool IsCompleted => true;

    /// <summary>Short-circuits the method.</summary>
    /// <exception cref="ShortCircuitException">Always.</exception>
    public void GetResult() => throw ShortCircuitException.Absent();

    /// <inheritdoc/>
    public void OnCompleted(Action continuation) => continuation();

    /// <inheritdoc/>
    public void UnsafeOnCompleted(Action continuation) => continuation();
}

/// <summary>Awaits a prelude failure: <c>await failure(error);</c> always short-circuits the method with that error.</summary>
/// <typeparam name="TFailure">The failure type.</typeparam>
[EditorBrowsable(EditorBrowsableState.Never)]
public readonly struct FailedAwaiter<TFailure> : ICriticalNotifyCompletion
{
    private readonly TFailure _failure;

    internal FailedAwaiter(TFailure failure) => _failure = failure;

    /// <summary>Gets <see langword="true"/>.</summary>
    public bool IsCompleted => true;

    /// <summary>Short-circuits the method with the failure.</summary>
    /// <exception cref="ShortCircuitException{TFailure}">Always.</exception>
    public void GetResult() => throw ShortCircuitException.Failed(_failure);

    /// <inheritdoc/>
    public void OnCompleted(Action continuation) => continuation();

    /// <inheritdoc/>
    public void UnsafeOnCompleted(Action continuation) => continuation();
}

/// <summary>Builds <c>async Option&lt;T&gt;</c> methods, which run synchronously and return <see cref="None"/> when an awaited option is absent.</summary>
/// <typeparam name="TSome">The type of the present value.</typeparam>
/// <remarks>
/// The method's <c>return value;</c> produces the present case. It can await options and <see cref="None"/>, which
/// complete synchronously; awaiting anything that would suspend throws <see cref="NotSupportedException"/>, because an
/// option is returned before a suspended method could finish. Use <c>async ValueTask&lt;Option&lt;T&gt;&gt;</c> with
/// <see cref="ShortCircuitMethodBuilder{TUnion}"/> for that.
/// </remarks>
[EditorBrowsable(EditorBrowsableState.Never)]
[SuppressMessage("Design", "CA1000:Do not declare static members on generic types", Justification = "The async method builder pattern requires a static Create.")]
public struct OptionMethodBuilder<TSome>
{
    private Option<TSome> _result;
    private ExceptionDispatchInfo? _error;

    /// <summary>Creates a builder.</summary>
    /// <returns>An empty builder.</returns>
    public static OptionMethodBuilder<TSome> Create() => default;

    /// <summary>Gets the option the method produced.</summary>
    public readonly Option<TSome> Task
    {
        get
        {
            _error?.Throw();
            return _result;
        }
    }

    /// <summary>Runs the method to completion.</summary>
    /// <typeparam name="TStateMachine">The compiler-generated state machine.</typeparam>
    /// <param name="stateMachine">The state machine.</param>
    public readonly void Start<TStateMachine>(ref TStateMachine stateMachine)
        where TStateMachine : IAsyncStateMachine =>
        stateMachine.MoveNext();

    /// <summary>Not used: the method never suspends.</summary>
    /// <param name="stateMachine">The boxed state machine.</param>
    public readonly void SetStateMachine(IAsyncStateMachine stateMachine)
    {
    }

    /// <summary>Records the present value the method returned.</summary>
    /// <param name="result">The returned value.</param>
    public void SetResult(TSome result) => _result = new Option<TSome>.Some(result);

    /// <summary>Records <see cref="None"/> for a short-circuit, or the error for anything else.</summary>
    /// <param name="exception">The exception that ended the method.</param>
    public void SetException(Exception exception)
    {
        if (exception.GetType() == typeof(ShortCircuitException))
        {
            _result = default;
            return;
        }

        _error = ExceptionDispatchInfo.Capture(exception);
    }

    /// <summary>Rejects an await that would suspend the method.</summary>
    /// <typeparam name="TAwaiter">The awaiter type.</typeparam>
    /// <typeparam name="TStateMachine">The state machine type.</typeparam>
    /// <param name="awaiter">The awaiter.</param>
    /// <param name="stateMachine">The state machine.</param>
    /// <exception cref="NotSupportedException">Always.</exception>
    public readonly void AwaitOnCompleted<TAwaiter, TStateMachine>(ref TAwaiter awaiter, ref TStateMachine stateMachine)
        where TAwaiter : INotifyCompletion
        where TStateMachine : IAsyncStateMachine =>
        throw Synchronous.CannotSuspend("Option<T>");

    /// <summary>Rejects an await that would suspend the method.</summary>
    /// <typeparam name="TAwaiter">The awaiter type.</typeparam>
    /// <typeparam name="TStateMachine">The state machine type.</typeparam>
    /// <param name="awaiter">The awaiter.</param>
    /// <param name="stateMachine">The state machine.</param>
    /// <exception cref="NotSupportedException">Always.</exception>
    public readonly void AwaitUnsafeOnCompleted<TAwaiter, TStateMachine>(ref TAwaiter awaiter, ref TStateMachine stateMachine)
        where TAwaiter : ICriticalNotifyCompletion
        where TStateMachine : IAsyncStateMachine =>
        throw Synchronous.CannotSuspend("Option<T>");
}

/// <summary>Builds async methods for generated result types, which run synchronously and return the failure an await short-circuits with.</summary>
/// <typeparam name="TSuccess">The success type.</typeparam>
/// <typeparam name="TFailure">The failure type.</typeparam>
/// <remarks>
/// A result has two type parameters, and C# accepts only task-like types with one, so <c>async Result&lt;T, E&gt;</c>
/// cannot be written. A struct marked <see cref="AsyncResultAttribute{TFailure}"/> fixes the failure type and gets a
/// builder that wraps this one. Awaiting anything that would suspend throws <see cref="NotSupportedException"/>.
/// </remarks>
[EditorBrowsable(EditorBrowsableState.Never)]
[SuppressMessage("Design", "CA1000:Do not declare static members on generic types", Justification = "The async method builder pattern requires a static Create.")]
public struct ResultMethodBuilder<TSuccess, TFailure>
{
    private Result<TSuccess, TFailure> _result;
    private ExceptionDispatchInfo? _error;

    /// <summary>Creates a builder.</summary>
    /// <returns>An empty builder.</returns>
    public static ResultMethodBuilder<TSuccess, TFailure> Create() => default;

    /// <summary>Gets the result the method produced.</summary>
    public readonly Result<TSuccess, TFailure> Task
    {
        get
        {
            _error?.Throw();
            return _result;
        }
    }

    /// <summary>Runs the method to completion.</summary>
    /// <typeparam name="TStateMachine">The compiler-generated state machine.</typeparam>
    /// <param name="stateMachine">The state machine.</param>
    public readonly void Start<TStateMachine>(ref TStateMachine stateMachine)
        where TStateMachine : IAsyncStateMachine =>
        stateMachine.MoveNext();

    /// <summary>Not used: the method never suspends.</summary>
    /// <param name="stateMachine">The boxed state machine.</param>
    public readonly void SetStateMachine(IAsyncStateMachine stateMachine)
    {
    }

    /// <summary>Records the success the method returned.</summary>
    /// <param name="result">The returned value.</param>
    public void SetResult(TSuccess result) => _result = new Result<TSuccess, TFailure>.Success(result);

    /// <summary>Records the failure for a short-circuit, or the error for anything else.</summary>
    /// <param name="exception">The exception that ended the method.</param>
    public void SetException(Exception exception)
    {
        if (exception is ShortCircuitException<TFailure> failed)
        {
            _result = new Result<TSuccess, TFailure>.Failure(failed.Failure);
            return;
        }

        _error = ExceptionDispatchInfo.Capture(exception);
    }

    /// <summary>Rejects an await that would suspend the method.</summary>
    /// <typeparam name="TAwaiter">The awaiter type.</typeparam>
    /// <typeparam name="TStateMachine">The state machine type.</typeparam>
    /// <param name="awaiter">The awaiter.</param>
    /// <param name="stateMachine">The state machine.</param>
    /// <exception cref="NotSupportedException">Always.</exception>
    public readonly void AwaitOnCompleted<TAwaiter, TStateMachine>(ref TAwaiter awaiter, ref TStateMachine stateMachine)
        where TAwaiter : INotifyCompletion
        where TStateMachine : IAsyncStateMachine =>
        throw Synchronous.CannotSuspend("result");

    /// <summary>Rejects an await that would suspend the method.</summary>
    /// <typeparam name="TAwaiter">The awaiter type.</typeparam>
    /// <typeparam name="TStateMachine">The state machine type.</typeparam>
    /// <param name="awaiter">The awaiter.</param>
    /// <param name="stateMachine">The state machine.</param>
    /// <exception cref="NotSupportedException">Always.</exception>
    public readonly void AwaitUnsafeOnCompleted<TAwaiter, TStateMachine>(ref TAwaiter awaiter, ref TStateMachine stateMachine)
        where TAwaiter : ICriticalNotifyCompletion
        where TStateMachine : IAsyncStateMachine =>
        throw Synchronous.CannotSuspend("result");
}

/// <summary>Builds <c>async ValueTask</c> methods that return an option or result and short-circuit on an awaited <see cref="None"/> or failure.</summary>
/// <typeparam name="TUnion">The option, result, or generated result type the method returns.</typeparam>
/// <remarks>
/// <para>
/// Put it on the method; it may await tasks as usual, and options and results short-circuit it:
/// <code>
/// [AsyncMethodBuilder(typeof(ShortCircuitMethodBuilder&lt;&gt;))]
/// async ValueTask&lt;Result&lt;Receipt, CheckoutError&gt;&gt; CheckoutAsync(Order order)
/// {
///     var valid = await Validate(order);
///     var total = await await inventory.ReserveAsync(valid.Sku, valid.Quantity);
///     return new Receipt(await await payments.ChargeAsync(valid.Customer, total), total);
/// }
/// </code>
/// <c>await await</c> waits for the task and then unwraps its result.
/// </para>
/// <para>
/// It wraps <see cref="AsyncValueTaskMethodBuilder{TResult}"/>, so a method that completes synchronously allocates
/// nothing and a suspended one allocates as any <c>async ValueTask</c> method does. A short-circuit allocates its
/// exception.
/// </para>
/// </remarks>
[SuppressMessage("Design", "CA1000:Do not declare static members on generic types", Justification = "The async method builder pattern requires a static Create.")]
public struct ShortCircuitMethodBuilder<TUnion>
    where TUnion : IShortCircuit<TUnion>
{
    private AsyncValueTaskMethodBuilder<TUnion> _inner;

    /// <summary>Creates a builder.</summary>
    /// <returns>An empty builder.</returns>
    public static ShortCircuitMethodBuilder<TUnion> Create() => new() { _inner = AsyncValueTaskMethodBuilder<TUnion>.Create() };

    /// <summary>Gets the task for the method's value.</summary>
    public ValueTask<TUnion> Task => _inner.Task;

    /// <summary>Starts the method.</summary>
    /// <typeparam name="TStateMachine">The compiler-generated state machine.</typeparam>
    /// <param name="stateMachine">The state machine.</param>
    public void Start<TStateMachine>(ref TStateMachine stateMachine)
        where TStateMachine : IAsyncStateMachine =>
        _inner.Start(ref stateMachine);

    /// <summary>Associates the boxed state machine.</summary>
    /// <param name="stateMachine">The boxed state machine.</param>
    public void SetStateMachine(IAsyncStateMachine stateMachine) => _inner.SetStateMachine(stateMachine);

    /// <summary>Completes the task with the method's value.</summary>
    /// <param name="result">The returned value.</param>
    public void SetResult(TUnion result) => _inner.SetResult(result);

    /// <summary>Completes the task with <see cref="None"/> or the failure for a short-circuit, or faults it for anything else.</summary>
    /// <param name="exception">The exception that ended the method.</param>
    public void SetException(Exception exception)
    {
        if (exception is ShortCircuitException shortCircuit && TUnion.TryShortCircuit(shortCircuit, out var value))
        {
            _inner.SetResult(value);
            return;
        }

        _inner.SetException(exception);
    }

    /// <summary>Suspends the method until the awaiter completes.</summary>
    /// <typeparam name="TAwaiter">The awaiter type.</typeparam>
    /// <typeparam name="TStateMachine">The state machine type.</typeparam>
    /// <param name="awaiter">The awaiter.</param>
    /// <param name="stateMachine">The state machine.</param>
    public void AwaitOnCompleted<TAwaiter, TStateMachine>(ref TAwaiter awaiter, ref TStateMachine stateMachine)
        where TAwaiter : INotifyCompletion
        where TStateMachine : IAsyncStateMachine =>
        _inner.AwaitOnCompleted(ref awaiter, ref stateMachine);

    /// <summary>Suspends the method until the awaiter completes.</summary>
    /// <typeparam name="TAwaiter">The awaiter type.</typeparam>
    /// <typeparam name="TStateMachine">The state machine type.</typeparam>
    /// <param name="awaiter">The awaiter.</param>
    /// <param name="stateMachine">The state machine.</param>
    public void AwaitUnsafeOnCompleted<TAwaiter, TStateMachine>(ref TAwaiter awaiter, ref TStateMachine stateMachine)
        where TAwaiter : ICriticalNotifyCompletion
        where TStateMachine : IAsyncStateMachine =>
        _inner.AwaitUnsafeOnCompleted(ref awaiter, ref stateMachine);
}

/// <summary>Marks a partial struct with one type parameter as a result type with a fixed failure, which async methods can return.</summary>
/// <typeparam name="TFailure">The failure type.</typeparam>
/// <remarks>
/// <code>
/// [AsyncResult&lt;CustomerError&gt;]
/// public readonly partial struct Registration&lt;T&gt;;
///
/// async Registration&lt;Customer&gt; Register(NewCustomer candidate)
/// {
///     var email = await Email.Parse(candidate.Email);     // Result&lt;Email, CustomerError&gt;
///     if (directory.Contains(email)) await failure(new AlreadyRegistered(email));
///     return directory.Add(email);
/// }
/// </code>
/// The generator bundled with this package completes the struct: it wraps a
/// <see cref="Result{TSuccess, TFailure}"/>, converts to and from one implicitly, and has a builder for
/// <see langword="async"/> methods. Like a result it has no zero value.
/// </remarks>
[AttributeUsage(AttributeTargets.Struct, Inherited = false)]
public sealed class AsyncResultAttribute<TFailure> : Attribute;

internal static class Synchronous
{
    internal static NotSupportedException CannotSuspend(string kind) =>
        new($"An async {kind} method runs synchronously and cannot await something that suspends. Return a ValueTask and add [AsyncMethodBuilder(typeof(ShortCircuitMethodBuilder<>))] to await tasks.");
}
