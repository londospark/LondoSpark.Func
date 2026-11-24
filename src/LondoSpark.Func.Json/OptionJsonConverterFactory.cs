using System;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LondoSpark.Func.Json;

/// <summary>Registers <see cref="OptionJsonConverter{TSome}"/> for every closed <see cref="Option{TSome}"/> type.</summary>
public sealed class OptionJsonConverterFactory : JsonConverterFactory
{
    /// <summary>Creates a factory that builds converters for closed generic types at run time.</summary>
    /// <remarks>
    /// The factory closes its converter over runtime type arguments, which Native AOT cannot guarantee. For AOT or
    /// trimmed applications, register a closed converter per type instead, for example with
    /// <see cref="JsonSerializerOptionsExtensions.AddOptionConverter{TSome}(JsonSerializerOptions)"/>.
    /// </remarks>
    [RequiresDynamicCode(JsonSerializerOptionsExtensions.DynamicCodeMessage)]
    [RequiresUnreferencedCode(JsonSerializerOptionsExtensions.UnreferencedCodeMessage)]
    public OptionJsonConverterFactory()
    {
    }

    /// <inheritdoc/>
    public override bool CanConvert(Type typeToConvert) =>
        typeToConvert.IsGenericType
        && typeToConvert.GetGenericTypeDefinition() == typeof(Option<>);

    /// <inheritdoc/>
    [UnconditionalSuppressMessage("AOT", "IL3050", Justification = "The constructor carries RequiresDynamicCode.")]
    [UnconditionalSuppressMessage("Trimming", "IL2055", Justification = "The constructor carries RequiresUnreferencedCode.")]
    [UnconditionalSuppressMessage("Trimming", "IL2071", Justification = "The constructor carries RequiresUnreferencedCode.")]
    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options) =>
        (JsonConverter)Activator.CreateInstance(
            typeof(OptionJsonConverter<>).MakeGenericType(typeToConvert.GetGenericArguments()))!;
}
