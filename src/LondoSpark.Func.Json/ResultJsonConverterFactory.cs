using System;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LondoSpark.Func.Json;

/// <summary>Registers <see cref="ResultJsonConverter{TSuccess, TFailure}"/> for every closed <see cref="Result{TSuccess, TFailure}"/> type.</summary>
public sealed class ResultJsonConverterFactory : JsonConverterFactory
{
    /// <summary>Creates a factory that builds converters for closed generic types at run time.</summary>
    /// <remarks>
    /// The factory closes its converter over runtime type arguments, which Native AOT cannot guarantee. For AOT or
    /// trimmed applications, register a closed converter per type instead, for example with
    /// <see cref="JsonSerializerOptionsExtensions.AddResultConverter{TSuccess, TFailure}(JsonSerializerOptions)"/>.
    /// </remarks>
    [RequiresDynamicCode(JsonSerializerOptionsExtensions.DynamicCodeMessage)]
    [RequiresUnreferencedCode(JsonSerializerOptionsExtensions.UnreferencedCodeMessage)]
    public ResultJsonConverterFactory()
    {
    }

    /// <inheritdoc/>
    public override bool CanConvert(Type typeToConvert) =>
        typeToConvert.IsGenericType
        && typeToConvert.GetGenericTypeDefinition() == typeof(Result<,>);

    /// <inheritdoc/>
    [UnconditionalSuppressMessage("AOT", "IL3050", Justification = "The constructor carries RequiresDynamicCode.")]
    [UnconditionalSuppressMessage("Trimming", "IL2055", Justification = "The constructor carries RequiresUnreferencedCode.")]
    [UnconditionalSuppressMessage("Trimming", "IL2071", Justification = "The constructor carries RequiresUnreferencedCode.")]
    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options) =>
        (JsonConverter)Activator.CreateInstance(
            typeof(ResultJsonConverter<,>).MakeGenericType(typeToConvert.GetGenericArguments()))!;
}
