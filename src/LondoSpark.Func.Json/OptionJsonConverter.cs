using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace LondoSpark.Func.Json;

/// <summary>Serializes <see cref="Option{TSome}"/> as <c>null</c> for none and <c>{"some": value}</c> for some.</summary>
/// <remarks>
/// The object wrapper keeps <c>Some(null)</c> distinct from <c>None</c>, which a bare
/// <c>null</c> cannot do. Reading is streaming: the payload is deserialized directly
/// from the reader without building a document. The property name follows the configured
/// naming policy and case sensitivity, and the payload's type metadata is resolved once per
/// serializer configuration rather than on every call.
/// </remarks>
/// <typeparam name="TSome">The present value type.</typeparam>
public sealed class OptionJsonConverter<TSome> : JsonConverter<Option<TSome>>
{
    private State? _state;

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

        var state = GetState(options);
        reader.Read();

        if (reader.TokenType != JsonTokenType.PropertyName || !state.Some.Matches(ref reader))
        {
            throw new JsonException("Expected a 'some' property.");
        }

        reader.Read();
        var value = JsonSerializer.Deserialize(ref reader, state.TypeInfo)!;

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
            case Option<TSome>.Some(var some):
                var state = GetState(options);
                writer.WriteStartObject();
                writer.WritePropertyName(state.Some.Encoded);
                JsonSerializer.Serialize(writer, some, state.TypeInfo);
                writer.WriteEndObject();
                break;
            case None:
                writer.WriteNullValue();
                break;
        }
    }

    // A converter built by the factory serves one configuration, but a hand-registered instance may be shared, so the
    // cache is keyed by the options it was built from. Racing writers store equivalent states.
    private State GetState(JsonSerializerOptions options)
    {
        var state = _state;
        if (state is null || !ReferenceEquals(state.Options, options))
        {
            _state = state = new State(options);
        }

        return state;
    }

    private sealed class State(JsonSerializerOptions options)
    {
        public JsonSerializerOptions Options { get; } = options;

        public JsonTypeInfo<TSome> TypeInfo { get; } = options.GetTypeInfo<TSome>();

        public JsonCaseName Some { get; } = new("some", options);
    }
}
