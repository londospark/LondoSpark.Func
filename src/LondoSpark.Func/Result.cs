using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace LondoSpark.Func;

/// <summary>Keeps success or failure in an explicit case so nullable payloads retain their case identity.</summary>
/// <typeparam name="TSuccess">The type of the success value.</typeparam>
/// <typeparam name="TFailure">The type of the failure value.</typeparam>
/// <remarks>
/// Every value created by <c>Result.Return</c> or <c>Result.Failure</c> contains a case. Because this is a struct,
/// <see langword="default"/> and <c>new()</c> produce a zero value that contains no case: <see cref="HasValue"/> is
/// <see langword="false"/>, the case patterns do not match, and <see cref="Value"/> throws.
/// Equality compares the active case and its payload.
/// </remarks>
[Union]
public readonly struct Result<TSuccess, TFailure> : IEquatable<Result<TSuccess, TFailure>>
{
    private const byte SuccessTag = 1;
    private const byte FailureTag = 2;

    private readonly Success<TSuccess> _successValue;
    private readonly Failure<TFailure> _failureValue;
    private readonly byte _tag;

    /// <summary>Creates the successful case containing <paramref name="value"/>.</summary>
    /// <param name="value">The success value to store.</param>
    public Result(Success<TSuccess> value)
    {
        _successValue = value;
        _tag = SuccessTag;
    }

    /// <summary>Creates the failed case containing <paramref name="value"/>.</summary>
    /// <param name="value">The failure value to store.</param>
    public Result(Failure<TFailure> value)
    {
        _failureValue = value;
        _tag = FailureTag;
    }

    /// <summary>Gets the boxed active case.</summary>
    /// <exception cref="InvalidOperationException">This result is <see langword="default"/> or <c>new()</c> and contains no case.</exception>
    public object Value =>
        _tag switch
        {
            SuccessTag => _successValue,
            FailureTag => _failureValue,
            _ => throw new InvalidOperationException(
                "A default result contains no case. Construct results with Result.Return or Result.Failure.")
        };

    /// <summary>Gets whether this result contains a case; <see langword="false"/> only for <see langword="default"/>.</summary>
    public bool HasValue => _tag is SuccessTag or FailureTag;

    /// <summary>Gets the successful case when this result contains a <see cref="Success{TSuccess}"/>.</summary>
    /// <param name="value">Receives the successful case, or its default value when this result does not contain it.</param>
    /// <returns><see langword="true"/> when this result contains a successful case.</returns>
    public bool TryGetValue(out Success<TSuccess> value)
    {
        value = _successValue;
        return _tag == SuccessTag;
    }

    /// <summary>Gets the failed case when this result contains a <see cref="Failure{TFailure}"/>.</summary>
    /// <param name="value">Receives the failed case, or its default value when this result does not contain it.</param>
    /// <returns><see langword="true"/> when this result contains a failed case.</returns>
    public bool TryGetValue(out Failure<TFailure> value)
    {
        value = _failureValue;
        return _tag == FailureTag;
    }

    /// <summary>Determines whether this result has the same case and an equal payload as <paramref name="other"/>.</summary>
    /// <param name="other">The result to compare.</param>
    /// <returns><see langword="true"/> when both results contain the same case with equal payloads.</returns>
    public bool Equals(Result<TSuccess, TFailure> other) =>
        _tag == other._tag
        && (_tag switch
        {
            SuccessTag => EqualityComparer<Success<TSuccess>>.Default.Equals(_successValue, other._successValue),
            FailureTag => EqualityComparer<Failure<TFailure>>.Default.Equals(_failureValue, other._failureValue),
            _ => true
        });

    /// <inheritdoc/>
    public override bool Equals(object? obj) =>
        obj is Result<TSuccess, TFailure> other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() =>
        _tag switch
        {
            SuccessTag => HashCode.Combine(SuccessTag, _successValue),
            FailureTag => HashCode.Combine(FailureTag, _failureValue),
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

    /// <summary>Renders the active case so success and failure are visible in logs.</summary>
    /// <returns><c>Success(value)</c>, <c>Failure(error)</c>, or <c>Result(default)</c> when there is no case.</returns>
    public override string ToString() =>
        _tag switch
        {
            SuccessTag => $"Success({_successValue.Value})",
            FailureTag => $"Failure({_failureValue.Value})",
            _ => "Result(default)"
        };
}

/// <summary>Wraps a successful result value, including null.</summary>
/// <typeparam name="TSuccess">The type of the successful value.</typeparam>
public readonly record struct Success<TSuccess>(TSuccess Value);

/// <summary>Wraps a failed result value, including null.</summary>
/// <typeparam name="TFailure">The type of the failure value.</typeparam>
public readonly record struct Failure<TFailure>(TFailure Value);

/// <summary>Starts a success-focused result computation.</summary>
public static class Result
{
    /// <summary>Places a successful value into a result so it can compose with fallible operations.</summary>
    /// <typeparam name="TSuccess">The success type.</typeparam>
    /// <typeparam name="TFailure">The failure type used by subsequent computations.</typeparam>
    /// <param name="value">The successful value.</param>
    /// <returns>A successful result containing <paramref name="value"/>.</returns>
    public static Result<TSuccess, TFailure> Return<TSuccess, TFailure>(TSuccess value) =>
        new Success<TSuccess>(value);

    /// <summary>Places a value, including null, into the failure case.</summary>
    /// <typeparam name="TSuccess">The success type of the result.</typeparam>
    /// <typeparam name="TFailure">The failure type.</typeparam>
    /// <param name="value">The value to store as a failure.</param>
    /// <returns>A failed result containing <paramref name="value"/>.</returns>
    public static Result<TSuccess, TFailure> Failure<TSuccess, TFailure>(TFailure value) =>
        new Failure<TFailure>(value);
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
                Success<TSuccess> success => new Success<TOut>(selector(success.Value)),
                Failure<TFailure> failure => new Failure<TFailure>(failure.Value)
            };

        /// <summary>Composes a fallible next step, stopping at the first failure instead of requiring nested result checks.</summary>
        /// <typeparam name="TOut">The success type produced by <paramref name="binder"/>.</typeparam>
        /// <param name="binder">The next computation, invoked only for a success.</param>
        /// <returns>The result of <paramref name="binder"/>, or the original failure without invoking it.</returns>
        public Result<TOut, TFailure> Bind<TOut>(Func<TSuccess, Result<TOut, TFailure>> binder) =>
            result switch
            {
                Success<TSuccess> success => binder(success.Value),
                Failure<TFailure> failure => new Failure<TFailure>(failure.Value)
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
                Success<TSuccess> success => collectionSelector(success.Value) switch
                {
                    Success<TCollection> collection =>
                        new Success<TResult>(resultSelector(success.Value, collection.Value)),
                    Failure<TFailure> failure => new Failure<TFailure>(failure.Value)
                },
                Failure<TFailure> failure => new Failure<TFailure>(failure.Value)
            };

        /// <summary>Changes the failure representation while leaving successes untouched, for adapting errors between layers.</summary>
        /// <typeparam name="TOut">The mapped failure type.</typeparam>
        /// <param name="mapper">The transformation applied only to a failure.</param>
        /// <returns>The original success or the mapped failure.</returns>
        public Result<TSuccess, TOut> MapError<TOut>(Func<TFailure, TOut> mapper) =>
            result switch
            {
                Success<TSuccess> success => new Success<TSuccess>(success.Value),
                Failure<TFailure> failure => new Failure<TOut>(mapper(failure.Value))
            };

        /// <summary>Composes failure-side recovery or translation while ensuring a success bypasses the error handler.</summary>
        /// <typeparam name="TOut">The failure type produced by <paramref name="binder"/>.</typeparam>
        /// <param name="binder">The next failure-side computation, invoked only for a failure.</param>
        /// <returns>The original success or the result returned by <paramref name="binder"/>.</returns>
        public Result<TSuccess, TOut> BindError<TOut>(
            Func<TFailure, Result<TSuccess, TOut>> binder) =>
            result switch
            {
                Success<TSuccess> success => new Success<TSuccess>(success.Value),
                Failure<TFailure> failure => binder(failure.Value)
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
                Success<Func<TInput, TOutput>> function =>
                    value.Select(function.Value),
                Failure<TFailure> failure => new Failure<TFailure>(failure.Value)
            };
    }
}
