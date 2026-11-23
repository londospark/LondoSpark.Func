using System;

namespace LondoSpark.Func;

public readonly union Result<TSuccess, TFailure>(TSuccess, TFailure)
    where TSuccess : notnull
    where TFailure : notnull;

public static class ResultExtensions
{
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
