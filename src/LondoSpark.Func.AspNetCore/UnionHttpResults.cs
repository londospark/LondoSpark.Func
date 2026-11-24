using System;
using System.Collections.Generic;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace LondoSpark.Func.AspNetCore;

/// <summary>Maps union values to ASP.NET Core HTTP results so endpoints do not hand-write the same switches.</summary>
/// <remarks>
/// Failure paths allocate the problem payload; success paths only wrap the value in the
/// typed result, so mapping adds no copying or reflection.
/// </remarks>
public static class UnionHttpResults
{
    /// <summary>Maps an option to <c>200 OK</c> or <c>404 Not Found</c>.</summary>
    /// <typeparam name="TSome">The present value type.</typeparam>
    /// <param name="option">The option to map.</param>
    /// <returns>The present value as <c>OK</c>, or <see cref="NotFound"/>.</returns>
    public static IResult ToOkOrNotFound<TSome>(this Option<TSome> option) =>
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

    /// <summary>Maps a result to <c>200 OK</c> or a problem response with the supplied status and detail.</summary>
    /// <typeparam name="TSuccess">The success value type.</typeparam>
    /// <typeparam name="TFailure">The failure value type.</typeparam>
    /// <param name="result">The result to map.</param>
    /// <param name="failureStatus">The status code used for a failure.</param>
    /// <param name="failureDetail">Selects the problem detail from the failure value.</param>
    /// <returns>The success value as <c>OK</c>, or a <see cref="ProblemHttpResult"/>.</returns>
    public static IResult ToOkOrProblem<TSuccess, TFailure>(
        this Result<TSuccess, TFailure> result,
        int failureStatus,
        Func<TFailure, string> failureDetail) =>
        result.ToHttpResult(
            static value => TypedResults.Ok(value),
            failure => TypedResults.Problem(failureDetail(failure), statusCode: failureStatus));

    /// <summary>Maps a result with caller-supplied results for each case.</summary>
    /// <typeparam name="TSuccess">The success value type.</typeparam>
    /// <typeparam name="TFailure">The failure value type.</typeparam>
    /// <param name="result">The result to map.</param>
    /// <param name="onSuccess">Produces the result for a success.</param>
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

    /// <summary>Maps a validation to <c>200 OK</c> or a <c>400</c> validation problem grouped by field.</summary>
    /// <typeparam name="TSuccess">The validated value type.</typeparam>
    /// <typeparam name="TValidationError">The validation error type, which must expose a field and a message.</typeparam>
    /// <param name="validation">The validation to map.</param>
    /// <param name="failedField">Selects the field name from an error.</param>
    /// <param name="failureMessage">Selects the message from an error.</param>
    /// <returns>The validated value as <c>OK</c>, or a <see cref="ValidationProblem"/>.</returns>
    /// <exception cref="InvalidOperationException">The validation is <see langword="default"/> and contains no case.</exception>
    public static IResult ToOkOrValidationProblem<TSuccess, TValidationError>(
        this Validation<TSuccess, TValidationError> validation,
        Func<TValidationError, string> failedField,
        Func<TValidationError, string> failureMessage) =>
        validation.ToHttpResult(
            static value => TypedResults.Ok(value),
            failedField,
            failureMessage);

    /// <summary>Maps a validation of <see cref="ValidationError"/> values to <c>200 OK</c> or a field-keyed <c>400</c>.</summary>
    /// <typeparam name="TSuccess">The validated value type.</typeparam>
    /// <param name="validation">The validation to map.</param>
    /// <returns>The validated value as <c>OK</c>, or a <see cref="ValidationProblem"/>.</returns>
    public static IResult ToOkOrValidationProblem<TSuccess>(this Validation<TSuccess, ValidationError> validation) =>
        validation.ToOkOrValidationProblem(static error => error.Field, static error => error.Message);

    /// <summary>Maps a validation with a caller-supplied success result, using a field-keyed <c>400</c> for failures.</summary>
    /// <typeparam name="TSuccess">The validated value type.</typeparam>
    /// <typeparam name="TValidationError">The validation error type, which must expose a field and a message.</typeparam>
    /// <param name="validation">The validation to map.</param>
    /// <param name="onSuccess">Produces the result for a valid value.</param>
    /// <param name="failedField">Selects the field name from an error.</param>
    /// <param name="failureMessage">Selects the message from an error.</param>
    /// <returns>The caller-supplied result, or a <see cref="ValidationProblem"/>.</returns>
    /// <exception cref="InvalidOperationException">The validation is <see langword="default"/> and contains no case.</exception>
    public static IResult ToHttpResult<TSuccess, TValidationError>(
        this Validation<TSuccess, TValidationError> validation,
        Func<TSuccess, IResult> onSuccess,
        Func<TValidationError, string> failedField,
        Func<TValidationError, string> failureMessage) =>
        validation switch
        {
            Success<TSuccess> success => onSuccess(success.Value),
            Failure<NonEmptyList<TValidationError>> errors =>
                TypedResults.ValidationProblem(GroupByField(errors.Value, failedField, failureMessage)),
            _ => throw new InvalidOperationException("A default validation contains no case to map.")
        };

    /// <summary>Maps a validation of <see cref="ValidationError"/> values with a caller-supplied success result.</summary>
    /// <typeparam name="TSuccess">The validated value type.</typeparam>
    /// <param name="validation">The validation to map.</param>
    /// <param name="onSuccess">Produces the result for a valid value.</param>
    /// <returns>The caller-supplied result, or a field-keyed <c>400</c> validation problem.</returns>
    public static IResult ToHttpResult<TSuccess>(
        this Validation<TSuccess, ValidationError> validation,
        Func<TSuccess, IResult> onSuccess) =>
        validation.ToHttpResult(
            onSuccess,
            static error => error.Field,
            static error => error.Message);

    private static Dictionary<string, string[]> GroupByField<TValidationError>(
        NonEmptyList<TValidationError> errors,
        Func<TValidationError, string> failedField,
        Func<TValidationError, string> failureMessage)
    {
        var grouped = new Dictionary<string, List<string>>(StringComparer.Ordinal);

        foreach (var error in errors)
        {
            var field = failedField(error);

            if (!grouped.TryGetValue(field, out var messages))
            {
                messages = [];
                grouped.Add(field, messages);
            }

            messages.Add(failureMessage(error));
        }

        var problem = new Dictionary<string, string[]>(grouped.Count, StringComparer.Ordinal);

        foreach (var pair in grouped)
        {
            problem.Add(pair.Key, [.. pair.Value]);
        }

        return problem;
    }
}
