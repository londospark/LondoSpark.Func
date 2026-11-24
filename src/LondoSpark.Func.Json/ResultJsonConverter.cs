using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace LondoSpark.Func.Json;

/// <summary>Serializes <see cref="Result{TSuccess, TFailure}"/> as <c>{"success": value}</c> or <c>{"failure": error}</c>.</summary>
/// <remarks>
/// Reading is streaming: the payload is deserialized directly from the reader without building a document.
/// Property names follow the configured naming policy and case sensitivity, and payload type metadata is
/// resolved once per serializer configuration rather than on every call.
/// </remarks>
/// <typeparam name="TSuccess">The success value type.</typeparam>
/// <typeparam name="TFailure">The failure value type.</typeparam>
public sealed class ResultJsonConverter<TSuccess, TFailure> : JsonConverter<Result<TSuccess, TFailure>>
{
    private State? _state;

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

        var state = GetState(options);
        reader.Read();

        if (reader.TokenType != JsonTokenType.PropertyName)
        {
            throw new JsonException("Expected a 'success' or 'failure' property.");
        }

        Result<TSuccess, TFailure> result;
        if (state.Success.Matches(ref reader))
        {
            reader.Read();
            result = Result.Return<TSuccess, TFailure>(
                JsonSerializer.Deserialize(ref reader, state.SuccessTypeInfo)!);
        }
        else if (state.Failure.Matches(ref reader))
        {
            reader.Read();
            result = Result.Failure<TSuccess, TFailure>(
                JsonSerializer.Deserialize(ref reader, state.FailureTypeInfo)!);
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
    /// <exception cref="JsonException"><paramref name="value"/> is <see langword="default"/> and contains no case.</exception>
    public override void Write(
        Utf8JsonWriter writer,
        Result<TSuccess, TFailure> value,
        JsonSerializerOptions options)
    {
        var state = GetState(options);

        switch (value)
        {
            case Result<TSuccess, TFailure>.Success(var success):
                writer.WriteStartObject();
                writer.WritePropertyName(state.Success.Encoded);
                JsonSerializer.Serialize(writer, success, state.SuccessTypeInfo);
                writer.WriteEndObject();
                break;
            case Result<TSuccess, TFailure>.Failure(var failure):
                writer.WriteStartObject();
                writer.WritePropertyName(state.Failure.Encoded);
                JsonSerializer.Serialize(writer, failure, state.FailureTypeInfo);
                writer.WriteEndObject();
                break;
            default:
                throw new JsonException("A default result has no case to serialize.");
        }
    }

    // See OptionJsonConverter: the cache is keyed by the options it was built from.
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

        public JsonTypeInfo<TSuccess> SuccessTypeInfo { get; } = options.GetTypeInfo<TSuccess>();

        public JsonTypeInfo<TFailure> FailureTypeInfo { get; } = options.GetTypeInfo<TFailure>();

        public JsonCaseName Success { get; } = new("success", options);

        public JsonCaseName Failure { get; } = new("failure", options);
    }
}
