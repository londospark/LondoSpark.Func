using System;
using System.Linq;
using System.Threading.Tasks;

using LondoSpark.Func.AspNetCore;
using Microsoft.AspNetCore.Builder;
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

        await Assert.That(present.Result is Ok<int> { Value: 42 }).IsTrue();
        await Assert.That(absent.Result is NotFound).IsTrue();
    }

    [Test]
    public async Task OptionHonoursCallerSuppliedSuccess()
    {
        var created = Option.Return(7).ToHttpResult(
            static value => TypedResults.Created($"/items/{value}", value));

        await Assert.That(created.Result is Created<int> { Value: 7 }).IsTrue();
    }

    [Test]
    public async Task ResultMapsToOkOrCallerSuppliedFailure()
    {
        var success = Result.Return<int, ValidationError>(7).ToHttpResult(
            static error => TypedResults.Conflict(error.Message));
        var failure = Result.Failure<int, ValidationError>(new ValidationError("email", "duplicate")).ToHttpResult(
            static error => TypedResults.Conflict(error.Message));

        await Assert.That(success.Result is Ok<int> { Value: 7 }).IsTrue();
        await Assert.That(failure.Result is Conflict<string> { Value: "duplicate" }).IsTrue();
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

        await Assert.That(created.Result is Created<int> { Value: 7 }).IsTrue();
        await Assert.That(conflict.Result is Conflict<string> { Value: "duplicate" }).IsTrue();
    }

    [Test]
    public async Task ValidationMapsToOkOrFieldKeyedProblem()
    {
        var invalid = Validation.Failure<int, ValidationError>(
            new ValidationError("name", "Name is required."),
            new ValidationError("email", "A valid email address is required."),
            new ValidationError("name", "Name is too long."));

        var valid = Validation.Return<int, ValidationError>(7).ToHttpResult();
        var problem = invalid.ToHttpResult();

        await Assert.That(valid.Result is Ok<int> { Value: 7 }).IsTrue();
        await Assert.That(problem.Result is ValidationProblem).IsTrue();

        var details = ((ValidationProblem)problem.Result).ProblemDetails;
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

        await Assert.That(result.Result is Created<int> { Value: 7 }).IsTrue();
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
        var invalid = Validation.Failure<int, string>("first", "second");

        var result = invalid.ToHttpResult(static error => new ValidationError("custom", error));

        await Assert.That(result.Result is ValidationProblem).IsTrue();
        await Assert.That(((ValidationProblem)result.Result).ProblemDetails.Errors["custom"].Length).IsEqualTo(2);
    }

    [Test]
    public async Task ErrorsWithoutAFieldAreKeyedAsModelLevelErrors()
    {
        var problem = new NonEmptyList<ValidationError>(default(ValidationError), new ValidationError("name", "Required."))
            .ToValidationProblem();

        await Assert.That(problem.ProblemDetails.Errors[""].Length).IsEqualTo(1);
        await Assert.That(problem.ProblemDetails.Errors[""][0]).IsEqualTo("");
        await Assert.That(problem.ProblemDetails.Errors["name"][0]).IsEqualTo("Required.");
    }

    [Test]
    public async Task DefaultValuesThrowInvalidOperationWhenMapped()
    {
#pragma warning disable LSF0001 // The zero values are the subject of this test.
        Result<int, string> defaultResult = default;
        Validation<int, ValidationError> defaultValidation = default;
#pragma warning restore LSF0001

        await Assert.That(() => defaultResult.ToHttpResult(static error => TypedResults.Conflict(error)))
            .Throws<InvalidOperationException>();
        await Assert.That(() => defaultValidation.ToHttpResult())
            .Throws<InvalidOperationException>();
    }

    [Test]
    public async Task TypedResultsPublishBothResponsesToEndpointMetadata()
    {
        var builder = new TestEndpointBuilder();

        PopulateMetadata<Results<Ok<int>, NotFound>>(builder);

        var statusCodes = builder.Metadata
            .OfType<Microsoft.AspNetCore.Http.Metadata.IProducesResponseTypeMetadata>()
            .Select(static metadata => metadata.StatusCode)
            .Order()
            .ToArray();

        await Assert.That(statusCodes).IsEquivalentTo([StatusCodes.Status200OK, StatusCodes.Status404NotFound]);
    }

    private static void PopulateMetadata<TResult>(EndpointBuilder builder)
        where TResult : Microsoft.AspNetCore.Http.Metadata.IEndpointMetadataProvider =>
        TResult.PopulateMetadata(typeof(AspNetCoreResultTests).GetMethod(nameof(PopulateMetadata), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!, builder);

    private sealed class TestEndpointBuilder : EndpointBuilder
    {
        public override Endpoint Build() => throw new NotSupportedException();
    }
}
