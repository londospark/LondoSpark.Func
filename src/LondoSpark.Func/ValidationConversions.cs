using System;

namespace LondoSpark.Func;

/// <summary>Collapses accumulated validation errors into one failure value so validation can feed a result pipeline.</summary>
/// <remarks>
/// A validation gathers problems applicatively; a result sequences the next step. Converting
/// at the point where gathering finishes lets both styles share one pipeline.
/// </remarks>
public static class ValidationConversions
{
    extension<TSuccess, TValidationError>(Validation<TSuccess, TValidationError> validation)
    {
        /// <summary>Converts a validation into a result whose failure carries every accumulated error in order.</summary>
        /// <returns>A success containing the validated value, or a failure containing the non-empty error list.</returns>
        public Result<TSuccess, NonEmptyList<TValidationError>> ToResult() =>
            validation switch
            {
                Validation<TSuccess, TValidationError>.Success(var success) =>
                    Result.Return<TSuccess, NonEmptyList<TValidationError>>(success),
                Validation<TSuccess, TValidationError>.Failure(var errors) =>
                    Result.Failure<TSuccess, NonEmptyList<TValidationError>>(errors),
                null => throw NoCase.Validation()
            };
    }
}
