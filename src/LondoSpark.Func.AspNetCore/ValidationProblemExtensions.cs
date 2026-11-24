using System;
using System.Collections.Generic;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace LondoSpark.Func.AspNetCore;

/// <summary>Builds the field-keyed <c>400</c> payload for validation failures.</summary>
/// <remarks>
/// Use this when a switch expression handles the failure case directly; otherwise let
/// <see cref="HttpResultExtensions.ToHttpResult{TSuccess}(Validation{TSuccess, ValidationError})"/>
/// build the problem for you.
/// </remarks>
public static class ValidationProblemExtensions
{
    /// <summary>Groups validation errors by field into a <c>400</c> problem.</summary>
    /// <param name="errors">The validation errors, in the order they were accumulated.</param>
    /// <returns>
    /// A <see cref="ValidationProblem"/> whose <c>errors</c> object keys messages by field,
    /// preserving their order within each field.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="errors"/> is <see langword="null"/>.</exception>
    public static ValidationProblem ToValidationProblem(this NonEmptyList<ValidationError> errors)
    {
        ArgumentNullException.ThrowIfNull(errors);

        return TypedResults.ValidationProblem(GroupByField(errors, static error => error));
    }

    internal static Dictionary<string, string[]> GroupByField<TValidationError>(
        NonEmptyList<TValidationError> errors,
        Func<TValidationError, ValidationError> projectError)
    {
        var grouped = new Dictionary<string, List<string>>(StringComparer.Ordinal);

        foreach (var error in errors)
        {
            var projected = projectError(error);

            if (!grouped.TryGetValue(projected.Field, out var messages))
            {
                messages = [];
                grouped.Add(projected.Field, messages);
            }

            messages.Add(projected.Message);
        }

        var problem = new Dictionary<string, string[]>(grouped.Count, StringComparer.Ordinal);

        foreach (var pair in grouped)
        {
            problem.Add(pair.Key, [.. pair.Value]);
        }

        return problem;
    }
}
