using System.Text.Json;

namespace LondoSpark.Func.Json;

/// <summary>Registers the union converters on a serializer configuration.</summary>
public static class JsonSerializerOptionsExtensions
{
    /// <summary>Adds converters for <see cref="Option{TSome}"/> and <see cref="Result{TSuccess, TFailure}"/>.</summary>
    /// <param name="options">The options to configure.</param>
    /// <returns>The same options, so registration can be chained.</returns>
    public static JsonSerializerOptions AddLondoSparkFuncConverters(this JsonSerializerOptions options)
    {
        options.Converters.Add(new OptionJsonConverterFactory());
        options.Converters.Add(new ResultJsonConverterFactory());
        return options;
    }
}
