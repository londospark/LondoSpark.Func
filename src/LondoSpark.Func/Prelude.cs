using System;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;

namespace LondoSpark.Func;

/// <summary>Lowercase constructors for code that imports them with <c>using static LondoSpark.Func.Prelude;</c>.</summary>
/// <remarks>
/// <para>
/// The prelude reads like the functional libraries many C# developers already know:
/// <code>
/// using static LondoSpark.Func.Prelude;
///
/// Option&lt;Customer&gt; Find(int id) =&gt; customers.TryGetValue(id, out var c) ? some(c) : none;
///
/// Result&lt;Customer, CustomerError&gt; Register(NewCustomer candidate) =&gt;
///     taken ? failure(new AlreadyRegistered(candidate.Email)) : success(Add(candidate));
/// </code>
/// <c>success</c> and <c>failure</c> know only one side of a result, so they return a
/// <see cref="Succeeded{TSuccess}"/> or <see cref="Failed{TFailure}"/> that converts implicitly to whichever
/// <see cref="Result{TSuccess, TFailure}"/> or <see cref="Validation{TSuccess, TValidationError}"/> the context
/// expects. The conversion is a struct copy and does not allocate.
/// </para>
/// <para>
/// Set <c>&lt;LondoSparkFuncPrelude&gt;true&lt;/LondoSparkFuncPrelude&gt;</c> in a project to import the prelude in every
/// file. Matching still names the cases: <c>Success(var c)</c> after <c>using static</c> on the closed result type.
/// </para>
/// </remarks>
[SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "Lowercase prelude constructors are the point of this type.")]
public static class Prelude
{
    /// <summary>The shared absent case, which converts to an absent option of any type.</summary>
    /// <remarks>It is a value, not a call: <c>return found ? some(customer) : none;</c>.</remarks>
    public static readonly None none;

    /// <summary>Places a value, including null, into the present case of an option.</summary>
    /// <typeparam name="TSome">The type of the present value.</typeparam>
    /// <param name="value">The value to store.</param>
    /// <returns>An option containing <paramref name="value"/>.</returns>
    public static Option<TSome> some<TSome>(TSome value) =>
        new Option<TSome>.Some(value);

    /// <summary>Starts a successful result or validation whose failure type the context supplies.</summary>
    /// <typeparam name="TSuccess">The type of the successful value.</typeparam>
    /// <param name="value">The successful value, which may be null.</param>
    /// <returns>A success that converts implicitly to a result or validation over <typeparamref name="TSuccess"/>.</returns>
    public static Succeeded<TSuccess> success<TSuccess>(TSuccess value) =>
        new(value);

    /// <summary>Starts a failed result or validation whose success type the context supplies.</summary>
    /// <typeparam name="TFailure">The type of the failure value.</typeparam>
    /// <param name="error">The failure value, which may be null.</param>
    /// <returns>A failure that converts implicitly to a result over <typeparamref name="TFailure"/>, or a validation with this one error.</returns>
    public static Failed<TFailure> failure<TFailure>(TFailure error) =>
        new(error);

    /// <summary>A success waiting for the context to name its failure type; it converts implicitly to a result or validation.</summary>
    /// <typeparam name="TSuccess">The type of the successful value.</typeparam>
    /// <param name="Value">The successful value.</param>
    public readonly record struct Succeeded<TSuccess>(TSuccess Value);

    /// <summary>A failure waiting for the context to name its success type; it converts implicitly to a result or validation.</summary>
    /// <typeparam name="TFailure">The type of the failure value.</typeparam>
    /// <param name="Value">The failure value.</param>
    public readonly record struct Failed<TFailure>(TFailure Value)
    {
        /// <summary>Lets <c>await failure(error);</c> end a short-circuiting async method with that failure.</summary>
        /// <returns>An awaiter that always short-circuits.</returns>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public FailedAwaiter<TFailure> GetAwaiter() => new(Value);
    }
}
