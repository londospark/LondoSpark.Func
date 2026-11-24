using System;

using Microsoft.FSharp.Core;

namespace LondoSpark.Func.FSharp;

/// <summary>Converts between F# Result and the core Result type.</summary>
public static class FSharpResultExtensions
{
    /// <summary>Converts F# Ok or Error to the corresponding core result case.</summary>
    /// <typeparam name="TSuccess">The success payload type.</typeparam>
    /// <typeparam name="TFailure">The failure payload type.</typeparam>
    /// <param name="result">The F# result to convert.</param>
    /// <returns>The equivalent core result, preserving its case and payload.</returns>
    public static Result<TSuccess, TFailure> ToResult<TSuccess, TFailure>(
        this FSharpResult<TSuccess, TFailure> result) =>
        result.IsOk
            ? Result.Return<TSuccess, TFailure>(result.ResultValue)
            : Result.Failure<TSuccess, TFailure>(result.ErrorValue);

    /// <summary>Converts a core result to F# Ok or Error, preserving its case and payload.</summary>
    /// <typeparam name="TSuccess">The success payload type.</typeparam>
    /// <typeparam name="TFailure">The failure payload type.</typeparam>
    /// <param name="result">The core result to convert.</param>
    /// <returns>The equivalent F# result.</returns>
    /// <exception cref="InvalidOperationException">The result is <see langword="default"/> and contains no case.</exception>
    public static FSharpResult<TSuccess, TFailure> ToFSharpResult<TSuccess, TFailure>(
        this Result<TSuccess, TFailure> result) =>
        result switch
        {
            Result<TSuccess, TFailure>.Success(var success) => FSharpResult<TSuccess, TFailure>.NewOk(success),
            Result<TSuccess, TFailure>.Failure(var failure) => FSharpResult<TSuccess, TFailure>.NewError(failure),
            null => throw new InvalidOperationException(
                "A default result contains no case. Construct results with Result.Return or Result.Failure.")
        };
}
