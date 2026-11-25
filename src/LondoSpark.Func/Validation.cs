using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;

namespace LondoSpark.Func;

/// <summary>Collects independent validation errors applicatively instead of failing at the first one; payloads may be nullable.</summary>
/// <typeparam name="TSuccess">The type of a valid value.</typeparam>
/// <typeparam name="TValidationError">The type of an individual validation error.</typeparam>
/// <remarks>
/// The cases are the nested <see cref="Success"/> and <see cref="Failure"/>; after
/// <c>using static LondoSpark.Func.Validation&lt;Customer, ValidationError&gt;;</c> a switch reads
/// <c>Success(var customer) => ..., Failure(var errors) => ...</c>. Every value created by <c>Validation.Return</c>,
/// <c>Validation.Failure</c>, or a case contains a case. Because this is a
/// struct, <see langword="default"/> and <c>new()</c> produce a zero value that contains no case: <see cref="HasValue"/>
/// is <see langword="false"/>, the case patterns do not match, and <see cref="Value"/> and every extension method throw
/// <see cref="InvalidOperationException"/>.
/// Cases are stored inline, so constructing and transforming a validation does not box. Equality compares the active
/// case; failures compare their <see cref="NonEmptyList{T}"/> by reference, because the list does not define
/// structural equality.
/// </remarks>
[Union]
public readonly struct Validation<TSuccess, TValidationError> : IEquatable<Validation<TSuccess, TValidationError>>
{
    private const byte SuccessTag = 1;
    private const byte FailureTag = 2;

    private readonly TSuccess _success;
    private readonly NonEmptyList<TValidationError>? _errors;
    private readonly byte _tag;

    /// <summary>Creates the valid case.</summary>
    /// <param name="value">The valid case, whose payload may be null.</param>
    public Validation(Success value)
    {
        _success = value.Value;
        _tag = SuccessTag;
    }

    /// <summary>Creates the invalid case containing every accumulated error.</summary>
    /// <param name="value">The invalid case.</param>
    public Validation(Failure value)
    {
        _success = default!;
        _errors = value.Errors;
        _tag = FailureTag;
    }

    /// <summary>Gets the boxed active case; the union contract requires this <c>object</c> property, so reading it boxes.</summary>
    /// <exception cref="InvalidOperationException">This validation is <see langword="default"/> or <c>new()</c> and contains no case.</exception>
    public object Value =>
        _tag switch
        {
            SuccessTag => new Success(_success),
            FailureTag => new Failure(_errors!),
            _ => throw NoCase.Validation()
        };

    /// <summary>Gets whether this validation contains a case; <see langword="false"/> only for <see langword="default"/>.</summary>
    public bool HasValue => _tag is SuccessTag or FailureTag;

    /// <summary>Gets the valid case when this validation contains one.</summary>
    /// <param name="value">Receives the valid case, or its default value when this validation does not contain it.</param>
    /// <returns><see langword="true"/> when this validation contains a valid case.</returns>
    public bool TryGetValue(out Success value)
    {
        value = new Success(_success);
        return _tag == SuccessTag;
    }

    /// <summary>Gets the invalid case when this validation contains one.</summary>
    /// <param name="value">Receives the invalid case, or its default value when this validation does not contain it.</param>
    /// <returns><see langword="true"/> when this validation contains an invalid case.</returns>
    public bool TryGetValue(out Failure value)
    {
        value = new Failure(_errors!);
        return _tag == FailureTag;
    }

    /// <summary>Determines whether this validation has the same case and payload as <paramref name="other"/>.</summary>
    /// <param name="other">The validation to compare.</param>
    /// <returns><see langword="true"/> for equal successes, the same error list, or two default values.</returns>
    public bool Equals(Validation<TSuccess, TValidationError> other) =>
        _tag == other._tag
        && (_tag switch
        {
            SuccessTag => EqualityComparer<TSuccess>.Default.Equals(_success, other._success),
            FailureTag => ReferenceEquals(_errors, other._errors),
            _ => true
        });

    /// <inheritdoc/>
    public override bool Equals(object? obj) =>
        obj is Validation<TSuccess, TValidationError> other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() =>
        _tag switch
        {
            SuccessTag => HashCode.Combine(SuccessTag, _success),
            FailureTag => HashCode.Combine(FailureTag, RuntimeHelpers.GetHashCode(_errors)),
            _ => _tag
        };

    /// <summary>Determines whether two validations are equal.</summary>
    /// <param name="left">The first validation.</param>
    /// <param name="right">The second validation.</param>
    /// <returns><see langword="true"/> when both validations are equal.</returns>
    public static bool operator ==(Validation<TSuccess, TValidationError> left, Validation<TSuccess, TValidationError> right) =>
        left.Equals(right);

    /// <summary>Determines whether two validations are not equal.</summary>
    /// <param name="left">The first validation.</param>
    /// <param name="right">The second validation.</param>
    /// <returns><see langword="true"/> when the validations differ.</returns>
    public static bool operator !=(Validation<TSuccess, TValidationError> left, Validation<TSuccess, TValidationError> right) =>
        !left.Equals(right);

    /// <summary>Renders the active case so valid and invalid inputs are visible in logs.</summary>
    /// <returns><c>Success(value)</c>, <c>Failure(error, ...)</c>, or <c>Validation(default)</c> when there is no case.</returns>
    public override string ToString() =>
        _tag switch
        {
            SuccessTag => $"Success({_success})",
            FailureTag => $"Failure({_errors!.ToSeparatedString(", ")})",
            _ => "Validation(default)"
        };

    /// <summary>Completes a prelude <c>success(value)</c> once the context names the error type.</summary>
    /// <param name="value">The pending success.</param>
    /// <returns>A valid validation containing the value.</returns>
    public static implicit operator Validation<TSuccess, TValidationError>(Prelude.Succeeded<TSuccess> value) =>
        new Success(value.Value);

    /// <summary>Completes a prelude <c>failure(error)</c> as an invalid validation with that one error.</summary>
    /// <param name="value">The pending failure.</param>
    /// <returns>An invalid validation containing the error.</returns>
    public static implicit operator Validation<TSuccess, TValidationError>(Prelude.Failed<TValidationError> value) =>
        new Failure(new NonEmptyList<TValidationError>(value.Value));

    /// <summary>The valid case of <see cref="Validation{TSuccess, TValidationError}"/>, holding a value that may be null.</summary>
    /// <param name="Value">The valid value.</param>
    public readonly record struct Success(TSuccess Value);

    /// <summary>The invalid case of <see cref="Validation{TSuccess, TValidationError}"/>, holding every accumulated error in order.</summary>
    /// <param name="Errors">The non-empty error list.</param>
    public readonly record struct Failure(NonEmptyList<TValidationError> Errors);
}

/// <summary>Provides explicit constructors for successful and failed validation results.</summary>
public static class Validation
{
    /// <summary>Wraps a valid value so it can participate in validation composition.</summary>
    /// <typeparam name="TSuccess">The type of the valid value.</typeparam>
    /// <typeparam name="TValidationError">The type of a validation error.</typeparam>
    /// <param name="value">The value that passed validation, which may be null.</param>
    /// <returns>A successful validation containing <paramref name="value"/>.</returns>
    public static Validation<TSuccess, TValidationError> Return<TSuccess, TValidationError>(TSuccess value) =>
        new Validation<TSuccess, TValidationError>.Success(value);

    /// <summary>Creates a failed validation with at least one error, so failures can never be mistaken for an empty error set.</summary>
    /// <typeparam name="TSuccess">The type of a valid value.</typeparam>
    /// <typeparam name="TValidationError">The type of an individual validation error.</typeparam>
    /// <param name="firstError">The first validation error, which may be null.</param>
    /// <param name="additionalErrors">Additional errors to accumulate in order.</param>
    /// <returns>A failed validation containing all supplied errors.</returns>
    public static Validation<TSuccess, TValidationError> Failure<TSuccess, TValidationError>(
        TValidationError firstError,
        params ReadOnlySpan<TValidationError> additionalErrors) =>
        new Validation<TSuccess, TValidationError>.Failure(
            new NonEmptyList<TValidationError>(firstError, additionalErrors));
}

/// <summary>Stores an immutable, ordered collection that always contains at least one item.</summary>
/// <typeparam name="T">The type of each item.</typeparam>
/// <remarks>
/// <see cref="Append"/> reuses spare capacity when the receiver is the newest list over its buffer, so a chain of
/// applicative steps accumulates errors in amortised linear time instead of copying every earlier error at each step.
/// Items visible through an existing list are never overwritten, so every list stays immutable and safe to share.
/// </remarks>
public sealed class NonEmptyList<T> : IReadOnlyList<T>
{
    private readonly T[] _items;
    private readonly int _count;

    // Shared by every list over a growable buffer: the number of slots claimed so far. Null for an exact-size
    // buffer, which has no spare capacity to claim.
    private readonly StrongBox<int>? _claimed;

    /// <summary>Creates a non-empty list without permitting an empty failure collection.</summary>
    /// <param name="head">The first item.</param>
    /// <param name="tail">Any additional items, which may include null.</param>
    public NonEmptyList(T head, params ReadOnlySpan<T> tail)
    {
        _items = new T[tail.Length + 1];
        _items[0] = head;
        tail.CopyTo(_items.AsSpan(1));
        _count = _items.Length;
    }

    private NonEmptyList(T[] items, int count, StrongBox<int> claimed)
    {
        _items = items;
        _count = count;
        _claimed = claimed;
    }

    /// <summary>Gets the number of items.</summary>
    public int Count => _count;

    /// <summary>Gets the item at the specified zero-based position.</summary>
    /// <param name="index">The zero-based item position.</param>
    /// <returns>The item at <paramref name="index"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="index"/> is negative or not less than <see cref="Count"/>.</exception>
    public T this[int index]
    {
        get
        {
            ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual((uint)index, (uint)_count, nameof(index));
            return _items[index];
        }
    }

    /// <summary>Gets the items as a span, for allocation-free iteration and slicing.</summary>
    /// <returns>A read-only view of every item in order.</returns>
    public ReadOnlySpan<T> AsSpan() =>
        new(_items, 0, _count);

    /// <summary>Combines two non-empty lists, keeping this list's items before the other list's items.</summary>
    /// <param name="other">The list whose items follow this list.</param>
    /// <returns>A new non-empty list containing both lists in order; neither input changes.</returns>
    public NonEmptyList<T> Append(NonEmptyList<T> other)
    {
        var combinedCount = _count + other._count;

        // Claim the slots after this list when no other list has claimed them yet. The compare-exchange makes the
        // claim exclusive, so two appends to the same list never write the same slots.
        if (_claimed is { } claimed
            && combinedCount <= _items.Length
            && Interlocked.CompareExchange(ref claimed.Value, combinedCount, _count) == _count)
        {
            other.AsSpan().CopyTo(_items.AsSpan(_count));
            return new NonEmptyList<T>(_items, combinedCount, claimed);
        }

        var grown = new T[Math.Max(combinedCount, _count * 2)];
        AsSpan().CopyTo(grown);
        other.AsSpan().CopyTo(grown.AsSpan(_count));
        return new NonEmptyList<T>(grown, combinedCount, new StrongBox<int>(combinedCount));
    }

    /// <summary>Joins every item with <paramref name="separator"/>, which reads natural for validation messages.</summary>
    /// <param name="separator">The text placed between items.</param>
    /// <returns>The joined items.</returns>
    public string ToSeparatedString(string separator)
    {
        var items = AsSpan();
        var builder = new DefaultInterpolatedStringHandler(separator.Length * (items.Length - 1), items.Length);
        builder.AppendFormatted(items[0]);

        for (var index = 1; index < items.Length; index++)
        {
            builder.AppendLiteral(separator);
            builder.AppendFormatted(items[index]);
        }

        return builder.ToStringAndClear();
    }

    /// <summary>Returns an allocation-free enumerator over the items.</summary>
    /// <returns>An enumerator over this list.</returns>
    public Enumerator GetEnumerator() =>
        new(_items, _count);

    IEnumerator<T> IEnumerable<T>.GetEnumerator() =>
        new BoxedEnumerator(GetEnumerator());

    IEnumerator IEnumerable.GetEnumerator() =>
        new BoxedEnumerator(GetEnumerator());

    /// <summary>Enumerates a <see cref="NonEmptyList{T}"/> without allocating.</summary>
    public struct Enumerator
    {
        private readonly T[] _items;
        private readonly int _count;
        private int _index;

        internal Enumerator(T[] items, int count)
        {
            _items = items;
            _count = count;
            _index = -1;
        }

        /// <summary>Gets the item at the current position.</summary>
        public readonly T Current => _items[_index];

        /// <summary>Advances to the next item.</summary>
        /// <returns><see langword="true"/> while an item remains.</returns>
        public bool MoveNext() =>
            ++_index < _count;
    }

    private sealed class BoxedEnumerator(Enumerator enumerator) : IEnumerator<T>
    {
        private Enumerator _enumerator = enumerator;

        public T Current => _enumerator.Current;

        object? IEnumerator.Current => Current;

        public bool MoveNext() => _enumerator.MoveNext();

        public void Reset() => throw new NotSupportedException();

        public void Dispose()
        {
        }
    }
}

/// <summary>Provides transformations and accumulating application for validation values.</summary>
public static class ValidationExtensions
{
    extension<TSuccess, TValidationError>(Validation<TSuccess, TValidationError> validation)
    {
        /// <summary>Transforms a valid value while preserving every validation error unchanged.</summary>
        /// <typeparam name="TOut">The transformed success type.</typeparam>
        /// <param name="selector">The transformation applied only to a valid value.</param>
        /// <returns>The transformed success or the original non-empty error list.</returns>
        public Validation<TOut, TValidationError> Select<TOut>(Func<TSuccess, TOut> selector) =>
            validation switch
            {
                Validation<TSuccess, TValidationError>.Success(var success) =>
                    new Validation<TOut, TValidationError>.Success(selector(success)),
                Validation<TSuccess, TValidationError>.Failure(var errors) =>
                    new Validation<TOut, TValidationError>.Failure(errors),
                null => throw NoCase.Validation()
            };
    }

    extension<TInput, TOutput, TValidationError>(
        Validation<Func<TInput, TOutput>, TValidationError> functions)
    {
        /// <summary>Combines independent validation results, accumulating errors in function-then-value order.</summary>
        /// <param name="value">The validation result containing the function argument.</param>
        /// <returns>The applied success, one error list, or both error lists concatenated in order.</returns>
        public Validation<TOutput, TValidationError> Apply(
            Validation<TInput, TValidationError> value) =>
            functions switch
            {
                Validation<Func<TInput, TOutput>, TValidationError>.Success(var function) => value switch
                {
                    Validation<TInput, TValidationError>.Success(var input) =>
                        new Validation<TOutput, TValidationError>.Success(function(input)),
                    Validation<TInput, TValidationError>.Failure(var errors) =>
                        new Validation<TOutput, TValidationError>.Failure(errors),
                    null => throw NoCase.Validation()
                },
                Validation<Func<TInput, TOutput>, TValidationError>.Failure(var functionErrors) => value switch
                {
                    Validation<TInput, TValidationError>.Success =>
                        new Validation<TOutput, TValidationError>.Failure(functionErrors),
                    Validation<TInput, TValidationError>.Failure(var valueErrors) =>
                        new Validation<TOutput, TValidationError>.Failure(functionErrors.Append(valueErrors)),
                    null => throw NoCase.Validation()
                },
                null => throw NoCase.Validation()
            };
    }
}
