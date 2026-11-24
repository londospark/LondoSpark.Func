using System;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace LondoSpark.Func.AspNetCore;

/// <summary>Maps union values to typed ASP.NET Core HTTP results with a single verb.</summary>
/// <remarks>
/// The overload decides the mapping: an option is <c>200</c> or <c>404</c>, a result is
/// <c>200</c> or the caller's failure result, and a validation is <c>200</c> or a
/// field-keyed <c>400</c>. Every overload returns <see cref="Results{TResult1, TResult2}"/>, so a
/// minimal-API endpoint that returns it publishes both responses to OpenAPI. When a callback
/// returns different result types from different branches, give the lambda an explicit
/// <see cref="IResult"/> return type. A flow of several steps reads better as a switch expression,
/// with <see cref="ValidationProblemExtensions.ToValidationProblem"/> building the failure case.
/// Success paths only wrap the value in the typed result; allocations happen on the failure path
/// where the problem payload is built.
/// </remarks>
public static class HttpResultExtensions
{
    private const string NoResultCase =
        "A default result contains no case. Construct results with Result.Return or Result.Failure.";

    private const string NoValidationCase =
        "A default validation contains no case. Construct validations with Validation.Return or Validation.Failure.";

    /// <summary>Maps an option to <c>200 OK</c> or <c>404 Not Found</c>.</summary>
    /// <typeparam name="TSome">The present value type.</typeparam>
    /// <param name="option">The option to map.</param>
    /// <returns>The present value as <c>OK</c>, or <see cref="NotFound"/>.</returns>
    public static Results<Ok<TSome>, NotFound> ToHttpResult<TSome>(this Option<TSome> option) =>
        option.ToHttpResult(static value => TypedResults.Ok(value));

    /// <summary>Maps an option with a caller-supplied success result, using <c>404 Not Found</c> for absence.</summary>
    /// <typeparam name="TSome">The present value type.</typeparam>
    /// <typeparam name="TOnSome">The result type produced for a present value.</typeparam>
    /// <param name="option">The option to map.</param>
    /// <param name="onSome">Produces the result for a present value.</param>
    /// <returns>The caller-supplied result, or <see cref="NotFound"/>.</returns>
    public static Results<TOnSome, NotFound> ToHttpResult<TSome, TOnSome>(
        this Option<TSome> option,
        Func<TSome, TOnSome> onSome)
        where TOnSome : IResult =>
        option switch
        {
            Option<TSome>.Some(var some) => onSome(some),
            None => TypedResults.NotFound()
        };

    /// <summary>Maps a result to <c>200 OK</c> or a caller-supplied failure result.</summary>
    /// <typeparam name="TSuccess">The success value type.</typeparam>
    /// <typeparam name="TFailure">The failure value type.</typeparam>
    /// <typeparam name="TOnFailure">The result type produced for a failure.</typeparam>
    /// <param name="result">The result to map.</param>
    /// <param name="onFailure">Produces the result for a failure, for example <c>TypedResults.Conflict(...)</c>.</param>
    /// <returns>The success value as <c>OK</c>, or the caller-supplied failure result.</returns>
    /// <exception cref="InvalidOperationException">The result is <see langword="default"/> and contains no case.</exception>
    public static Results<Ok<TSuccess>, TOnFailure> ToHttpResult<TSuccess, TFailure, TOnFailure>(
        this Result<TSuccess, TFailure> result,
        Func<TFailure, TOnFailure> onFailure)
        where TOnFailure : IResult =>
        result.ToHttpResult(static value => TypedResults.Ok(value), onFailure);

    /// <summary>Maps a result with caller-supplied results for each case.</summary>
    /// <typeparam name="TSuccess">The success value type.</typeparam>
    /// <typeparam name="TFailure">The failure value type.</typeparam>
    /// <typeparam name="TOnSuccess">The result type produced for a success.</typeparam>
    /// <typeparam name="TOnFailure">The result type produced for a failure.</typeparam>
    /// <param name="result">The result to map.</param>
    /// <param name="onSuccess">Produces the result for a success, for example <c>TypedResults.Created(...)</c>.</param>
    /// <param name="onFailure">Produces the result for a failure.</param>
    /// <returns>The mapped result.</returns>
    /// <exception cref="InvalidOperationException">The result is <see langword="default"/> and contains no case.</exception>
    public static Results<TOnSuccess, TOnFailure> ToHttpResult<TSuccess, TFailure, TOnSuccess, TOnFailure>(
        this Result<TSuccess, TFailure> result,
        Func<TSuccess, TOnSuccess> onSuccess,
        Func<TFailure, TOnFailure> onFailure)
        where TOnSuccess : IResult
        where TOnFailure : IResult =>
        result switch
        {
            Result<TSuccess, TFailure>.Success(var success) => onSuccess(success),
            Result<TSuccess, TFailure>.Failure(var failure) => onFailure(failure),
            null => throw new InvalidOperationException(NoResultCase)
        };

    /// <summary>Maps a validation to <c>200 OK</c> or a <c>400</c> problem keyed by field.</summary>
    /// <typeparam name="TSuccess">The validated value type.</typeparam>
    /// <typeparam name="TValidationError">The validation error type.</typeparam>
    /// <param name="validation">The validation to map.</param>
    /// <param name="projectError">Projects a validation error onto the field and message sent to the client.</param>
    /// <returns>The validated value as <c>OK</c>, or a field-keyed <see cref="ValidationProblem"/>.</returns>
    /// <exception cref="InvalidOperationException">The validation is <see langword="default"/> and contains no case.</exception>
    public static Results<Ok<TSuccess>, ValidationProblem> ToHttpResult<TSuccess, TValidationError>(
        this Validation<TSuccess, TValidationError> validation,
        Func<TValidationError, ValidationError> projectError) =>
        validation.ToHttpResult(static value => TypedResults.Ok(value), projectError);

    /// <summary>Maps a validation with a caller-supplied success result, using a field-keyed <c>400</c> for failures.</summary>
    /// <typeparam name="TSuccess">The validated value type.</typeparam>
    /// <typeparam name="TValidationError">The validation error type.</typeparam>
    /// <typeparam name="TOnSuccess">The result type produced for a valid value.</typeparam>
    /// <param name="validation">The validation to map.</param>
    /// <param name="onSuccess">Produces the result for a valid value, for example <c>TypedResults.Created(...)</c>.</param>
    /// <param name="projectError">Projects a validation error onto the field and message sent to the client.</param>
    /// <returns>The caller-supplied result, or a field-keyed <see cref="ValidationProblem"/>.</returns>
    /// <exception cref="InvalidOperationException">The validation is <see langword="default"/> and contains no case.</exception>
    public static Results<TOnSuccess, ValidationProblem> ToHttpResult<TSuccess, TValidationError, TOnSuccess>(
        this Validation<TSuccess, TValidationError> validation,
        Func<TSuccess, TOnSuccess> onSuccess,
        Func<TValidationError, ValidationError> projectError)
        where TOnSuccess : IResult =>
        validation switch
        {
            Validation<TSuccess, TValidationError>.Success(var success) => onSuccess(success),
            Validation<TSuccess, TValidationError>.Failure(var errors) =>
                TypedResults.ValidationProblem(
                    ValidationProblemExtensions.GroupByField(errors, projectError)),
            null => throw new InvalidOperationException(NoValidationCase)
        };

    /// <summary>Maps a validation of <see cref="ValidationError"/> values to <c>200 OK</c> or a field-keyed <c>400</c>.</summary>
    /// <typeparam name="TSuccess">The validated value type.</typeparam>
    /// <param name="validation">The validation to map.</param>
    /// <returns>The validated value as <c>OK</c>, or a field-keyed <see cref="ValidationProblem"/>.</returns>
    /// <exception cref="InvalidOperationException">The validation is <see langword="default"/> and contains no case.</exception>
    public static Results<Ok<TSuccess>, ValidationProblem> ToHttpResult<TSuccess>(
        this Validation<TSuccess, ValidationError> validation) =>
        validation.ToHttpResult(static error => error);

    /// <summary>Maps a validation of <see cref="ValidationError"/> values with a caller-supplied success result.</summary>
    /// <typeparam name="TSuccess">The validated value type.</typeparam>
    /// <typeparam name="TOnSuccess">The result type produced for a valid value.</typeparam>
    /// <param name="validation">The validation to map.</param>
    /// <param name="onSuccess">Produces the result for a valid value.</param>
    /// <returns>The caller-supplied result, or a field-keyed <see cref="ValidationProblem"/>.</returns>
    /// <exception cref="InvalidOperationException">The validation is <see langword="default"/> and contains no case.</exception>
    public static Results<TOnSuccess, ValidationProblem> ToHttpResult<TSuccess, TOnSuccess>(
        this Validation<TSuccess, ValidationError> validation,
        Func<TSuccess, TOnSuccess> onSuccess)
        where TOnSuccess : IResult =>
        validation.ToHttpResult(onSuccess, static error => error);
}
