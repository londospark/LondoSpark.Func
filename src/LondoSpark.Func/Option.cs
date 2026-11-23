using System;

namespace LondoSpark.Func;

/// <summary>Represents either a non-null value or the absence of a value.</summary>
/// <typeparam name="TSome">The type of the present value.</typeparam>
public readonly union Option<TSome>(TSome, None)
    where TSome : notnull;

/// <summary>Represents the absence of an <see cref="Option{TSome}"/> value.</summary>
public readonly record struct None;

/// <summary>Creates <see cref="Option{TSome}"/> values.</summary>
public static class Option
{
    /// <summary>Creates an option containing <paramref name="value"/>.</summary>
    /// <typeparam name="TSome">The type of the present value.</typeparam>
    /// <param name="value">The non-null value to store.</param>
    /// <returns>An option containing <paramref name="value"/>.</returns>
    public static Option<TSome> Return<TSome>(TSome value)
        where TSome : notnull =>
        value;
}

/// <summary>Provides mapping and LINQ operations for <see cref="Option{TSome}"/>.</summary>
public static class OptionExtensions
{
    /// <summary>Converts a nullable reference to an option.</summary>
    /// <typeparam name="TSome">The reference type of the value.</typeparam>
    /// <param name="value">The value to convert.</param>
    /// <returns>An option containing <paramref name="value"/> when it is non-null; otherwise, <see cref="None"/>.</returns>
    public static Option<TSome> ToOption<TSome>(this TSome? value)
        where TSome : class =>
        value is null ? new None() : Option.Return(value);

    /// <summary>Converts a nullable value type to an option.</summary>
    /// <typeparam name="TSome">The underlying value type.</typeparam>
    /// <param name="value">The value to convert.</param>
    /// <returns>An option containing the value when <paramref name="value"/> has a value; otherwise, <see cref="None"/>.</returns>
    public static Option<TSome> ToOption<TSome>(this TSome? value)
        where TSome : struct =>
        value.HasValue ? Option.Return(value.Value) : new None();

    /// <summary>Maps the present value using <paramref name="selector"/>.</summary>
    /// <typeparam name="TSome">The type of the present input value.</typeparam>
    /// <typeparam name="TOut">The type of the mapped value.</typeparam>
    /// <param name="option">The option to map.</param>
    /// <param name="selector">The function applied when <paramref name="option"/> contains a value.</param>
    /// <returns>The mapped option, or <see cref="None"/> when <paramref name="option"/> is empty.</returns>
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

    /// <summary>Applies an option-producing function to the present value.</summary>
    /// <typeparam name="TSome">The type of the present input value.</typeparam>
    /// <typeparam name="TOut">The type of the value produced by <paramref name="selector"/>.</typeparam>
    /// <param name="option">The option to bind.</param>
    /// <param name="selector">The function applied when <paramref name="option"/> contains a value.</param>
    /// <returns>The option returned by <paramref name="selector"/>, or <see cref="None"/> when <paramref name="option"/> is empty.</returns>
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

    /// <summary>Binds to a second option and combines both present values.</summary>
    /// <typeparam name="TSome">The type of the first value.</typeparam>
    /// <typeparam name="TCollection">The type of the value produced by <paramref name="collectionSelector"/>.</typeparam>
    /// <typeparam name="TResult">The type of the projected result.</typeparam>
    /// <param name="option">The first option.</param>
    /// <param name="collectionSelector">Produces a second option from the first value.</param>
    /// <param name="resultSelector">Combines the first value and the value from the second option.</param>
    /// <returns>The projected option when both options contain values; otherwise, <see cref="None"/>.</returns>
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
