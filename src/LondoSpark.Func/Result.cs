using System;

namespace LondoSpark.Func;

/// <summary>Represents either a successful value or an expected failure.</summary>
/// <typeparam name="TSuccess">The type of the success value.</typeparam>
/// <typeparam name="TFailure">The type of the failure value.</typeparam>
public readonly union Result<TSuccess, TFailure>(TSuccess, TFailure)
    where TSuccess : notnull
    where TFailure : notnull;

/// <summary>Provides success-focused mapping and LINQ operations for <see cref="Result{TSuccess, TFailure}"/>.</summary>
public static class ResultExtensions
{
    /// <summary>Maps a successful value using <paramref name="selector"/>.</summary>
    /// <typeparam name="TSuccess">The input success type.</typeparam>
    /// <typeparam name="TFailure">The failure type, which is preserved.</typeparam>
    /// <typeparam name="TOut">The mapped success type.</typeparam>
    /// <param name="result">The result to map.</param>
    /// <param name="selector">The function applied only when <paramref name="result"/> is successful.</param>
    /// <returns>The mapped success or the original failure.</returns>
    public static Result<TOut, TFailure> Select<TSuccess, TFailure, TOut>(
        this Result<TSuccess, TFailure> result,
        Func<TSuccess, TOut> selector)
        where TSuccess : notnull
        where TFailure : notnull
        where TOut : notnull =>
        result switch
        {
            TSuccess success => selector(success),
            TFailure failure => failure
        };

    /// <summary>Applies a result-producing function to a successful value.</summary>
    /// <typeparam name="TSuccess">The input success type.</typeparam>
    /// <typeparam name="TFailure">The failure type, which is preserved.</typeparam>
    /// <typeparam name="TOut">The success type produced by <paramref name="selector"/>.</typeparam>
    /// <param name="result">The result to bind.</param>
    /// <param name="selector">The function applied only when <paramref name="result"/> is successful.</param>
    /// <returns>The result returned by <paramref name="selector"/>, or the original failure.</returns>
    public static Result<TOut, TFailure> SelectMany<TSuccess, TFailure, TOut>(
        this Result<TSuccess, TFailure> result,
        Func<TSuccess, Result<TOut, TFailure>> selector)
        where TSuccess : notnull
        where TFailure : notnull
        where TOut : notnull =>
        result switch
        {
            TSuccess success => selector(success),
            TFailure failure => failure
        };

    /// <summary>Binds to a second result and combines both successful values.</summary>
    /// <typeparam name="TSuccess">The type of the first success value.</typeparam>
    /// <typeparam name="TFailure">The failure type, preserved from either result.</typeparam>
    /// <typeparam name="TCollection">The type of the second success value.</typeparam>
    /// <typeparam name="TResult">The type of the projected success value.</typeparam>
    /// <param name="result">The first result.</param>
    /// <param name="collectionSelector">Produces a second result from the first success value.</param>
    /// <param name="resultSelector">Combines the first and second success values.</param>
    /// <returns>The projected success when both results succeed; otherwise, the first failure encountered.</returns>
    public static Result<TResult, TFailure> SelectMany<TSuccess, TFailure, TCollection, TResult>(
        this Result<TSuccess, TFailure> result,
        Func<TSuccess, Result<TCollection, TFailure>> collectionSelector,
        Func<TSuccess, TCollection, TResult> resultSelector)
        where TSuccess : notnull
        where TFailure : notnull
        where TCollection : notnull
        where TResult : notnull =>
        result switch
        {
            TSuccess success => collectionSelector(success) switch
            {
                TCollection collection => resultSelector(success, collection),
                TFailure failure => failure
            },
            TFailure failure => failure
        };

    /// <summary>Maps a failure value while preserving a success value.</summary>
    /// <typeparam name="TSuccess">The success type, which is preserved.</typeparam>
    /// <typeparam name="TFailure">The input failure type.</typeparam>
    /// <typeparam name="TOut">The mapped failure type.</typeparam>
    /// <param name="result">The result whose failure may be mapped.</param>
    /// <param name="mapper">The function applied only when <paramref name="result"/> is a failure.</param>
    /// <returns>The original success or the mapped failure.</returns>
    public static Result<TSuccess, TOut> MapError<TSuccess, TFailure, TOut>(
        this Result<TSuccess, TFailure> result,
        Func<TFailure, TOut> mapper)
        where TSuccess : notnull
        where TFailure : notnull
        where TOut : notnull =>
        result switch
        {
            TSuccess success => success,
            TFailure failure => mapper(failure)
        };

    /// <summary>Applies a result-producing function to a failure value.</summary>
    /// <typeparam name="TSuccess">The success type, which is preserved.</typeparam>
    /// <typeparam name="TFailure">The input failure type.</typeparam>
    /// <typeparam name="TOut">The failure type produced by <paramref name="binder"/>.</typeparam>
    /// <param name="result">The result whose failure may be bound.</param>
    /// <param name="binder">The function applied only when <paramref name="result"/> is a failure.</param>
    /// <returns>The original success or the result returned by <paramref name="binder"/>.</returns>
    public static Result<TSuccess, TOut> BindError<TSuccess, TFailure, TOut>(
        this Result<TSuccess, TFailure> result,
        Func<TFailure, Result<TSuccess, TOut>> binder)
        where TSuccess : notnull
        where TFailure : notnull
        where TOut : notnull =>
        result switch
        {
            TSuccess success => success,
            TFailure failure => binder(failure)
        };
}
