using System;

namespace LondoSpark.Func;

/// <summary>Views a result from its failure side so recovery and error translation can be composed without changing success flow.</summary>
/// <typeparam name="TSuccess">The success type preserved during error-focused binding.</typeparam>
/// <typeparam name="TFailure">The current failure type.</typeparam>
/// <param name="Value">The result represented by this wrapper.</param>
/// <remarks>Like <see cref="Result{TSuccess, TFailure}"/>, a <see langword="default"/> wrapper contains no case and its operations throw.</remarks>
public readonly record struct ErrorResult<TSuccess, TFailure>(Result<TSuccess, TFailure> Value);

/// <summary>Starts a failure-focused computation with an error value.</summary>
public static class ErrorResult
{
    /// <summary>Places an error into the focused side so it can compose with error handlers.</summary>
    /// <typeparam name="TSuccess">The success type passed through by error-focused operations.</typeparam>
    /// <typeparam name="TFailure">The focused error type.</typeparam>
    /// <param name="failure">The error value to place in the wrapper.</param>
    /// <returns>An error-focused wrapper containing <paramref name="failure"/>.</returns>
    public static ErrorResult<TSuccess, TFailure> Return<TSuccess, TFailure>(TFailure failure) =>
        new(Result.Failure<TSuccess, TFailure>(failure));
}

/// <summary>Provides fluent, applicative, and query-syntax operations while preserving successful results.</summary>
public static class ErrorResultExtensions
{
    extension<TSuccess, TFailure>(Result<TSuccess, TFailure> result)
    {
        /// <summary>Switches the composition focus to failures when a pipeline needs to recover from or explain errors.</summary>
        /// <returns>A wrapper that composes on failures and passes successes through.</returns>
        public ErrorResult<TSuccess, TFailure> AsErrorResult() =>
            new(result);
    }

    extension<TSuccess, TFailure>(ErrorResult<TSuccess, TFailure> result)
    {
        /// <summary>Returns to success-focused composition after an error-focused pipeline.</summary>
        /// <returns>The original result without changing either case.</returns>
        public Result<TSuccess, TFailure> ToResult() =>
            result.Value;

        /// <summary>Transforms a failure while passing success through, which is useful for adapting errors at a boundary.</summary>
        /// <typeparam name="TOut">The mapped failure type.</typeparam>
        /// <param name="selector">The transformation applied only to a failure.</param>
        /// <returns>A wrapper containing the mapped failure or unchanged success.</returns>
        public ErrorResult<TSuccess, TOut> Select<TOut>(Func<TFailure, TOut> selector) =>
            new(result.Value.MapError(selector));

        /// <summary>Composes a failure-side computation while allowing successes to bypass recovery logic.</summary>
        /// <typeparam name="TOut">The failure type produced by <paramref name="binder"/>.</typeparam>
        /// <param name="binder">The next error-focused computation.</param>
        /// <returns>The result of <paramref name="binder"/> for a failure, or the unchanged success.</returns>
        public ErrorResult<TSuccess, TOut> BindError<TOut>(
            Func<TFailure, ErrorResult<TSuccess, TOut>> binder) =>
            result.Value switch
            {
                Result<TSuccess, TFailure>.Success(var success) =>
                    Result.Return<TSuccess, TOut>(success).AsErrorResult(),
                Result<TSuccess, TFailure>.Failure(var failure) => binder(failure),
                null => throw NoCase.Result()
            };

        /// <summary>Provides query-syntax composition over failures; use <c>BindError</c> for fluent code.</summary>
        /// <typeparam name="TOut">The failure type produced by <paramref name="selector"/>.</typeparam>
        /// <param name="selector">The next error-focused computation.</param>
        /// <returns>The result of <paramref name="selector"/> for a failure, or the unchanged success.</returns>
        public ErrorResult<TSuccess, TOut> SelectMany<TOut>(
            Func<TFailure, ErrorResult<TSuccess, TOut>> selector) =>
            result.BindError(selector);

        /// <summary>Composes two failure-side computations and projects their errors without branching at each step.</summary>
        /// <typeparam name="TCollection">The type of the second failure value.</typeparam>
        /// <typeparam name="TResult">The projected failure type.</typeparam>
        /// <param name="collectionSelector">Produces a second error-focused result.</param>
        /// <param name="resultSelector">Combines the two failure values.</param>
        /// <returns>The projected failure, or the original success when the input is successful.</returns>
        public ErrorResult<TSuccess, TResult> SelectMany<TCollection, TResult>(
            Func<TFailure, ErrorResult<TSuccess, TCollection>> collectionSelector,
            Func<TFailure, TCollection, TResult> resultSelector) =>
            result.Value switch
            {
                Result<TSuccess, TFailure>.Success(var outerSuccess) =>
                    Result.Return<TSuccess, TResult>(outerSuccess).AsErrorResult(),
                Result<TSuccess, TFailure>.Failure(var failure) => collectionSelector(failure).Value switch
                {
                    Result<TSuccess, TCollection>.Success(var innerSuccess) =>
                        Result.Return<TSuccess, TResult>(innerSuccess).AsErrorResult(),
                    Result<TSuccess, TCollection>.Failure(var collection) =>
                        ErrorResult.Return<TSuccess, TResult>(
                            resultSelector(failure, collection)),
                    null => throw NoCase.Result()
                },
                null => throw NoCase.Result()
            };
    }

    extension<TSuccess, TInput, TOutput>(
        ErrorResult<TSuccess, Func<TInput, TOutput>> functions)
    {
        /// <summary>Applies a function to a failure value; a success in either position short-circuits error-focused application.</summary>
        /// <param name="value">The error-focused argument.</param>
        /// <returns>The transformed failure, or the first success pass-through encountered.</returns>
        public ErrorResult<TSuccess, TOutput> Apply(
            ErrorResult<TSuccess, TInput> value) =>
            functions.Value switch
            {
                Result<TSuccess, Func<TInput, TOutput>>.Success(var success) =>
                    Result.Return<TSuccess, TOutput>(success).AsErrorResult(),
                Result<TSuccess, Func<TInput, TOutput>>.Failure(var function) =>
                    value.Select(function),
                null => throw NoCase.Result()
            };
    }
}
