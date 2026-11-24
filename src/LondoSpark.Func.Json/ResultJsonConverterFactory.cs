using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LondoSpark.Func.Json;

/// <summary>Registers <see cref="ResultJsonConverter{TSuccess, TFailure}"/> for every closed <see cref="Result{TSuccess, TFailure}"/> type.</summary>
public sealed class ResultJsonConverterFactory : JsonConverterFactory
{
    /// <inheritdoc/>
    public override bool CanConvert(Type typeToConvert) =>
        typeToConvert.IsGenericType
        && typeToConvert.GetGenericTypeDefinition() == typeof(Result<,>);

    /// <inheritdoc/>
    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options) =>
        (JsonConverter)Activator.CreateInstance(
            typeof(ResultJsonConverter<,>).MakeGenericType(typeToConvert.GetGenericArguments()))!;
}
