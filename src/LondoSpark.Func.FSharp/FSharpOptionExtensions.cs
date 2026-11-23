using LondoSpark.Func;
using Microsoft.FSharp.Core;

namespace LondoSpark.Func.FSharp;

/// <summary>Converts between F# options and the core Option type.</summary>
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
            Some<T> some => FSharpOption<T>.Some(some.Value),
            None => FSharpOption<T>.None
        };
}
