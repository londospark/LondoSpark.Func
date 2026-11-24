using System;
using System.Text.Json;
using System.Text.Json.Serialization;

using LondoSpark.Func;

namespace Examples.JsonSerialization;

/// <summary>Round-trips an option as a discriminated object, so a present null survives.</summary>
/// <typeparam name="T">The present value type.</typeparam>
internal sealed class OptionJsonConverter<T> : JsonConverter<Option<T>>
{
    public override Option<T> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var document = JsonDocument.ParseValue(ref reader);
        var root = document.RootElement;

        return root.TryGetProperty("value", out var payload)
            ? Option.Return(payload.Deserialize<T>(options)!)
            : Option.Empty<T>();
    }

    public override void Write(Utf8JsonWriter writer, Option<T> value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();

        switch (value)
        {
            case Some<T> some:
                writer.WritePropertyName("value");
                JsonSerializer.Serialize(writer, some.Value, options);
                break;
            case None:
                writer.WriteBoolean("none", true);
                break;
        }

        writer.WriteEndObject();
    }
}
