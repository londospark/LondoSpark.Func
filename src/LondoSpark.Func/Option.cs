using System;

namespace LondoSpark.Func;

/// <summary>Models a present value or explicit absence; a nullable payload remains present.</summary>
/// <typeparam name="TSome">The type of the present value.</typeparam>
[System.Runtime.CompilerServices.Union]
public readonly struct Option<TSome>
{
    private const byte UninitializedTag = 0;
    private const byte SomeTag = 1;
    private const byte NoneTag = 2;

    private readonly Some<TSome> _someValue;
    private readonly byte _tag;

    /// <summary>Creates the present case containing <paramref name="value"/>.</summary>
    /// <param name="value">The value to store.</param>
    public Option(Some<TSome> value)
    {
        _someValue = value;
        _tag = SomeTag;
    }

    /// <summary>Creates the absent case.</summary>
    /// <param name="value">The absent case value.</param>
    public Option(None value)
    {
        _someValue = default;
        _tag = NoneTag;
    }

    /// <summary>Gets the boxed active case, or <see langword="null"/> for a default-initialized option.</summary>
    public object? Value =>
        _tag switch
        {
            SomeTag => _someValue,
            NoneTag => new None(),
            _ => null
        };

    /// <summary>Gets whether this option contains an initialized case.</summary>
    public bool HasValue => _tag != UninitializedTag;

    /// <summary>Gets the present case when this option contains a <see cref="Some{TSome}"/>.</summary>
    /// <param name="value">Receives the present case, or its default value when this option does not contain it.</param>
    /// <returns><see langword="true"/> when this option contains a present case.</returns>
    public bool TryGetValue(out Some<TSome> value)
    {
        value = _someValue;
        return _tag == SomeTag;
    }

    /// <summary>Gets the absent case when this option contains a <see cref="None"/>.</summary>
    /// <param name="value">Receives the absent case, or its default value when this option does not contain it.</param>
    /// <returns><see langword="true"/> when this option contains an absent case.</returns>
    public bool TryGetValue(out None value)
    {
        value = default;
        return _tag == NoneTag;
    }
}

/// <summary>Wraps a present option value, including null.</summary>
/// <typeparam name="T">The type of the present value.</typeparam>
public readonly record struct Some<T>(T Value);

/// <summary>Represents the absence of a value without requiring a placeholder payload.</summary>
public readonly record struct None;

/// <summary>Starts an option computation with a present value.</summary>
public static class Option
{
    /// <summary>Places a value, including null, into the present case so it can participate in option-based composition.</summary>
    /// <typeparam name="TSome">The type of the present value.</typeparam>
    /// <param name="value">The value to store.</param>
    /// <returns>An option containing <paramref name="value"/>.</returns>
    public static Option<TSome> Return<TSome>(TSome value) =>
        new Some<TSome>(value);

    /// <summary>Creates an absent option without requiring callers to construct the <see cref="None"/> case.</summary>
    /// <typeparam name="TSome">The type of a present value.</typeparam>
    /// <returns>An option containing <see cref="None"/>.</returns>
    public static Option<TSome> Empty<TSome>() =>
        new None();
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
    {
        /// <summary>Applies an optional function to an optional value so either missing input short-circuits the calculation.</summary>
        /// <param name="value">The optional argument to the function.</param>
        /// <returns>The function result when both inputs are present; otherwise, <see cref="None"/>.</returns>
        public Option<TOutput> Apply(Option<TInput> value) =>
            functions switch
            {
                Some<Func<TInput, TOutput>> function =>
                    value.Select(function.Value),
                None none => none
            };
    }

    extension<TSome>(Option<TSome> option)
    {
        /// <summary>Transforms a present value while allowing absence to flow through unchanged.</summary>
        /// <typeparam name="TOut">The type of the transformed value.</typeparam>
        /// <param name="selector">The transformation applied only when a value is present.</param>
        /// <returns>The transformed value in an option, or <see cref="None"/> when the option is empty.</returns>
        public Option<TOut> Select<TOut>(Func<TSome, TOut> selector) =>
            option switch
            {
                Some<TSome> some => new Some<TOut>(selector(some.Value)),
                None none => none
            };

        /// <summary>Composes a computation that may also produce no value, short-circuiting when this option is empty.</summary>
        /// <typeparam name="TOut">The type of the value produced by <paramref name="binder"/>.</typeparam>
        /// <param name="binder">The next computation, invoked only when this option contains a value.</param>
        /// <returns>The result of <paramref name="binder"/>, or <see cref="None"/> without invoking it for an empty option.</returns>
        public Option<TOut> Bind<TOut>(Func<TSome, Option<TOut>> binder) =>
            option switch
            {
                Some<TSome> some => binder(some.Value),
                None none => none
            };

        /// <summary>Provides query-syntax composition over <see cref="Option{TSome}"/>; use <c>Bind</c> for fluent code.</summary>
        /// <typeparam name="TOut">The type of the value produced by <paramref name="selector"/>.</typeparam>
        /// <param name="selector">The next computation, invoked only when this option contains a value.</param>
        /// <returns>The result of <paramref name="selector"/>, or <see cref="None"/> when the option is empty.</returns>
        public Option<TOut> SelectMany<TOut>(Func<TSome, Option<TOut>> selector) =>
            option.Bind(selector);

        /// <summary>Composes two optional computations and projects their values without manual absence checks.</summary>
        /// <typeparam name="TCollection">The type of the second present value.</typeparam>
        /// <typeparam name="TResult">The type of the projected result.</typeparam>
        /// <param name="collectionSelector">Produces the second option from the first value.</param>
        /// <param name="resultSelector">Combines both values when both options are present.</param>
        /// <returns>The projected value in an option, or <see cref="None"/> if either computation is empty.</returns>
        public Option<TResult> SelectMany<TCollection, TResult>(
            Func<TSome, Option<TCollection>> collectionSelector,
            Func<TSome, TCollection, TResult> resultSelector) =>
            option switch
            {
                Some<TSome> some => collectionSelector(some.Value) switch
                {
                    Some<TCollection> collection =>
                        new Some<TResult>(resultSelector(some.Value, collection.Value)),
                    None none => none
                },
                None none => none
            };
    }
}
