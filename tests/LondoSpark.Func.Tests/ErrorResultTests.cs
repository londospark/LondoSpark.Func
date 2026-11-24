using System;
using System.Threading.Tasks;

using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;
using static LondoSpark.Func.Result<int, string>;

namespace LondoSpark.Func.Tests;

public sealed class ErrorResultTests
{
    [Test]
    public async Task ErrorResultReturnAndFunctorPreserveFailureFocus()
    {
        var failure = ErrorResult.Return<int, string>("first");
        var mapped = failure.Select(error => $"{error}:mapped");
        var success = Result.Return<int, string>(7).AsErrorResult();
        var successMapped = success.Select(error => $"{error}:mapped");

        await Assert.That(mapped.ToResult() is Failure("first:mapped")).IsTrue();
        await Assert.That(successMapped.ToResult() is Success(7)).IsTrue();
    }

    [Test]
    public async Task ErrorResultFunctorIdentityAndComposition()
    {
        var source = ErrorResult.Return<int, string>("error");
        var identity = source.Select(error => error);
        var composed = source.Select(error => error + "-one").Select(error => error + "-two");
        var separate = source.Select(error => error + "-one" + "-two");

        await Assert.That(identity.ToResult() is Failure("error")).IsTrue();
        await Assert.That(composed.ToResult() is Failure("error-one-two")).IsTrue();
        await Assert.That(separate.ToResult() is Failure("error-one-two")).IsTrue();
    }

    [Test]
    public async Task ErrorResultApplyAppliesFunctionToFailure()
    {
        var function =
            ErrorResult.Return<string, Func<int, int>>(value => value + 1);
        var value = ErrorResult.Return<string, int>(41);

        var applied = function.Apply(value);

        await Assert.That(applied.ToResult() is Result<string, int>.Failure(42)).IsTrue();
    }

    [Test]
    public async Task ErrorResultApplyShortCircuitsSuccessPassThroughCases()
    {
        var functionCalled = false;

        var successFunction =
            Result.Return<string, Func<int, int>>("function pass-through").AsErrorResult();
        var failureValue = ErrorResult.Return<string, int>(41);
        var failureFunction =
            ErrorResult.Return<string, Func<int, int>>(value =>
            {
                functionCalled = true;
                return value + 1;
            });
        var successValue =
                Result.Return<string, int>("value pass-through").AsErrorResult();

        var successFunctionFailureValue = successFunction.Apply(failureValue);
        var failureFunctionSuccessValue = failureFunction.Apply(successValue);
        var bothFailures = failureFunction.Apply(failureValue);

        await Assert.That(successFunctionFailureValue.ToResult()
            is Result<string, int>.Success("function pass-through")).IsTrue();
        await Assert.That(failureFunctionSuccessValue.ToResult()
            is Result<string, int>.Success("value pass-through")).IsTrue();
        await Assert.That(bothFailures.ToResult() is Result<string, int>.Failure(42)).IsTrue();
        await Assert.That(functionCalled).IsTrue();
    }

    [Test]
    public async Task ErrorResultBindErrorCallbacksDoNotRunOnSuccess()
    {
        var callbackCalled = false;
        var success = Result.Return<int, string>(7).AsErrorResult();

        var bound = success.BindError(error =>
        {
            callbackCalled = true;
            return ErrorResult.Return<int, string>($"{error}:mapped");
        });

        await Assert.That(bound.ToResult() is Success(7)).IsTrue();
        await Assert.That(callbackCalled).IsFalse();
    }

    [Test]
    public async Task ErrorResultQueryBindsOnFailure()
    {
        static ErrorResult<double, string> AddContext(DivideByZeroError _) =>
            Result.Failure<double, string>("division by zero").AsErrorResult();

        var result = from error in MathOperations.Divide(10.0, 0.0).AsErrorResult()
                     from context in AddContext(error)
                     select $"{context}: {error.GetType().Name}";

        await Assert.That(result.ToResult()
            is Result<double, string>.Failure("division by zero: DivideByZeroError")).IsTrue();
        await Assert.That(MathOperations.Divide(10.0, 2.0).AsErrorResult().ToResult()
            is Result<double, DivideByZeroError>.Success(5.0)).IsTrue();
    }

    [Test]
    public async Task ErrorResultQueryPassesThroughSuccessFromSecondComputation()
    {
        var projectorCalled = false;
        var source = ErrorResult.Return<int, string>("first failure");
        var secondSuccess = Result.Return<int, bool>(42).AsErrorResult();

        var result = source.SelectMany(
            _ => secondSuccess,
            (first, second) =>
            {
                projectorCalled = true;
                return $"{first}: {second}";
            });

        await Assert.That(result.ToResult() is Success(42)).IsTrue();
        await Assert.That(projectorCalled).IsFalse();
    }

    [Test]
    public async Task ErrorResultSelectPreservesNullSuccessAndMapsNullFailure()
    {
        var selectCalledForSuccess = false;
        var successNull =
            Result.Return<string?, string?>(null).AsErrorResult();
        var failureNull =
            Result.Failure<string?, string?>(null).AsErrorResult();

        var mappedSuccess = successNull.Select(_ =>
        {
            selectCalledForSuccess = true;
            return "should-not-be-produced";
        });
        var mappedFailure = failureNull.Select(error =>
            error is null ? "mapped-null-error" : "unexpected-error");

        await Assert.That(mappedSuccess.ToResult() is Result<string, string>.Success(null)).IsTrue();
        await Assert.That(mappedFailure.ToResult() is Result<string, string>.Failure("mapped-null-error")).IsTrue();
        await Assert.That(selectCalledForSuccess).IsFalse();
    }

    [Test]
    public async Task ErrorResultBindErrorRecoversFromNullFailureAndPassesNullSuccessThrough()
    {
        var binderReceivedNull = false;
        var binderCalledForSuccess = false;
        var failureNull =
            Result.Failure<string?, string?>(null).AsErrorResult();
        var successNull =
            Result.Return<string?, string?>(null).AsErrorResult();

        var recovered = failureNull.BindError(error =>
        {
            binderReceivedNull = error is null;
            return Result.Return<string?, string?>(null).AsErrorResult();
        });
        var passedThrough = successNull.BindError(_ =>
        {
            binderCalledForSuccess = true;
            return failureNull;
        });

        await Assert.That(binderReceivedNull).IsTrue();
        await Assert.That(recovered.ToResult() is Result<string, string>.Success(null)).IsTrue();
        await Assert.That(passedThrough.ToResult() is Result<string, string>.Success(null)).IsTrue();
        await Assert.That(binderCalledForSuccess).IsFalse();
    }

    [Test]
    public async Task ErrorResultApplyPassesNullSuccessAndMapsNullFailure()
    {
        var successFunction =
            Result.Return<string?, Func<string?, string>>(null).AsErrorResult();
        var failureFunction =
            Result.Failure<string?, Func<string?, string>>(value =>
                value is null ? "function-received-null-error" : value).AsErrorResult();
        var nullFailure =
            Result.Failure<string?, string?>(null).AsErrorResult();

        var passedThrough = successFunction.Apply(nullFailure);
        var appliedFailure = failureFunction.Apply(nullFailure);

        await Assert.That(passedThrough.ToResult() is Result<string, string>.Success(null)).IsTrue();
        await Assert.That(appliedFailure.ToResult() is Result<string, string>.Failure("function-received-null-error")).IsTrue();
    }

    [Test]
    public async Task ErrorResultSelectManyPropagatesNullPassThroughAndFailurePayloads()
    {
        var sourceSuccess =
            Result.Return<string?, string?>(null).AsErrorResult();
        var sourceFailure =
            Result.Failure<string?, string?>(null).AsErrorResult();
        var nestedSuccess =
            Result.Return<string?, string?>(null).AsErrorResult();
        var nestedFailure =
            Result.Failure<string?, string?>(null).AsErrorResult();

        var sourceSuccessPassThrough = sourceSuccess.SelectMany(
            _ => nestedFailure,
            (_, _) => "should-not-be-projected");
        var nestedSuccessPassThrough = sourceFailure.SelectMany(
            _ => nestedSuccess,
            (_, _) => "should-not-be-projected");
        var projectedFailures = sourceFailure.SelectMany(
            _ => nestedFailure,
            (first, second) =>
                first is null && second is null ? "both-errors-null" : "unexpected-error");

        await Assert.That(sourceSuccessPassThrough.ToResult()
            is Result<string, string>.Success(null)).IsTrue();
        await Assert.That(nestedSuccessPassThrough.ToResult()
            is Result<string, string>.Success(null)).IsTrue();
        await Assert.That(projectedFailures.ToResult()
            is Result<string, string>.Failure("both-errors-null")).IsTrue();
    }
}
