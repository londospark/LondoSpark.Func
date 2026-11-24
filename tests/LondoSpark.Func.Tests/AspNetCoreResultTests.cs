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
        var present = Option.Return(42).ToOkOrNotFound();
        var absent = Option.Empty<int>().ToOkOrNotFound();

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
    public async Task ResultMapsSuccessAndFailure()
    {
        var success = Result.Return<int, ValidationError>(7).ToOkOrProblem(
            StatusCodes.Status409Conflict,
            static error => error.Message);
        var failure = Result.Failure<int, ValidationError>(new ValidationError("email", "duplicate")).ToOkOrProblem(
            StatusCodes.Status409Conflict,
            static error => error.Message);

        await Assert.That(success is Ok<int> { Value: 7 }).IsTrue();
        await Assert.That(failure is ProblemHttpResult { StatusCode: StatusCodes.Status409Conflict }).IsTrue();
        await Assert.That(((ProblemHttpResult)failure).ProblemDetails.Detail).IsEqualTo("duplicate");
    }

    [Test]
    public async Task ValidationMapsSuccessToOk()
    {
        var result = Validation.Return<int, ValidationError>(7).ToOkOrValidationProblem();

        await Assert.That(result is Ok<int> { Value: 7 }).IsTrue();
    }

    [Test]
    public async Task ValidationGroupsErrorsByField()
    {
        Validation<int, ValidationError> invalid = Validation.Failure<int, ValidationError>(
            new ValidationError("name", "Name is required."),
            new ValidationError("email", "A valid email address is required."),
            new ValidationError("name", "Name is too long."));

        var result = invalid.ToOkOrValidationProblem();

        await Assert.That(result is ValidationProblem).IsTrue();

        var problem = (ValidationProblem)result;
        await Assert.That(problem.ProblemDetails.Errors["name"].Length).IsEqualTo(2);
        await Assert.That(problem.ProblemDetails.Errors["name"][0]).IsEqualTo("Name is required.");
        await Assert.That(problem.ProblemDetails.Errors["name"][1]).IsEqualTo("Name is too long.");
        await Assert.That(problem.ProblemDetails.Errors["email"][0]).IsEqualTo("A valid email address is required.");
    }

    [Test]
    public async Task ValidationHonoursCallerSuppliedSuccess()
    {
        var result = Validation.Return<int, ValidationError>(7).ToHttpResult(
            static value => TypedResults.Created($"/items/{value}", value));

        await Assert.That(result is Created<int> { Value: 7 }).IsTrue();
    }

    [Test]
    public async Task ValidationSupportsCustomErrorTypes()
    {
        Validation<int, string> invalid = Validation.Failure<int, string>("first", "second");

        var result = invalid.ToOkOrValidationProblem(
            static _ => "custom",
            static error => error);

        await Assert.That(result is ValidationProblem).IsTrue();
        await Assert.That(((ValidationProblem)result).ProblemDetails.Errors["custom"].Length).IsEqualTo(2);
    }
}
