using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LondoSpark.Func.Json;

/// <summary>Registers <see cref="OptionJsonConverter{TSome}"/> for every closed <see cref="Option{TSome}"/> type.</summary>
public sealed class OptionJsonConverterFactory : JsonConverterFactory
{
    /// <inheritdoc/>
    public override bool CanConvert(Type typeToConvert) =>
        typeToConvert.IsGenericType
        && typeToConvert.GetGenericTypeDefinition() == typeof(Option<>);

    /// <inheritdoc/>
    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options) =>
        (JsonConverter)Activator.CreateInstance(
            typeof(OptionJsonConverter<>).MakeGenericType(typeToConvert.GetGenericArguments()))!;
}
