using System;

namespace LondoSpark.Func;

public readonly record struct ErrorResult<TSuccess, TFailure>(Result<TSuccess, TFailure> Value)
    where TSuccess : notnull
    where TFailure : notnull;

public static class ErrorResultExtensions
{
    public static ErrorResult<TSuccess, TFailure> AsErrorResult<TSuccess, TFailure>(
        this Result<TSuccess, TFailure> result)
        where TSuccess : notnull
        where TFailure : notnull =>
        new(result);

    public static Result<TSuccess, TFailure> ToResult<TSuccess, TFailure>(
        this ErrorResult<TSuccess, TFailure> result)
        where TSuccess : notnull
        where TFailure : notnull =>
        result.Value;

    public static ErrorResult<TSuccess, TOut> Select<TSuccess, TFailure, TOut>(
        this ErrorResult<TSuccess, TFailure> result,
        Func<TFailure, TOut> selector)
        where TSuccess : notnull
        where TFailure : notnull
        where TOut : notnull =>
        new(result.Value.MapError(selector));

    public static ErrorResult<TSuccess, TOut> BindError<TSuccess, TFailure, TOut>(
        this ErrorResult<TSuccess, TFailure> result,
        Func<TFailure, ErrorResult<TSuccess, TOut>> binder)
        where TSuccess : notnull
        where TFailure : notnull
        where TOut : notnull =>
        new(result.Value.BindError(failure => binder(failure).Value));

    public static ErrorResult<TSuccess, TOut> SelectMany<TSuccess, TFailure, TOut>(
        this ErrorResult<TSuccess, TFailure> result,
        Func<TFailure, ErrorResult<TSuccess, TOut>> selector)
        where TSuccess : notnull
        where TFailure : notnull
        where TOut : notnull =>
        result.BindError(selector);

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
