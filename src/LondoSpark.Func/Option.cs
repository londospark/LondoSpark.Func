using System;

namespace LondoSpark.Func;

/// <summary>Models optional data explicitly so callers can compose computations without null checks or sentinel values.</summary>
/// <typeparam name="TSome">The type of the present value.</typeparam>
public readonly union Option<TSome>(TSome, None)
    where TSome : notnull;

/// <summary>Represents the absence of a value without requiring a placeholder payload.</summary>
public readonly record struct None;

/// <summary>Starts an option computation with a present value.</summary>
public static class Option
{
    /// <summary>Places a known value into an option so it can participate in option-based composition.</summary>
    /// <typeparam name="TSome">The type of the present value.</typeparam>
    /// <param name="value">The non-null value to store.</param>
    /// <returns>An option containing <paramref name="value"/>.</returns>
    public static Option<TSome> Return<TSome>(TSome value)
        where TSome : notnull =>
        value;
}

/// <summary>Provides fluent, applicative, and query-syntax operations for <see cref="Option{TSome}"/>.</summary>
public static class OptionExtensions
{
    extension<TSome>(TSome? value)
        where TSome : class
    {
        /// <summary>Normalizes a nullable reference at a boundary so subsequent code handles absence as an explicit case.</summary>
        /// <returns>An option containing the reference when non-null; otherwise, <see cref="None"/>.</returns>
        public Option<TSome> ToOption() =>
            value is null ? new None() : Option.Return(value);
    }

    extension<TSome>(TSome? value)
        where TSome : struct
    {
        /// <summary>Normalizes an absent nullable value at a boundary so subsequent code can compose it as an option.</summary>
        /// <returns>An option containing the value when present; otherwise, <see cref="None"/>.</returns>
        public Option<TSome> ToOption() =>
            value.HasValue ? Option.Return(value.Value) : new None();
    }

    extension<TInput, TOutput>(Option<Func<TInput, TOutput>> functions)
        where TInput : notnull
        where TOutput : notnull
    {
        /// <summary>Applies an optional function to an optional value so either missing input short-circuits the calculation.</summary>
        /// <param name="value">The optional argument to the function.</param>
        /// <returns>The function result when both inputs are present; otherwise, <see cref="None"/>.</returns>
        public Option<TOutput> Apply(Option<TInput> value) =>
            functions switch
            {
                Func<TInput, TOutput> function => value.Select(function),
                None none => none
            };
    }

    extension<TSome>(Option<TSome> option)
        where TSome : notnull
    {
        /// <summary>Transforms a present value while allowing absence to flow through unchanged.</summary>
        /// <typeparam name="TOut">The type of the transformed value.</typeparam>
        /// <param name="selector">The transformation applied only when a value is present.</param>
        /// <returns>The transformed value in an option, or <see cref="None"/> when the option is empty.</returns>
        public Option<TOut> Select<TOut>(Func<TSome, TOut> selector)
            where TOut : notnull =>
            option switch
            {
                TSome some => selector(some),
                None none => none
            };

        /// <summary>Composes a computation that may also produce no value, short-circuiting when this option is empty.</summary>
        /// <typeparam name="TOut">The type of the value produced by <paramref name="binder"/>.</typeparam>
        /// <param name="binder">The next computation, invoked only when this option contains a value.</param>
        /// <returns>The result of <paramref name="binder"/>, or <see cref="None"/> without invoking it for an empty option.</returns>
        public Option<TOut> Bind<TOut>(Func<TSome, Option<TOut>> binder)
            where TOut : notnull =>
            option switch
            {
                TSome some => binder(some),
                None none => none
            };

        /// <summary>Provides query-syntax composition over <see cref="Option{TSome}"/>; use <c>Bind</c> for fluent code.</summary>
        /// <typeparam name="TOut">The type of the value produced by <paramref name="selector"/>.</typeparam>
        /// <param name="selector">The next computation, invoked only when this option contains a value.</param>
        /// <returns>The result of <paramref name="selector"/>, or <see cref="None"/> when the option is empty.</returns>
        public Option<TOut> SelectMany<TOut>(Func<TSome, Option<TOut>> selector)
            where TOut : notnull =>
            option.Bind(selector);

        /// <summary>Composes two optional computations and projects their values without manual absence checks.</summary>
        /// <typeparam name="TCollection">The type of the second present value.</typeparam>
        /// <typeparam name="TResult">The type of the projected result.</typeparam>
        /// <param name="collectionSelector">Produces the second option from the first value.</param>
        /// <param name="resultSelector">Combines both values when both options are present.</param>
        /// <returns>The projected value in an option, or <see cref="None"/> if either computation is empty.</returns>
        public Option<TResult> SelectMany<TCollection, TResult>(
            Func<TSome, Option<TCollection>> collectionSelector,
            Func<TSome, TCollection, TResult> resultSelector)
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
}
