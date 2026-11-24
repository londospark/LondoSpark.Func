using System;
using System.Collections.Generic;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace LondoSpark.Func.AspNetCore;

/// <summary>Maps union values to ASP.NET Core HTTP results with a single verb.</summary>
/// <remarks>
/// The overload decides the mapping: an option is <c>200</c> or <c>404</c>, a result is
/// <c>200</c> or the caller's failure result, and a validation is <c>200</c> or a
/// field-keyed <c>400</c>. Success paths only wrap the value in the typed result;
/// allocations happen on the failure path where the problem payload is built.
/// </remarks>
public static class HttpResultExtensions
{
    /// <summary>Maps an option to <c>200 OK</c> or <c>404 Not Found</c>.</summary>
    /// <typeparam name="TSome">The present value type.</typeparam>
    /// <param name="option">The option to map.</param>
    /// <returns>The present value as <c>OK</c>, or <see cref="NotFound"/>.</returns>
    public static IResult ToHttpResult<TSome>(this Option<TSome> option) =>
        option.ToHttpResult(static value => TypedResults.Ok(value));

    /// <summary>Maps an option with a caller-supplied success result, using <c>404 Not Found</c> for absence.</summary>
    /// <typeparam name="TSome">The present value type.</typeparam>
    /// <param name="option">The option to map.</param>
    /// <param name="onSome">Produces the result for a present value.</param>
    /// <returns>The caller-supplied result, or <see cref="NotFound"/>.</returns>
    public static IResult ToHttpResult<TSome>(this Option<TSome> option, Func<TSome, IResult> onSome) =>
        option switch
        {
            Some<TSome> some => onSome(some.Value),
            _ => TypedResults.NotFound()
        };

    /// <summary>Maps a result to <c>200 OK</c> or a caller-supplied failure result.</summary>
    /// <typeparam name="TSuccess">The success value type.</typeparam>
    /// <typeparam name="TFailure">The failure value type.</typeparam>
    /// <param name="result">The result to map.</param>
    /// <param name="onFailure">Produces the result for a failure, for example <c>TypedResults.Conflict(...)</c>.</param>
    /// <returns>The success value as <c>OK</c>, or the caller-supplied failure result.</returns>
    public static IResult ToHttpResult<TSuccess, TFailure>(
        this Result<TSuccess, TFailure> result,
        Func<TFailure, IResult> onFailure) =>
        result.ToHttpResult(static value => TypedResults.Ok(value), onFailure);

    /// <summary>Maps a result with caller-supplied results for each case.</summary>
    /// <typeparam name="TSuccess">The success value type.</typeparam>
    /// <typeparam name="TFailure">The failure value type.</typeparam>
    /// <param name="result">The result to map.</param>
    /// <param name="onSuccess">Produces the result for a success, for example <c>TypedResults.Created(...)</c>.</param>
    /// <param name="onFailure">Produces the result for a failure.</param>
    /// <returns>The mapped result.</returns>
    /// <exception cref="InvalidOperationException">The result is <see langword="default"/> and contains no case.</exception>
    public static IResult ToHttpResult<TSuccess, TFailure>(
        this Result<TSuccess, TFailure> result,
        Func<TSuccess, IResult> onSuccess,
        Func<TFailure, IResult> onFailure) =>
        result switch
        {
            Success<TSuccess> success => onSuccess(success.Value),
            Failure<TFailure> failure => onFailure(failure.Value),
            _ => throw new InvalidOperationException("A default result contains no case to map.")
        };

    /// <summary>Maps a validation to <c>200 OK</c> or a <c>400</c> problem keyed by field.</summary>
    /// <typeparam name="TSuccess">The validated value type.</typeparam>
    /// <typeparam name="TValidationError">The validation error type.</typeparam>
    /// <param name="validation">The validation to map.</param>
    /// <param name="projectError">Projects a validation error onto the field and message sent to the client.</param>
    /// <returns>The validated value as <c>OK</c>, or a field-keyed <see cref="ValidationProblem"/>.</returns>
    /// <exception cref="InvalidOperationException">The validation is <see langword="default"/> and contains no case.</exception>
    public static IResult ToHttpResult<TSuccess, TValidationError>(
        this Validation<TSuccess, TValidationError> validation,
        Func<TValidationError, ValidationError> projectError) =>
        validation.ToHttpResult(static value => TypedResults.Ok(value), projectError);

    /// <summary>Maps a validation with a caller-supplied success result, using a field-keyed <c>400</c> for failures.</summary>
    /// <typeparam name="TSuccess">The validated value type.</typeparam>
    /// <typeparam name="TValidationError">The validation error type.</typeparam>
    /// <param name="validation">The validation to map.</param>
    /// <param name="onSuccess">Produces the result for a valid value, for example <c>TypedResults.Created(...)</c>.</param>
    /// <param name="projectError">Projects a validation error onto the field and message sent to the client.</param>
    /// <returns>The caller-supplied result, or a field-keyed <see cref="ValidationProblem"/>.</returns>
    /// <exception cref="InvalidOperationException">The validation is <see langword="default"/> and contains no case.</exception>
    public static IResult ToHttpResult<TSuccess, TValidationError>(
        this Validation<TSuccess, TValidationError> validation,
        Func<TSuccess, IResult> onSuccess,
        Func<TValidationError, ValidationError> projectError) =>
        validation switch
        {
            Success<TSuccess> success => onSuccess(success.Value),
            Failure<NonEmptyList<TValidationError>> errors =>
                TypedResults.ValidationProblem(GroupByField(errors.Value, projectError)),
            _ => throw new InvalidOperationException("A default validation contains no case to map.")
        };

    /// <summary>Maps a validation of <see cref="ValidationError"/> values to <c>200 OK</c> or a field-keyed <c>400</c>.</summary>
    /// <typeparam name="TSuccess">The validated value type.</typeparam>
    /// <param name="validation">The validation to map.</param>
    /// <returns>The validated value as <c>OK</c>, or a field-keyed <see cref="ValidationProblem"/>.</returns>
    public static IResult ToHttpResult<TSuccess>(this Validation<TSuccess, ValidationError> validation) =>
        validation.ToHttpResult(static error => error);

    /// <summary>Maps a validation of <see cref="ValidationError"/> values with a caller-supplied success result.</summary>
    /// <typeparam name="TSuccess">The validated value type.</typeparam>
    /// <param name="validation">The validation to map.</param>
    /// <param name="onSuccess">Produces the result for a valid value.</param>
    /// <returns>The caller-supplied result, or a field-keyed <see cref="ValidationProblem"/>.</returns>
    public static IResult ToHttpResult<TSuccess>(
        this Validation<TSuccess, ValidationError> validation,
        Func<TSuccess, IResult> onSuccess) =>
        validation.ToHttpResult(onSuccess, static error => error);

    private static Dictionary<string, string[]> GroupByField<TValidationError>(
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
