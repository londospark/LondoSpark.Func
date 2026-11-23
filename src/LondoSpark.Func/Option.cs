using System;

namespace LondoSpark.Func;

public readonly union Option<TSome>(TSome, None)
    where TSome : notnull;

public readonly record struct None;

public static class Option
{
    public static Option<TSome> Return<TSome>(TSome value)
        where TSome : notnull =>
        value;
}

public static class OptionExtensions
{
    public static Option<TSome> ToOption<TSome>(this TSome? value)
        where TSome : class =>
        value is null ? new None() : Option.Return(value);

    public static Option<TSome> ToOption<TSome>(this TSome? value)
        where TSome : struct =>
        value.HasValue ? Option.Return(value.Value) : new None();

    public static Option<TOut> Select<TSome, TOut>(
        this Option<TSome> option,
        Func<TSome, TOut> selector)
        where TSome : notnull
        where TOut : notnull =>
        option switch
        {
            TSome some => selector(some),
            None none => none
        };

    public static Option<TOut> SelectMany<TSome, TOut>(
        this Option<TSome> option,
        Func<TSome, Option<TOut>> selector)
        where TSome : notnull
        where TOut : notnull =>
        option switch
        {
            TSome some => selector(some),
            None none => none
        };

    public static Option<TResult> SelectMany<TSome, TCollection, TResult>(
        this Option<TSome> option,
        Func<TSome, Option<TCollection>> collectionSelector,
        Func<TSome, TCollection, TResult> resultSelector)
        where TSome : notnull
        where TCollection : notnull
        where TResult : notnull =>
        option switch
        {
            TSome some => collectionSelector(some) switch
            {
                TCollection collection => resultSelector(some, collection),
                None none => none
            },
            None none => none
        };
}
