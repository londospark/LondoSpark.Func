using System;

namespace LondoSpark.Func;

/// <summary>Keeps success or failure in an explicit case so nullable payloads retain their case identity.</summary>
/// <typeparam name="TSuccess">The type of the success value.</typeparam>
/// <typeparam name="TFailure">The type of the failure value.</typeparam>
public readonly union Result<TSuccess, TFailure>(Success<TSuccess>, Failure<TFailure>);

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
