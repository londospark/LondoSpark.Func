using System;
using System.Text.Json;

namespace LondoSpark.Func.Json;

/// <summary>A case property name resolved against a serializer configuration.</summary>
/// <remarks>
/// The name honours <see cref="JsonSerializerOptions.PropertyNamingPolicy"/> on write and
/// <see cref="JsonSerializerOptions.PropertyNameCaseInsensitive"/> on read, so the wrapper object follows the
/// same conventions as the caller's own types.
/// </remarks>
internal readonly struct JsonCaseName
{
    // Longer property names cannot be a case name, so they are rejected without unescaping.
    private const int MaxComparedLength = 128;

    private readonly string _name;
    private readonly bool _ignoreCase;

    internal JsonCaseName(string name, JsonSerializerOptions options)
    {
        _name = options.PropertyNamingPolicy?.ConvertName(name) ?? name;
        _ignoreCase = options.PropertyNameCaseInsensitive;
        Encoded = JsonEncodedText.Encode(_name, options.Encoder);
    }

    /// <summary>Gets the pre-encoded name to write.</summary>
    internal JsonEncodedText Encoded { get; }

    /// <summary>Determines whether the property name under <paramref name="reader"/> is this case name.</summary>
    /// <param name="reader">A reader positioned on a property name.</param>
    /// <returns><see langword="true"/> when the names match under the configured comparison.</returns>
    internal bool Matches(ref readonly Utf8JsonReader reader)
    {
        if (reader.ValueTextEquals(Encoded.EncodedUtf8Bytes))
            return true;

        if (!_ignoreCase)
            return false;

        var length = reader.HasValueSequence ? reader.ValueSequence.Length : reader.ValueSpan.Length;
        if (length > MaxComparedLength)
            return false;

        // Unescaped UTF-16 is never longer than the UTF-8 source, so the buffer always fits.
        Span<char> buffer = stackalloc char[MaxComparedLength];
        var written = reader.CopyString(buffer);
        return buffer[..written].Equals(_name, StringComparison.OrdinalIgnoreCase);
    }
}
