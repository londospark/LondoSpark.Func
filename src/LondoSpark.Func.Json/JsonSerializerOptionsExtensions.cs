using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace LondoSpark.Func.Json;

/// <summary>Registers the union converters on a serializer configuration.</summary>
public static class JsonSerializerOptionsExtensions
{
    internal const string DynamicCodeMessage =
        "The converter factory closes generic converters over runtime types. Under Native AOT, register closed converters with AddOptionConverter<T> and AddResultConverter<TSuccess, TFailure> instead.";

    internal const string UnreferencedCodeMessage =
        "The converter factory constructs generic converters by reflection. In trimmed applications, register closed converters with AddOptionConverter<T> and AddResultConverter<TSuccess, TFailure> instead.";

    /// <summary>Adds converters for every closed <see cref="Option{TSome}"/> and <see cref="Result{TSuccess, TFailure}"/>.</summary>
    /// <param name="options">The options to configure.</param>
    /// <returns>The same options, so registration can be chained.</returns>
    /// <remarks>This uses runtime generic instantiation; Native AOT applications should register closed converters instead.</remarks>
    [RequiresDynamicCode(DynamicCodeMessage)]
    [RequiresUnreferencedCode(UnreferencedCodeMessage)]
    public static JsonSerializerOptions AddLondoSparkFuncConverters(this JsonSerializerOptions options)
    {
        options.Converters.Add(new OptionJsonConverterFactory());
        options.Converters.Add(new ResultJsonConverterFactory());
        return options;
    }

    /// <summary>Adds the converter for one closed <see cref="Option{TSome}"/> type without reflection, for Native AOT and trimming.</summary>
    /// <typeparam name="TSome">The present value type.</typeparam>
    /// <param name="options">The options to configure.</param>
    /// <returns>The same options, so registration can be chained.</returns>
    public static JsonSerializerOptions AddOptionConverter<TSome>(this JsonSerializerOptions options)
    {
        options.Converters.Add(new OptionJsonConverter<TSome>());
        return options;
    }

    /// <summary>Adds the converter for one closed <see cref="Result{TSuccess, TFailure}"/> type without reflection, for Native AOT and trimming.</summary>
    /// <typeparam name="TSuccess">The success value type.</typeparam>
    /// <typeparam name="TFailure">The failure value type.</typeparam>
    /// <param name="options">The options to configure.</param>
    /// <returns>The same options, so registration can be chained.</returns>
    public static JsonSerializerOptions AddResultConverter<TSuccess, TFailure>(this JsonSerializerOptions options)
    {
        options.Converters.Add(new ResultJsonConverter<TSuccess, TFailure>());
        return options;
    }
}
