using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LondoSpark.Func.Json;

/// <summary>Serializes <see cref="Option{TSome}"/> as <c>null</c> for none and <c>{"some": value}</c> for some.</summary>
/// <remarks>
/// The object wrapper keeps <c>Some(null)</c> distinct from <c>None</c>, which a bare
/// <c>null</c> cannot do. Reading is streaming: the payload is deserialized directly
/// from the reader without building a document.
/// </remarks>
/// <typeparam name="TSome">The present value type.</typeparam>
public sealed class OptionJsonConverter<TSome> : JsonConverter<Option<TSome>>
{
    private static readonly JsonEncodedText SomeProperty = JsonEncodedText.Encode("some");

    /// <inheritdoc/>
    /// <exception cref="JsonException">The payload is neither <c>null</c> nor a single-property <c>{"some": ...}</c> object.</exception>
    public override Option<TSome> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
        {
            return Option.Empty<TSome>();
        }

        if (reader.TokenType != JsonTokenType.StartObject)
        {
            throw new JsonException("Expected null for none or an object with a 'some' property.");
        }

        reader.Read();

        if (reader.TokenType != JsonTokenType.PropertyName
            || !reader.ValueTextEquals(SomeProperty.EncodedUtf8Bytes))
        {
            throw new JsonException("Expected a 'some' property.");
        }

        reader.Read();
        var value = JsonSerializer.Deserialize<TSome>(ref reader, options)!;

        reader.Read();

        if (reader.TokenType != JsonTokenType.EndObject)
        {
            throw new JsonException("Expected the end of the option object.");
        }

        return Option.Return(value);
    }

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, Option<TSome> value, JsonSerializerOptions options)
    {
        switch (value)
        {
            case Some<TSome> some:
                writer.WriteStartObject();
                writer.WritePropertyName(SomeProperty);
                JsonSerializer.Serialize(writer, some.Value, options);
                writer.WriteEndObject();
                break;
            case None:
                writer.WriteNullValue();
                break;
        }
    }
}
