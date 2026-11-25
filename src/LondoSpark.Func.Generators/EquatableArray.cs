using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace LondoSpark.Func.Generators;

/// <summary>An immutable array compared by its items, so generator models cache between edits.</summary>
/// <typeparam name="T">The item type.</typeparam>
internal readonly struct EquatableArray<T>(ImmutableArray<T> items) : IEquatable<EquatableArray<T>>, IEnumerable<T>
    where T : IEquatable<T>
{
    private readonly ImmutableArray<T> _items = items;

    public bool IsEmpty => _items.IsDefaultOrEmpty;

    public bool Equals(EquatableArray<T> other) =>
        _items.AsSpan().SequenceEqual(other._items.AsSpan());

    public override bool Equals(object? obj) =>
        obj is EquatableArray<T> other && Equals(other);

    public override int GetHashCode()
    {
        var hash = 17;
        foreach (var item in _items.IsDefault ? [] : _items)
        {
            hash = (hash * 31) + item.GetHashCode();
        }

        return hash;
    }

    public IEnumerator<T> GetEnumerator() =>
        ((IEnumerable<T>)(_items.IsDefault ? [] : _items)).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() =>
        GetEnumerator();
}

/// <summary>A location without a syntax tree reference, so diagnostics can be cached with the model.</summary>
internal sealed record LocationInfo(string FilePath, TextSpan Span, LinePositionSpan LineSpan)
{
    public static LocationInfo From(Location location) =>
        new(location.SourceTree?.FilePath ?? string.Empty, location.SourceSpan, location.GetLineSpan().Span);

    public Location ToLocation() =>
        Location.Create(FilePath, Span, LineSpan);
}
