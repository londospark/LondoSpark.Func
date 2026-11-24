using System;
using System.Threading.Tasks;

using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;
using static LondoSpark.Func.Validation<int, string>;

namespace LondoSpark.Func.Tests;

public sealed class ValidationTests
{
    [Test]
    public async Task ValidationErrorsRenderAsFieldKeyedMessages()
    {
        var errors = new NonEmptyList<ValidationError>(
            new ValidationError("name", "Name is required."),
            new ValidationError("email", "A valid email address is required."));

        await Assert.That(errors.Count).IsEqualTo(2);
        await Assert.That(errors[0].ToString()).IsEqualTo("name: Name is required.");
        await Assert.That(errors.ToSeparatedString(" ")).IsEqualTo(
            "name: Name is required. email: A valid email address is required.");
    }

    [Test]
    public async Task ValidationFunctorIdentityAndComposition()
    {
        var source = Validation.Return<int, string>(7);
        var identity = source.Select(value => value);
        var composed = source.Select(value => value + 2).Select(value => value * 3);
        var separate = source.Select(value => (value + 2) * 3);

        await Assert.That(identity is Success(7)).IsTrue();
        await Assert.That(composed is Success(27)).IsTrue();
        await Assert.That(separate is Success(27)).IsTrue();
    }

    [Test]
    public async Task ValidationApplyReturnsSuccessWhenBothInputsPass()
    {
        var function =
            Validation.Return<Func<int, int>, string>(value => value * 6);
        var value = Validation.Return<int, string>(7);

        await Assert.That(function.Apply(value) is Success(42)).IsTrue();
    }

    [Test]
    public async Task ValidationApplyCollectsFunctionErrorsBeforeValueErrors()
    {
        var callbackCalled = false;
        var functionErrors =
            Validation.Failure<Func<int, int>, string>("function 1", "function 2");
        var valueErrors =
            Validation.Failure<int, string>("value 1", "value 2");

        var bothFailures = functionErrors.Apply(valueErrors);
        var functionOnly = functionErrors.Apply(Validation.Return<int, string>(7));
        var valueOnly = Validation.Return<Func<int, int>, string>(
                value =>
                {
                    callbackCalled = true;
                    return value;
                })
            .Apply(valueErrors);

        await Assert.That(ValidationErrors(bothFailures))
            .IsEqualTo("function 1|function 2|value 1|value 2");
        await Assert.That(ValidationErrors(functionOnly))
            .IsEqualTo("function 1|function 2");
        await Assert.That(ValidationErrors(valueOnly))
            .IsEqualTo("value 1|value 2");
        await Assert.That(callbackCalled).IsFalse();
    }

    [Test]
    public async Task ValidationApplyPassesBothSuccessesToTheFunction()
    {
        var callbackCalled = false;
        var function =
            Validation.Return<Func<int, int>, string>(value =>
            {
                callbackCalled = true;
                return value + 1;
            });
        var value = Validation.Return<int, string>(41);

        var applied = function.Apply(value);

        await Assert.That(applied is Success(42)).IsTrue();
        await Assert.That(callbackCalled).IsTrue();
    }

    [Test]
    public async Task NonEmptyListAppendPreservesBothListsInOrder()
    {
        var left = new NonEmptyList<string>("left 1", "left 2");
        var right = new NonEmptyList<string>("right 1", "right 2");

        var appended = left.Append(right);

        await Assert.That(appended.Count).IsEqualTo(4);
        await Assert.That(appended[0]).IsEqualTo("left 1");
        await Assert.That(appended[1]).IsEqualTo("left 2");
        await Assert.That(appended[2]).IsEqualTo("right 1");
        await Assert.That(appended[3]).IsEqualTo("right 2");
    }

    [Test]
    public async Task ValidationNullSuccessRemainsSuccessfulThroughSelect()
    {
        var selectReceivedNull = false;
        var successNull =
            Validation.Return<string?, string?>(null);

        var selected = successNull.Select(value =>
        {
            selectReceivedNull = value is null;
            return "selected-null-success";
        });

        await Assert.That(selectReceivedNull).IsTrue();
        await Assert.That(selected is Validation<string, string>.Success("selected-null-success")).IsTrue();
    }

    [Test]
    public async Task ValidationApplyAccumulatesNullableErrorsInOrder()
    {
        var functionErrors =
            Validation.Failure<Func<int, int>, string?>(null, "function-error");
        var valueErrors =
            Validation.Failure<int, string?>(null, "value-error");

        var combined = functionErrors.Apply(valueErrors);
        var selected = combined.Select(_ => 0);

        if (combined is not Failure combinedFailure)
        {
            throw new InvalidOperationException("Applying two invalid validations must return accumulated errors.");
        }

        if (selected is not Failure selectedFailure)
        {
            throw new InvalidOperationException("Selecting a failed validation must preserve its errors.");
        }

        var combinedErrors = combinedFailure.Errors;
        var selectedErrors = selectedFailure.Errors;

        await Assert.That(combinedErrors.Count).IsEqualTo(4);
        await Assert.That(combinedErrors[0] is null).IsTrue();
        await Assert.That(combinedErrors[1]).IsEqualTo("function-error");
        await Assert.That(combinedErrors[2] is null).IsTrue();
        await Assert.That(combinedErrors[3]).IsEqualTo("value-error");
        await Assert.That(selectedErrors.Count).IsEqualTo(4);
        await Assert.That(selectedErrors[0] is null).IsTrue();
        await Assert.That(selectedErrors[1]).IsEqualTo("function-error");
        await Assert.That(selectedErrors[2] is null).IsTrue();
        await Assert.That(selectedErrors[3]).IsEqualTo("value-error");
    }

    [Test]
    public async Task ValidationConvertsToAResultSuccess()
    {
        var result = Validation.Return<int, string>(7).ToResult();

        await Assert.That(result is Result<int, NonEmptyList<string>>.Success(7)).IsTrue();
    }

    [Test]
    public async Task ValidationConvertsToAResultFailureCarryingEveryErrorInOrder()
    {
        var invalid = Validation.Failure<int, string>("name", "email", "age");

        var result = invalid.ToResult();

        var errors = result switch
        {
            Result<int, NonEmptyList<string>>.Failure failure => failure.Value,
            _ => throw new InvalidOperationException($"Expected a failure but got {result}.")
        };

        await Assert.That(errors.Count).IsEqualTo(3);
        await Assert.That(errors[0]).IsEqualTo("name");
        await Assert.That(errors[1]).IsEqualTo("email");
        await Assert.That(errors[2]).IsEqualTo("age");
    }

    private static string ValidationErrors<TSuccess>(
        Validation<TSuccess, string> validation) =>
        validation switch
        {
            Validation<TSuccess, string>.Success => "",
            Validation<TSuccess, string>.Failure(var errors) => string.Join("|", errors)
        };
}
