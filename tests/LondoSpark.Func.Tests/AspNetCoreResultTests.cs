using System;
using System.Threading.Tasks;

using LondoSpark.Func;
using LondoSpark.Func.AspNetCore;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace LondoSpark.Func.Tests;

public sealed class AspNetCoreResultTests
{
    [Test]
    public async Task OptionMapsToOkOrNotFound()
    {
        var present = Option.Return(42).ToHttpResult();
        var absent = Option.Empty<int>().ToHttpResult();

        await Assert.That(present is Ok<int> { Value: 42 }).IsTrue();
        await Assert.That(absent is NotFound).IsTrue();
    }

    [Test]
    public async Task OptionHonoursCallerSuppliedSuccess()
    {
        var created = Option.Return(7).ToHttpResult(
            static value => TypedResults.Created($"/items/{value}", value));

        await Assert.That(created is Created<int> { Value: 7 }).IsTrue();
    }

    [Test]
    public async Task ResultMapsToOkOrCallerSuppliedFailure()
    {
        var success = Result.Return<int, ValidationError>(7).ToHttpResult(
            static error => TypedResults.Conflict(error.Message));
        var failure = Result.Failure<int, ValidationError>(new ValidationError("email", "duplicate")).ToHttpResult(
            static error => TypedResults.Conflict(error.Message));

        await Assert.That(success is Ok<int> { Value: 7 }).IsTrue();
        await Assert.That(failure is Conflict<string> { Value: "duplicate" }).IsTrue();
    }

    [Test]
    public async Task ResultHonoursCallerSuppliedSuccessAndFailure()
    {
        var created = Result.Return<int, ValidationError>(7).ToHttpResult(
            static value => TypedResults.Created($"/items/{value}", value),
            static error => TypedResults.Conflict(error.Message));
        var conflict = Result.Failure<int, ValidationError>(new ValidationError("email", "duplicate")).ToHttpResult(
            static value => TypedResults.Created($"/items/{value}", value),
            static error => TypedResults.Conflict(error.Message));

        await Assert.That(created is Created<int> { Value: 7 }).IsTrue();
        await Assert.That(conflict is Conflict<string> { Value: "duplicate" }).IsTrue();
    }

    [Test]
    public async Task ValidationMapsToOkOrFieldKeyedProblem()
    {
        Validation<int, ValidationError> invalid = Validation.Failure<int, ValidationError>(
            new ValidationError("name", "Name is required."),
            new ValidationError("email", "A valid email address is required."),
            new ValidationError("name", "Name is too long."));

        var valid = Validation.Return<int, ValidationError>(7).ToHttpResult();
        var problem = invalid.ToHttpResult();

        await Assert.That(valid is Ok<int> { Value: 7 }).IsTrue();
        await Assert.That(problem is ValidationProblem).IsTrue();

        var details = ((ValidationProblem)problem).ProblemDetails;
        await Assert.That(details.Errors["name"].Length).IsEqualTo(2);
        await Assert.That(details.Errors["name"][0]).IsEqualTo("Name is required.");
        await Assert.That(details.Errors["name"][1]).IsEqualTo("Name is too long.");
        await Assert.That(details.Errors["email"][0]).IsEqualTo("A valid email address is required.");
    }

    [Test]
    public async Task ValidationHonoursCallerSuppliedSuccess()
    {
        var result = Validation.Return<int, ValidationError>(7).ToHttpResult(
            static value => TypedResults.Created($"/items/{value}", value));

        await Assert.That(result is Created<int> { Value: 7 }).IsTrue();
    }

    [Test]
    public async Task NonEmptyListBuildsTheFieldKeyedProblem()
    {
        var errors = new NonEmptyList<ValidationError>(
            new ValidationError("name", "Name is required."),
            new ValidationError("email", "A valid email address is required."),
            new ValidationError("name", "Name is too long."));

        var problem = errors.ToValidationProblem();

        await Assert.That(problem.ProblemDetails.Errors["name"].Length).IsEqualTo(2);
        await Assert.That(problem.ProblemDetails.Errors["name"][0]).IsEqualTo("Name is required.");
        await Assert.That(problem.ProblemDetails.Errors["name"][1]).IsEqualTo("Name is too long.");
        await Assert.That(problem.ProblemDetails.Errors["email"][0]).IsEqualTo("A valid email address is required.");
        await Assert.That(problem.ProblemDetails.Status).IsEqualTo(StatusCodes.Status400BadRequest);
    }

    [Test]
    public async Task NonEmptyListRejectsNullWhenBuildingAProblem()
    {
        await Assert.That(() => ((NonEmptyList<ValidationError>)null!).ToValidationProblem())
            .Throws<ArgumentNullException>();
    }

    [Test]
    public async Task ValidationProjectsCustomErrorsOntoFields()
    {
        Validation<int, string> invalid = Validation.Failure<int, string>("first", "second");

        var result = invalid.ToHttpResult(static error => new ValidationError("custom", error));

        await Assert.That(result is ValidationProblem).IsTrue();
        await Assert.That(((ValidationProblem)result).ProblemDetails.Errors["custom"].Length).IsEqualTo(2);
    }
}
