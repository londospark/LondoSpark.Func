using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LondoSpark.Func.Json;

/// <summary>Serializes <see cref="Result{TSuccess, TFailure}"/> as <c>{"success": value}</c> or <c>{"failure": error}</c>.</summary>
/// <remarks>Reading is streaming: the payload is deserialized directly from the reader without building a document.</remarks>
/// <typeparam name="TSuccess">The success value type.</typeparam>
/// <typeparam name="TFailure">The failure value type.</typeparam>
public sealed class ResultJsonConverter<TSuccess, TFailure> : JsonConverter<Result<TSuccess, TFailure>>
{
    private static readonly JsonEncodedText SuccessProperty = JsonEncodedText.Encode("success");
    private static readonly JsonEncodedText FailureProperty = JsonEncodedText.Encode("failure");

    /// <inheritdoc/>
    /// <exception cref="JsonException">The payload is not a single-property object with a success or failure property.</exception>
    public override Result<TSuccess, TFailure> Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
        {
            throw new JsonException("Expected an object with a 'success' or 'failure' property.");
        }

        reader.Read();

        if (reader.TokenType != JsonTokenType.PropertyName)
        {
            throw new JsonException("Expected a 'success' or 'failure' property.");
        }

        Result<TSuccess, TFailure> result;
        if (reader.ValueTextEquals(SuccessProperty.EncodedUtf8Bytes))
        {
            reader.Read();
            result = Result.Return<TSuccess, TFailure>(
                JsonSerializer.Deserialize<TSuccess>(ref reader, options)!);
        }
        else if (reader.ValueTextEquals(FailureProperty.EncodedUtf8Bytes))
        {
            reader.Read();
            result = Result.Failure<TSuccess, TFailure>(
                JsonSerializer.Deserialize<TFailure>(ref reader, options)!);
        }
        else
        {
            throw new JsonException("Expected a 'success' or 'failure' property.");
        }

        reader.Read();

        if (reader.TokenType != JsonTokenType.EndObject)
        {
            throw new JsonException("Expected the end of the result object.");
        }

        return result;
    }

    /// <inheritdoc/>
    public override void Write(
        Utf8JsonWriter writer,
        Result<TSuccess, TFailure> value,
        JsonSerializerOptions options)
    {
        switch (value)
        {
            case Success<TSuccess> success:
                writer.WriteStartObject();
                writer.WritePropertyName(SuccessProperty);
                JsonSerializer.Serialize(writer, success.Value, options);
                writer.WriteEndObject();
                break;
            case Failure<TFailure> failure:
                writer.WriteStartObject();
                writer.WritePropertyName(FailureProperty);
                JsonSerializer.Serialize(writer, failure.Value, options);
                writer.WriteEndObject();
                break;
            default:
                throw new JsonException("A default result has no case to serialize.");
        }
    }
}
