using Microsoft.FSharp.Core;

namespace LondoSpark.Func.FSharp;

/// <summary>Converts between F# options, F# value options, and the core Option type.</summary>
public static class FSharpOptionExtensions
{
    /// <summary>Converts F# None or Some to the corresponding core option case.</summary>
    /// <typeparam name="T">The option payload type.</typeparam>
    /// <param name="option">The F# option to convert; a null reference represents F# None.</param>
    /// <returns>The equivalent core option, preserving Some null.</returns>
    public static Option<T> ToOption<T>(this FSharpOption<T>? option) =>
        option is null
            ? Option.Empty<T>()
            : Option.Return(option.Value);

    /// <summary>Converts a core option to F# None or Some, preserving Some null.</summary>
    /// <typeparam name="T">The option payload type.</typeparam>
    /// <param name="option">The core option to convert.</param>
    /// <returns>The equivalent F# option.</returns>
    public static FSharpOption<T> ToFSharpOption<T>(this Option<T> option) =>
        option switch
        {
            Option<T>.Some(var some) => FSharpOption<T>.Some(some),
            None => FSharpOption<T>.None
        };

    /// <summary>Converts F# ValueNone or ValueSome to the corresponding core option case without allocating.</summary>
    /// <typeparam name="T">The option payload type.</typeparam>
    /// <param name="option">The F# value option to convert.</param>
    /// <returns>The equivalent core option, preserving ValueSome null.</returns>
    public static Option<T> ToOption<T>(this FSharpValueOption<T> option) =>
        option.IsSome
            ? Option.Return(option.Value)
            : Option.Empty<T>();

    /// <summary>Converts a core option to F# ValueNone or ValueSome without allocating, preserving Some null.</summary>
    /// <typeparam name="T">The option payload type.</typeparam>
    /// <param name="option">The core option to convert.</param>
    /// <returns>The equivalent F# value option.</returns>
    public static FSharpValueOption<T> ToFSharpValueOption<T>(this Option<T> option) =>
        option switch
        {
            Option<T>.Some(var some) => FSharpValueOption<T>.NewValueSome(some),
            None => FSharpValueOption<T>.ValueNone
        };
}
