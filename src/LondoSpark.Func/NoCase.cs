using System;

namespace LondoSpark.Func;

/// <summary>Builds the exception for a union value that was zero-initialised and so contains no case.</summary>
/// <remarks>
/// Switches match <see langword="null"/> for that state and throw what these methods return; keeping
/// construction out of line leaves the hot switch small enough to inline.
/// </remarks>
internal static class NoCase
{
    internal const string ResultMessage =
        "A default result contains no case. Construct results with Result.Return or Result.Failure.";

    internal const string ValidationMessage =
        "A default validation contains no case. Construct validations with Validation.Return or Validation.Failure.";

    internal static InvalidOperationException Result() => new(ResultMessage);

    internal static InvalidOperationException Validation() => new(ValidationMessage);
}
