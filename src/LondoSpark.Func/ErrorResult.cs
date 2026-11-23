using System;

namespace LondoSpark.Func;

/// <summary>Wraps a <see cref="Result{TSuccess, TFailure}"/> for failure-focused LINQ composition.</summary>
/// <typeparam name="TSuccess">The success type preserved during error-focused binding.</typeparam>
/// <typeparam name="TFailure">The current failure type.</typeparam>
/// <param name="Value">The result represented by this wrapper.</param>
public readonly record struct ErrorResult<TSuccess, TFailure>(Result<TSuccess, TFailure> Value)
    where TSuccess : notnull
    where TFailure : notnull;

/// <summary>Provides failure-focused mapping and LINQ operations for <see cref="ErrorResult{TSuccess, TFailure}"/>.</summary>
public static class ErrorResultExtensions
{
    /// <summary>Wraps a result for failure-focused composition.</summary>
    /// <typeparam name="TSuccess">The success type.</typeparam>
    /// <typeparam name="TFailure">The failure type.</typeparam>
    /// <param name="result">The result to wrap.</param>
    /// <returns>A wrapper containing <paramref name="result"/>.</returns>
    public static ErrorResult<TSuccess, TFailure> AsErrorResult<TSuccess, TFailure>(
        this Result<TSuccess, TFailure> result)
        where TSuccess : notnull
        where TFailure : notnull =>
        new(result);

    /// <summary>Unwraps the original result.</summary>
    /// <typeparam name="TSuccess">The success type.</typeparam>
    /// <typeparam name="TFailure">The failure type.</typeparam>
    /// <param name="result">The error-focused wrapper to unwrap.</param>
    /// <returns>The result held by <paramref name="result"/>.</returns>
    public static Result<TSuccess, TFailure> ToResult<TSuccess, TFailure>(
        this ErrorResult<TSuccess, TFailure> result)
        where TSuccess : notnull
        where TFailure : notnull =>
        result.Value;

    /// <summary>Maps the failure value while preserving a success value.</summary>
    /// <typeparam name="TSuccess">The success type, which is preserved.</typeparam>
    /// <typeparam name="TFailure">The input failure type.</typeparam>
    /// <typeparam name="TOut">The mapped failure type.</typeparam>
    /// <param name="result">The wrapper whose failure may be mapped.</param>
    /// <param name="selector">The function applied only when the wrapped result is a failure.</param>
    /// <returns>A wrapper containing the mapped failure or the original success.</returns>
    public static ErrorResult<TSuccess, TOut> Select<TSuccess, TFailure, TOut>(
        this ErrorResult<TSuccess, TFailure> result,
        Func<TFailure, TOut> selector)
        where TSuccess : notnull
        where TFailure : notnull
        where TOut : notnull =>
        new(result.Value.MapError(selector));

    /// <summary>Applies an error-focused function to the failure value.</summary>
    /// <typeparam name="TSuccess">The success type, which is preserved.</typeparam>
    /// <typeparam name="TFailure">The input failure type.</typeparam>
    /// <typeparam name="TOut">The failure type produced by <paramref name="binder"/>.</typeparam>
    /// <param name="result">The wrapper whose failure may be bound.</param>
    /// <param name="binder">The function applied only when the wrapped result is a failure.</param>
    /// <returns>The wrapper returned by <paramref name="binder"/>, or the original success wrapped with the new failure type.</returns>
    public static ErrorResult<TSuccess, TOut> BindError<TSuccess, TFailure, TOut>(
        this ErrorResult<TSuccess, TFailure> result,
        Func<TFailure, ErrorResult<TSuccess, TOut>> binder)
        where TSuccess : notnull
        where TFailure : notnull
        where TOut : notnull =>
        new(result.Value.BindError(failure => binder(failure).Value));

    /// <summary>Applies an error-focused function to the failure value.</summary>
    /// <typeparam name="TSuccess">The success type, which is preserved.</typeparam>
    /// <typeparam name="TFailure">The input failure type.</typeparam>
    /// <typeparam name="TOut">The failure type produced by <paramref name="selector"/>.</typeparam>
    /// <param name="result">The wrapper to bind.</param>
    /// <param name="selector">The function applied only when the wrapped result is a failure.</param>
    /// <returns>The wrapper returned by <paramref name="selector"/>, or the original success.</returns>
    public static ErrorResult<TSuccess, TOut> SelectMany<TSuccess, TFailure, TOut>(
        this ErrorResult<TSuccess, TFailure> result,
        Func<TFailure, ErrorResult<TSuccess, TOut>> selector)
        where TSuccess : notnull
        where TFailure : notnull
        where TOut : notnull =>
        result.BindError(selector);

    /// <summary>Binds to a second error-focused result and combines both failure values.</summary>
    /// <typeparam name="TSuccess">The success type, preserved from either result.</typeparam>
    /// <typeparam name="TFailure">The type of the first failure value.</typeparam>
    /// <typeparam name="TCollection">The type of the second failure value.</typeparam>
    /// <typeparam name="TResult">The type of the projected failure value.</typeparam>
    /// <param name="result">The first wrapper.</param>
    /// <param name="collectionSelector">Produces a second wrapper from the first failure value.</param>
    /// <param name="resultSelector">Combines the first and second failure values.</param>
    /// <returns>The projected failure when both wrapped results are failures; otherwise, the original success.</returns>
    public static ErrorResult<TSuccess, TResult> SelectMany<TSuccess, TFailure, TCollection, TResult>(
        this ErrorResult<TSuccess, TFailure> result,
        Func<TFailure, ErrorResult<TSuccess, TCollection>> collectionSelector,
        Func<TFailure, TCollection, TResult> resultSelector)
        where TSuccess : notnull
        where TFailure : notnull
        where TCollection : notnull
        where TResult : notnull =>
        result.BindError(failure =>
            collectionSelector(failure).Select(collection =>
                resultSelector(failure, collection)));
}
