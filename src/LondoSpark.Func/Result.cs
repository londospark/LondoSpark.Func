using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace LondoSpark.Func;

/// <summary>Keeps success or failure in an explicit case so nullable payloads retain their case identity.</summary>
/// <typeparam name="TSuccess">The type of the success value.</typeparam>
/// <typeparam name="TFailure">The type of the failure value.</typeparam>
/// <remarks>
/// <para>
/// The cases are the nested <see cref="Success"/> and <see cref="Failure"/>. Import the closed result type once per
/// file and a switch names its cases without type arguments, and stays exhaustive:
/// <code>
/// using static LondoSpark.Func.Result&lt;Customer, CustomerError&gt;;
///
/// var message = result switch
/// {
///     Success(var customer) => $"Registered {customer.Id}",
///     Failure(var error) => error.Describe()
/// };
/// </code>
/// When a file matches several result types, alias each one instead
/// (<c>using Registration = Result&lt;Customer, CustomerError&gt;;</c>) and write <c>Registration.Success(var c)</c>.
/// Build results with the <see cref="Prelude"/>: <c>return taken ? failure(new AlreadyRegistered(email)) : success(customer);</c>
/// needs neither <c>new</c> nor type arguments. The cases also convert implicitly, so <c>return new Success(customer);</c>
/// works too.
/// </para>
/// <para>
/// Every value created by <c>Result.Return</c>, <c>Result.Failure</c>, or a case contains a case. Because this is a
/// struct, <see langword="default"/> and <c>new()</c> produce a zero value that contains no case: <see cref="HasValue"/>
/// is <see langword="false"/>, the case patterns do not match, and <see cref="Value"/> and every extension method throw
/// <see cref="InvalidOperationException"/>. The LSF0001 analyzer reports the ways such a value is usually written.
/// Equality compares the active case and its payload.
/// </para>
/// <para>
/// Both payloads are stored inline so neither case boxes; the struct is therefore as large as both payloads plus a
/// tag (24 bytes for <c>Result&lt;long, long&gt;</c>). Prefer reference-typed or small failure payloads on hot paths.
/// </para>
/// </remarks>
[Union]
public readonly struct Result<TSuccess, TFailure> : IEquatable<Result<TSuccess, TFailure>>, IShortCircuit<Result<TSuccess, TFailure>>
{
    private const byte SuccessTag = 1;
    private const byte FailureTag = 2;

    private readonly TSuccess _success;
    private readonly TFailure _failure;
    private readonly byte _tag;

    /// <summary>Creates the successful case.</summary>
    /// <param name="value">The successful case, whose payload may be null.</param>
    public Result(Success value)
    {
        _success = value.Value;
        _failure = default!;
        _tag = SuccessTag;
    }

    /// <summary>Creates the failed case.</summary>
    /// <param name="value">The failed case, whose payload may be null.</param>
    public Result(Failure value)
    {
        _success = default!;
        _failure = value.Value;
        _tag = FailureTag;
    }

    /// <summary>Gets the boxed active case; the union contract requires this <c>object</c> property, so reading it boxes.</summary>
    /// <exception cref="InvalidOperationException">This result is <see langword="default"/> or <c>new()</c> and contains no case.</exception>
    public object Value =>
        _tag switch
        {
            SuccessTag => new Success(_success),
            FailureTag => new Failure(_failure),
            _ => throw NoCase.Result()
        };

    /// <summary>Gets whether this result contains a case; <see langword="false"/> only for <see langword="default"/>.</summary>
    public bool HasValue => _tag is SuccessTag or FailureTag;

    /// <summary>Gets the successful case when this result contains one.</summary>
    /// <param name="value">Receives the successful case, or its default value when this result does not contain it.</param>
    /// <returns><see langword="true"/> when this result contains a successful case.</returns>
    public bool TryGetValue(out Success value)
    {
        value = new Success(_success);
        return _tag == SuccessTag;
    }

    /// <summary>Gets the failed case when this result contains one.</summary>
    /// <param name="value">Receives the failed case, or its default value when this result does not contain it.</param>
    /// <returns><see langword="true"/> when this result contains a failed case.</returns>
    public bool TryGetValue(out Failure value)
    {
        value = new Failure(_failure);
        return _tag == FailureTag;
    }

    /// <summary>Determines whether this result has the same case and an equal payload as <paramref name="other"/>.</summary>
    /// <param name="other">The result to compare.</param>
    /// <returns><see langword="true"/> when both results contain the same case with equal payloads.</returns>
    public bool Equals(Result<TSuccess, TFailure> other) =>
        _tag == other._tag
        && (_tag switch
        {
            SuccessTag => EqualityComparer<TSuccess>.Default.Equals(_success, other._success),
            FailureTag => EqualityComparer<TFailure>.Default.Equals(_failure, other._failure),
            _ => true
        });

    /// <inheritdoc/>
    public override bool Equals(object? obj) =>
        obj is Result<TSuccess, TFailure> other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() =>
        _tag switch
        {
            SuccessTag => HashCode.Combine(SuccessTag, _success),
            FailureTag => HashCode.Combine(FailureTag, _failure),
            _ => _tag
        };

    /// <summary>Determines whether two results are equal.</summary>
    /// <param name="left">The first result.</param>
    /// <param name="right">The second result.</param>
    /// <returns><see langword="true"/> when both results contain the same case with equal payloads.</returns>
    public static bool operator ==(Result<TSuccess, TFailure> left, Result<TSuccess, TFailure> right) =>
        left.Equals(right);

    /// <summary>Determines whether two results are not equal.</summary>
    /// <param name="left">The first result.</param>
    /// <param name="right">The second result.</param>
    /// <returns><see langword="true"/> when the results differ in case or payload.</returns>
    public static bool operator !=(Result<TSuccess, TFailure> left, Result<TSuccess, TFailure> right) =>
        !left.Equals(right);

    /// <summary>Lets <c>await</c> unwrap the success inside a short-circuiting async method, which returns the failure otherwise.</summary>
    /// <returns>An awaiter that completes synchronously.</returns>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public ResultAwaiter<TSuccess, TFailure> GetAwaiter() => new(this);

    static bool IShortCircuit<Result<TSuccess, TFailure>>.TryShortCircuit(ShortCircuitException exception, out Result<TSuccess, TFailure> value)
    {
        if (exception is ShortCircuitException<TFailure> failed)
        {
            value = new Failure(failed.Failure);
            return true;
        }

#pragma warning disable LSF0001 // The out value is unused when this is not a short-circuit of this result.
        value = default;
#pragma warning restore LSF0001
        return false;
    }

    /// <summary>Renders the active case so success and failure are visible in logs.</summary>
    /// <returns><c>Success(value)</c>, <c>Failure(error)</c>, or <c>Result(default)</c> when there is no case.</returns>
    public override string ToString() =>
        _tag switch
        {
            SuccessTag => $"Success({_success})",
            FailureTag => $"Failure({_failure})",
            _ => "Result(default)"
        };

    /// <summary>Completes a prelude <c>success(value)</c> once the context names the failure type.</summary>
    /// <param name="value">The pending success.</param>
    /// <returns>A successful result containing the value.</returns>
    public static implicit operator Result<TSuccess, TFailure>(Prelude.Succeeded<TSuccess> value) =>
        new Success(value.Value);

    /// <summary>Completes a prelude <c>failure(error)</c> once the context names the success type.</summary>
    /// <param name="value">The pending failure.</param>
    /// <returns>A failed result containing the error.</returns>
    public static implicit operator Result<TSuccess, TFailure>(Prelude.Failed<TFailure> value) =>
        new Failure(value.Value);

    /// <summary>The successful case of <see cref="Result{TSuccess, TFailure}"/>, holding a value that may be null.</summary>
    /// <param name="Value">The successful value.</param>
    /// <remarks>Match it positionally, <c>Success(var value)</c>, after <c>using static</c> on the closed result type.</remarks>
    public readonly record struct Success(TSuccess Value);

    /// <summary>The failed case of <see cref="Result{TSuccess, TFailure}"/>, holding an error that may be null.</summary>
    /// <param name="Value">The failure value.</param>
    /// <remarks>Match it positionally, <c>Failure(var error)</c>, after <c>using static</c> on the closed result type.</remarks>
    public readonly record struct Failure(TFailure Value);
}

/// <summary>Starts a success-focused result computation.</summary>
public static class Result
{
    /// <summary>Places a successful value into a result so it can compose with fallible operations.</summary>
    /// <typeparam name="TSuccess">The success type.</typeparam>
    /// <typeparam name="TFailure">The failure type used by subsequent computations.</typeparam>
    /// <param name="value">The successful value.</param>
    /// <returns>A successful result containing <paramref name="value"/>.</returns>
    public static Result<TSuccess, TFailure> Return<TSuccess, TFailure>(TSuccess value) =>
        new Result<TSuccess, TFailure>.Success(value);

    /// <summary>Places a value, including null, into the failure case.</summary>
    /// <typeparam name="TSuccess">The success type of the result.</typeparam>
    /// <typeparam name="TFailure">The failure type.</typeparam>
    /// <param name="value">The value to store as a failure.</param>
    /// <returns>A failed result containing <paramref name="value"/>.</returns>
    public static Result<TSuccess, TFailure> Failure<TSuccess, TFailure>(TFailure value) =>
        new Result<TSuccess, TFailure>.Failure(value);
}

/// <summary>Provides fluent, applicative, and query-syntax operations for <see cref="Result{TSuccess, TFailure}"/>.</summary>
public static class ResultExtensions
{
    extension<TSuccess, TFailure>(Result<TSuccess, TFailure> result)
    {
        /// <summary>Transforms a success while carrying any failure unchanged, so callers do not have to unwrap and rewrap the result.</summary>
        /// <typeparam name="TOut">The transformed success type.</typeparam>
        /// <param name="selector">The transformation applied only to a success.</param>
        /// <returns>The transformed success or the original failure.</returns>
        public Result<TOut, TFailure> Select<TOut>(Func<TSuccess, TOut> selector) =>
            result switch
            {
                Result<TSuccess, TFailure>.Success(var success) => new Result<TOut, TFailure>.Success(selector(success)),
                Result<TSuccess, TFailure>.Failure(var failure) => new Result<TOut, TFailure>.Failure(failure),
                null => throw NoCase.Result()
            };

        /// <summary>Composes a fallible next step, stopping at the first failure instead of requiring nested result checks.</summary>
        /// <typeparam name="TOut">The success type produced by <paramref name="binder"/>.</typeparam>
        /// <param name="binder">The next computation, invoked only for a success.</param>
        /// <returns>The result of <paramref name="binder"/>, or the original failure without invoking it.</returns>
        public Result<TOut, TFailure> Bind<TOut>(Func<TSuccess, Result<TOut, TFailure>> binder) =>
            result switch
            {
                Result<TSuccess, TFailure>.Success(var success) => binder(success),
                Result<TSuccess, TFailure>.Failure(var failure) => new Result<TOut, TFailure>.Failure(failure),
                null => throw NoCase.Result()
            };

        /// <summary>Maps this result to a process exit code, so a CLI can return failure without its own switch.</summary>
        /// <returns><c>0</c> for a success; <c>1</c> for a failure.</returns>
        public int ToExitCode() =>
            result switch
            {
                Result<TSuccess, TFailure>.Success => 0,
                Result<TSuccess, TFailure>.Failure => 1,
                null => throw NoCase.Result()
            };

        /// <summary>Provides query-syntax composition over successful values; use <c>Bind</c> for fluent code.</summary>
        /// <typeparam name="TOut">The success type produced by <paramref name="selector"/>.</typeparam>
        /// <param name="selector">The next computation, invoked only for a success.</param>
        /// <returns>The result of <paramref name="selector"/>, or the original failure.</returns>
        public Result<TOut, TFailure> SelectMany<TOut>(Func<TSuccess, Result<TOut, TFailure>> selector) =>
            result.Bind(selector);

        /// <summary>Composes two fallible steps and projects their successful values without manually branching on either failure.</summary>
        /// <typeparam name="TCollection">The type of the second success value.</typeparam>
        /// <typeparam name="TResult">The type of the projected success value.</typeparam>
        /// <param name="collectionSelector">Produces the second result from the first success value.</param>
        /// <param name="resultSelector">Combines both success values.</param>
        /// <returns>The projected success, or the first failure encountered.</returns>
        public Result<TResult, TFailure> SelectMany<TCollection, TResult>(
            Func<TSuccess, Result<TCollection, TFailure>> collectionSelector,
            Func<TSuccess, TCollection, TResult> resultSelector) =>
            result switch
            {
                Result<TSuccess, TFailure>.Success(var success) => collectionSelector(success) switch
                {
                    Result<TCollection, TFailure>.Success(var collection) =>
                        new Result<TResult, TFailure>.Success(resultSelector(success, collection)),
                    Result<TCollection, TFailure>.Failure(var failure) => new Result<TResult, TFailure>.Failure(failure),
                    null => throw NoCase.Result()
                },
                Result<TSuccess, TFailure>.Failure(var failure) => new Result<TResult, TFailure>.Failure(failure),
                null => throw NoCase.Result()
            };

        /// <summary>Changes the failure representation while leaving successes untouched, for adapting errors between layers.</summary>
        /// <typeparam name="TOut">The mapped failure type.</typeparam>
        /// <param name="mapper">The transformation applied only to a failure.</param>
        /// <returns>The original success or the mapped failure.</returns>
        public Result<TSuccess, TOut> MapError<TOut>(Func<TFailure, TOut> mapper) =>
            result switch
            {
                Result<TSuccess, TFailure>.Success(var success) => new Result<TSuccess, TOut>.Success(success),
                Result<TSuccess, TFailure>.Failure(var failure) => new Result<TSuccess, TOut>.Failure(mapper(failure)),
                null => throw NoCase.Result()
            };

        /// <summary>Composes failure-side recovery or translation while ensuring a success bypasses the error handler.</summary>
        /// <typeparam name="TOut">The failure type produced by <paramref name="binder"/>.</typeparam>
        /// <param name="binder">The next failure-side computation, invoked only for a failure.</param>
        /// <returns>The original success or the result returned by <paramref name="binder"/>.</returns>
        public Result<TSuccess, TOut> BindError<TOut>(
            Func<TFailure, Result<TSuccess, TOut>> binder) =>
            result switch
            {
                Result<TSuccess, TFailure>.Success(var success) => new Result<TSuccess, TOut>.Success(success),
                Result<TSuccess, TFailure>.Failure(var failure) => binder(failure),
                null => throw NoCase.Result()
            };
    }

    extension<TInput, TOutput, TFailure>(
        Result<Func<TInput, TOutput>, TFailure> functions)
    {
        /// <summary>Applies a successful function to a successful value; failures stop the application and the function-side failure takes precedence.</summary>
        /// <param name="value">The result containing the function argument.</param>
        /// <returns>The function result, or the first failure in function-then-value order.</returns>
        public Result<TOutput, TFailure> Apply(Result<TInput, TFailure> value) =>
            functions switch
            {
                Result<Func<TInput, TOutput>, TFailure>.Success(var function) => value.Select(function),
                Result<Func<TInput, TOutput>, TFailure>.Failure(var failure) => new Result<TOutput, TFailure>.Failure(failure),
                null => throw NoCase.Result()
            };
    }
}
