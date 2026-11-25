using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace LondoSpark.Func;

/// <summary>Models a present value or explicit absence; a nullable payload remains present.</summary>
/// <typeparam name="TSome">The type of the present value.</typeparam>
/// <remarks>
/// <para>
/// The cases are the nested <see cref="Some"/> and the shared <see cref="None"/>. Import the closed option type once
/// per file and a switch names its cases without type arguments, and stays exhaustive:
/// <code>
/// using static LondoSpark.Func.Option&lt;Customer&gt;;
///
/// var greeting = directory.Find(id) switch
/// {
///     Some(var customer) => $"Hello, {customer.Name}",
///     None => "Not found"
/// };
/// </code>
/// Both cases convert implicitly, so a method returning <c>Option&lt;Customer&gt;</c> can
/// <c>return new Some(customer);</c> or <c>return new None();</c>.
/// </para>
/// <para>
/// Every value, including <see langword="default"/> and <c>new()</c>, contains a case; the zero value is <see cref="None"/>.
/// Equality compares the active case and its payload. The payload is stored inline, so matching does not box.
/// </para>
/// </remarks>
[Union]
public readonly struct Option<TSome> : IEquatable<Option<TSome>>
{
    private readonly TSome _value;
    private readonly bool _isSome;

    /// <summary>Creates the present case.</summary>
    /// <param name="value">The present case, whose payload may be null.</param>
    public Option(Some value)
    {
        _value = value.Value;
        _isSome = true;
    }

    /// <summary>Creates the absent case, which is also the value of <see langword="default"/>.</summary>
    /// <param name="value">The absent case value.</param>
#pragma warning disable IDE0060 // Remove unused parameter - the parameter type names the union case.
    public Option(None value)
#pragma warning restore IDE0060
    {
        _value = default!;
        _isSome = false;
    }

    /// <summary>Gets the boxed active case; the union contract requires this <c>object</c> property, so reading a present case boxes it. Never <see langword="null"/>.</summary>
    /// <remarks>The absent case returns one instance shared by every option type, so it does not allocate.</remarks>
    public object Value =>
        _isSome ? new Some(_value) : None.Boxed;

    /// <summary>Gets a value indicating whether this option contains a case; always <see langword="true"/> because <see langword="default"/> is <see cref="None"/>.</summary>
    public bool HasValue => true;

    /// <summary>Gets the present case when this option contains one.</summary>
    /// <param name="value">Receives the present case, or its default value when this option is absent.</param>
    /// <returns><see langword="true"/> when this option contains a present case.</returns>
    public bool TryGetValue(out Some value)
    {
        value = new Some(_value);
        return _isSome;
    }

    /// <summary>Gets the absent case when this option contains a <see cref="None"/>.</summary>
    /// <param name="value">Receives the absent case.</param>
    /// <returns><see langword="true"/> when this option is absent.</returns>
    public bool TryGetValue(out None value)
    {
        value = default;
        return !_isSome;
    }

    /// <summary>Determines whether this option has the same case and an equal payload as <paramref name="other"/>.</summary>
    /// <param name="other">The option to compare.</param>
    /// <returns><see langword="true"/> when both options contain the same case with equal payloads.</returns>
    public bool Equals(Option<TSome> other) =>
        _isSome == other._isSome
        && (!_isSome || EqualityComparer<TSome>.Default.Equals(_value, other._value));

    /// <inheritdoc/>
    public override bool Equals(object? obj) =>
        obj is Option<TSome> other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() =>
        _isSome ? HashCode.Combine(true, _value) : 0;

    /// <summary>Determines whether two options are equal.</summary>
    /// <param name="left">The first option.</param>
    /// <param name="right">The second option.</param>
    /// <returns><see langword="true"/> when both options contain the same case with equal payloads.</returns>
    public static bool operator ==(Option<TSome> left, Option<TSome> right) =>
        left.Equals(right);

    /// <summary>Determines whether two options are not equal.</summary>
    /// <param name="left">The first option.</param>
    /// <param name="right">The second option.</param>
    /// <returns><see langword="true"/> when the options differ in case or payload.</returns>
    public static bool operator !=(Option<TSome> left, Option<TSome> right) =>
        !left.Equals(right);

    /// <summary>Renders the active case, using <c>Some(</c> and <c>None</c> so absence is visible in logs.</summary>
    /// <returns><c>Some(value)</c> for a present value, <c>Some(null)</c> for a present null, or <c>None</c>.</returns>
    public override string ToString() =>
        !_isSome ? nameof(None)
        : _value is null ? "Some(null)"
        : $"Some({_value})";

    /// <summary>The present case of <see cref="Option{TSome}"/>, holding a value that may be null.</summary>
    /// <param name="Value">The present value.</param>
    /// <remarks>Match it positionally, <c>Some(var value)</c>, after <c>using static</c> on the closed option type.</remarks>
    public readonly record struct Some(TSome Value);
}

/// <summary>Represents the absence of a value without requiring a placeholder payload.</summary>
/// <remarks>
/// One <see cref="None"/> serves every option type: it has no fields, so copies are free and all equal, and it
/// converts implicitly to an absent <see cref="Option{TSome}"/> of any type.
/// </remarks>
public readonly record struct None
{
    // The single boxed None, returned by Option<T>.Value for every T so reading an absent case never allocates.
    internal static readonly object Boxed = new None();
}

/// <summary>Starts an option computation.</summary>
public static class Option
{
    /// <summary>Places a value, including null, into the present case so it can participate in option-based composition.</summary>
    /// <typeparam name="TSome">The type of the present value.</typeparam>
    /// <param name="value">The value to store.</param>
    /// <returns>An option containing <paramref name="value"/>.</returns>
    public static Option<TSome> Return<TSome>(TSome value) =>
        new Option<TSome>.Some(value);

    /// <summary>Creates an absent option without requiring callers to construct the <see cref="LondoSpark.Func.None"/> case.</summary>
    /// <typeparam name="TSome">The type of a present value.</typeparam>
    /// <returns>An option containing <see cref="LondoSpark.Func.None"/>.</returns>
    public static Option<TSome> Empty<TSome>() =>
        default;

    /// <summary>Places a value, including null, into the present case; reads as the case it creates.</summary>
    /// <typeparam name="TSome">The type of the present value.</typeparam>
    /// <param name="value">The value to store.</param>
    /// <returns>An option containing <paramref name="value"/>, as from <see cref="Return{TSome}(TSome)"/>.</returns>
    public static Option<TSome> Some<TSome>(TSome value) =>
        Return(value);

    /// <summary>Creates an absent option; reads as the case it creates.</summary>
    /// <typeparam name="TSome">The type of a present value.</typeparam>
    /// <returns>An absent option, as from <see cref="Empty{TSome}"/>.</returns>
    public static Option<TSome> None<TSome>() =>
        Empty<TSome>();
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
            value is null ? new None() : new Option<TSome>.Some(value);
    }

    extension<TSome>(TSome? value)
        where TSome : struct
    {
        /// <summary>Normalizes an absent nullable value at a boundary so subsequent code can compose it as an option.</summary>
        /// <returns>An option containing the value when present; otherwise, <see cref="None"/>.</returns>
        public Option<TSome> ToOption() =>
            value.HasValue ? new Option<TSome>.Some(value.Value) : new None();
    }

    extension<TInput, TOutput>(Option<Func<TInput, TOutput>> functions)
    {
        /// <summary>Applies an optional function to an optional value so either missing input short-circuits the calculation.</summary>
        /// <param name="value">The optional argument to the function.</param>
        /// <returns>The function result when both inputs are present; otherwise, <see cref="None"/>.</returns>
        public Option<TOutput> Apply(Option<TInput> value) =>
            functions switch
            {
                Option<Func<TInput, TOutput>>.Some(var function) => value.Select(function),
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
                Option<TSome>.Some(var some) => new Option<TOut>.Some(selector(some)),
                None none => none
            };

        /// <summary>Composes a computation that may also produce no value, short-circuiting when this option is empty.</summary>
        /// <typeparam name="TOut">The type of the value produced by <paramref name="binder"/>.</typeparam>
        /// <param name="binder">The next computation, invoked only when this option contains a value.</param>
        /// <returns>The result of <paramref name="binder"/>, or <see cref="None"/> without invoking it for an empty option.</returns>
        public Option<TOut> Bind<TOut>(Func<TSome, Option<TOut>> binder) =>
            option switch
            {
                Option<TSome>.Some(var some) => binder(some),
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
                Option<TSome>.Some(var some) => collectionSelector(some) switch
                {
                    Option<TCollection>.Some(var collection) =>
                        new Option<TResult>.Some(resultSelector(some, collection)),
                    None none => none
                },
                None none => none
            };
    }
}
