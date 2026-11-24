using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace LondoSpark.Func;

/// <summary>Collects independent validation errors applicatively instead of failing at the first one; payloads may be nullable.</summary>
/// <typeparam name="TSuccess">The type of a valid value.</typeparam>
/// <typeparam name="TFailure">The type of an individual validation error.</typeparam>
[SuppressMessage(
    "Performance",
    "CA1815:Override equals and operator equals on value types",
    Justification = "The failure case holds a NonEmptyList reference, so structural equality would depend on reference identity.")]
public readonly union Validation<TSuccess, TFailure>(
    Success<TSuccess>,
    Failure<NonEmptyList<TFailure>>);

/// <summary>Provides explicit constructors for successful and failed validation results.</summary>
public static class Validation
{
    /// <summary>Wraps a valid value so it can participate in validation composition.</summary>
    /// <typeparam name="TSuccess">The type of the valid value.</typeparam>
    /// <typeparam name="TFailure">The type of a validation error.</typeparam>
    /// <param name="value">The value that passed validation, which may be null.</param>
    /// <returns>A successful validation containing <paramref name="value"/>.</returns>
    public static Validation<TSuccess, TFailure> Return<TSuccess, TFailure>(TSuccess value) =>
        new Success<TSuccess>(value);

    /// <summary>Creates a failed validation with at least one error, so failures can never be mistaken for an empty error set.</summary>
    /// <typeparam name="TSuccess">The type of a valid value.</typeparam>
    /// <typeparam name="TFailure">The type of an individual validation error.</typeparam>
    /// <param name="firstError">The first validation error, which may be null.</param>
    /// <param name="additionalErrors">Additional errors to accumulate in order.</param>
    /// <returns>A failed validation containing all supplied errors.</returns>
    public static Validation<TSuccess, TFailure> Failure<TSuccess, TFailure>(
        TFailure firstError,
        params TFailure[] additionalErrors) =>
        new Failure<NonEmptyList<TFailure>>(
            new NonEmptyList<TFailure>(firstError, additionalErrors));
}

/// <summary>Stores an ordered collection that always contains at least one item.</summary>
/// <typeparam name="T">The type of each item.</typeparam>
public sealed class NonEmptyList<T> : IReadOnlyList<T>
{
    private readonly T[] items;

    /// <summary>Creates a non-empty list without permitting an empty failure collection.</summary>
    /// <param name="head">The first item.</param>
    /// <param name="tail">Any additional items, which may include null.</param>
    public NonEmptyList(T head, params T[] tail)
    {
        items = new T[tail.Length + 1];
        items[0] = head;
        Array.Copy(tail, 0, items, 1, tail.Length);
    }

    private NonEmptyList(T[] items) =>
        this.items = items;

    /// <summary>Gets the number of items.</summary>
    public int Count => items.Length;

    /// <summary>Gets the item at the specified zero-based position.</summary>
    /// <param name="index">The zero-based item position.</param>
    /// <returns>The item at <paramref name="index"/>.</returns>
    public T this[int index] => items[index];

    /// <summary>Combines two non-empty lists, keeping this list's items before the other list's items.</summary>
    /// <param name="other">The list whose items follow this list.</param>
    /// <returns>A new non-empty list containing both lists in order.</returns>
    public NonEmptyList<T> Append(NonEmptyList<T> other)
    {
        var combined = new T[items.Length + other.items.Length];
        Array.Copy(items, combined, items.Length);
        Array.Copy(other.items, 0, combined, items.Length, other.items.Length);
        return new NonEmptyList<T>(combined);
    }

    /// <summary>Returns an enumerator over the items.</summary>
    /// <returns>An enumerator over this list.</returns>
    public IEnumerator<T> GetEnumerator() =>
        ((IEnumerable<T>)items).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() =>
        items.GetEnumerator();
}

/// <summary>Provides transformations and accumulating application for validation values.</summary>
public static class ValidationExtensions
{
    extension<TSuccess, TFailure>(Validation<TSuccess, TFailure> validation)
    {
        /// <summary>Transforms a valid value while preserving every validation error unchanged.</summary>
        /// <typeparam name="TOut">The transformed success type.</typeparam>
        /// <param name="selector">The transformation applied only to a valid value.</param>
        /// <returns>The transformed success or the original non-empty error list.</returns>
        public Validation<TOut, TFailure> Select<TOut>(Func<TSuccess, TOut> selector) =>
            validation switch
            {
                Success<TSuccess> success => new Success<TOut>(selector(success.Value)),
                Failure<NonEmptyList<TFailure>> errors =>
                    new Failure<NonEmptyList<TFailure>>(errors.Value)
            };
    }

    extension<TInput, TOutput, TFailure>(
        Validation<Func<TInput, TOutput>, TFailure> functions)
    {
        /// <summary>Combines independent validation results, accumulating errors in function-then-value order.</summary>
        /// <param name="value">The validation result containing the function argument.</param>
        /// <returns>The applied success, one error list, or both error lists concatenated in order.</returns>
        public Validation<TOutput, TFailure> Apply(
            Validation<TInput, TFailure> value) =>
            functions switch
            {
                Success<Func<TInput, TOutput>> function =>
                    value switch
                    {
                        Success<TInput> input =>
                            new Success<TOutput>(function.Value(input.Value)),
                        Failure<NonEmptyList<TFailure>> errors =>
                            new Failure<NonEmptyList<TFailure>>(errors.Value)
                    },
                Failure<NonEmptyList<TFailure>> functionErrors => value switch
                {
                    Success<TInput> _ => functionErrors,
                    Failure<NonEmptyList<TFailure>> valueErrors =>
                        new Failure<NonEmptyList<TFailure>>(
                            functionErrors.Value.Append(valueErrors.Value))
                }
            };
    }
}
