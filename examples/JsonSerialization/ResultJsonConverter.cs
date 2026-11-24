using System;
using System.Text.Json;
using System.Text.Json.Serialization;

using LondoSpark.Func;

namespace Examples.JsonSerialization;

/// <summary>Round-trips a result as a discriminated object, keeping success and failure distinct.</summary>
/// <typeparam name="TSuccess">The success value type.</typeparam>
/// <typeparam name="TFailure">The failure value type.</typeparam>
internal sealed class ResultJsonConverter<TSuccess, TFailure> : JsonConverter<Result<TSuccess, TFailure>>
{
    public override Result<TSuccess, TFailure> Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        using var document = JsonDocument.ParseValue(ref reader);
        var root = document.RootElement;

        if (root.TryGetProperty("success", out var success))
        {
            return Result.Return<TSuccess, TFailure>(success.Deserialize<TSuccess>(options)!);
        }

        if (root.TryGetProperty("failure", out var failure))
        {
            return Result.Failure<TSuccess, TFailure>(failure.Deserialize<TFailure>(options)!);
        }

        throw new JsonException("Expected a success or failure result.");
    }

    public override void Write(
        Utf8JsonWriter writer,
        Result<TSuccess, TFailure> value,
        JsonSerializerOptions options)
    {
        writer.WriteStartObject();

        switch (value)
        {
            case Success<TSuccess> success:
                writer.WritePropertyName("success");
                JsonSerializer.Serialize(writer, success.Value, options);
                break;
            case Failure<TFailure> failure:
                writer.WritePropertyName("failure");
                JsonSerializer.Serialize(writer, failure.Value, options);
                break;
        }

        writer.WriteEndObject();
    }
}
