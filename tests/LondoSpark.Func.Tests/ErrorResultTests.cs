using System;
using System.Threading.Tasks;

using LondoSpark.Func;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace LondoSpark.Func.Tests;

public sealed class ErrorResultTests
{
    [Test]
    public async Task ErrorResultReturnAndFunctorPreserveFailureFocus()
    {
        ErrorResult<int, string> failure = ErrorResult.Return<int, string>("first");
        var mapped = failure.Select(error => $"{error}:mapped");
        var success = Result.Return<int, string>(7).AsErrorResult();
        var successMapped = success.Select(error => $"{error}:mapped");

        await Assert.That(mapped.ToResult() is Failure<string> { Value: "first:mapped" }).IsTrue();
        await Assert.That(successMapped.ToResult() is Success<int> { Value: 7 }).IsTrue();
    }

    [Test]
    public async Task ErrorResultFunctorIdentityAndComposition()
    {
        ErrorResult<int, string> source = ErrorResult.Return<int, string>("error");
        var identity = source.Select(error => error);
        var composed = source.Select(error => error + "-one").Select(error => error + "-two");
        var separate = source.Select(error => error + "-one" + "-two");

        await Assert.That(identity.ToResult() is Failure<string> { Value: "error" }).IsTrue();
        await Assert.That(composed.ToResult() is Failure<string> { Value: "error-one-two" }).IsTrue();
        await Assert.That(separate.ToResult() is Failure<string> { Value: "error-one-two" }).IsTrue();
    }

    [Test]
    public async Task ErrorResultApplyAppliesFunctionToFailure()
    {
        ErrorResult<string, Func<int, int>> function =
            ErrorResult.Return<string, Func<int, int>>(value => value + 1);
        ErrorResult<string, int> value = ErrorResult.Return<string, int>(41);

        var applied = function.Apply(value);

        await Assert.That(applied.ToResult() is Failure<int> { Value: 42 }).IsTrue();
    }

    [Test]
    public async Task ErrorResultApplyShortCircuitsSuccessPassThroughCases()
    {
        var functionCalled = false;

        ErrorResult<string, Func<int, int>> successFunction =
            Result.Return<string, Func<int, int>>("function pass-through").AsErrorResult();
        ErrorResult<string, int> failureValue = ErrorResult.Return<string, int>(41);
        ErrorResult<string, Func<int, int>> failureFunction =
            ErrorResult.Return<string, Func<int, int>>(value =>
            {
                functionCalled = true;
                return value + 1;
            });
        ErrorResult<string, int> successValue =
                Result.Return<string, int>("value pass-through").AsErrorResult();

        var successFunctionFailureValue = successFunction.Apply(failureValue);
        var failureFunctionSuccessValue = failureFunction.Apply(successValue);
        var bothFailures = failureFunction.Apply(failureValue);

        await Assert.That(successFunctionFailureValue.ToResult()
            is Success<string> { Value: "function pass-through" }).IsTrue();
        await Assert.That(failureFunctionSuccessValue.ToResult()
            is Success<string> { Value: "value pass-through" }).IsTrue();
        await Assert.That(bothFailures.ToResult() is Failure<int> { Value: 42 }).IsTrue();
        await Assert.That(functionCalled).IsTrue();
    }

    [Test]
    public async Task ErrorResultBindErrorCallbacksDoNotRunOnSuccess()
    {
        var callbackCalled = false;
        ErrorResult<int, string> success = Result.Return<int, string>(7).AsErrorResult();

        var bound = success.BindError(error =>
        {
            callbackCalled = true;
            return ErrorResult.Return<int, string>($"{error}:mapped");
        });

        await Assert.That(bound.ToResult() is Success<int> { Value: 7 }).IsTrue();
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
            is Failure<string> { Value: "division by zero: DivideByZeroError" }).IsTrue();
        await Assert.That(MathOperations.Divide(10.0, 2.0).AsErrorResult().ToResult()
            is Success<double> { Value: 5.0 }).IsTrue();
    }

    [Test]
    public async Task ErrorResultQueryPassesThroughSuccessFromSecondComputation()
    {
        var projectorCalled = false;
        ErrorResult<int, string> source = ErrorResult.Return<int, string>("first failure");
        ErrorResult<int, bool> secondSuccess = Result.Return<int, bool>(42).AsErrorResult();

        var result = source.SelectMany(
            _ => secondSuccess,
            (first, second) =>
            {
                projectorCalled = true;
                return $"{first}: {second}";
            });

        await Assert.That(result.ToResult() is Success<int> { Value: 42 }).IsTrue();
        await Assert.That(projectorCalled).IsFalse();
    }

    [Test]
    public async Task ErrorResultSelectPreservesNullSuccessAndMapsNullFailure()
    {
        var selectCalledForSuccess = false;
        ErrorResult<string?, string?> successNull =
            Result.Return<string?, string?>(null).AsErrorResult();
        ErrorResult<string?, string?> failureNull =
            Result.Failure<string?, string?>(null).AsErrorResult();

        var mappedSuccess = successNull.Select(_ =>
        {
            selectCalledForSuccess = true;
            return "should-not-be-produced";
        });
        var mappedFailure = failureNull.Select(error =>
            error is null ? "mapped-null-error" : "unexpected-error");

        await Assert.That(mappedSuccess.ToResult() is Success<string?> { Value: null }).IsTrue();
        await Assert.That(mappedFailure.ToResult() is Failure<string> { Value: "mapped-null-error" }).IsTrue();
        await Assert.That(selectCalledForSuccess).IsFalse();
    }

    [Test]
    public async Task ErrorResultBindErrorRecoversFromNullFailureAndPassesNullSuccessThrough()
    {
        var binderReceivedNull = false;
        var binderCalledForSuccess = false;
        ErrorResult<string?, string?> failureNull =
            Result.Failure<string?, string?>(null).AsErrorResult();
        ErrorResult<string?, string?> successNull =
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
        await Assert.That(recovered.ToResult() is Success<string?> { Value: null }).IsTrue();
        await Assert.That(passedThrough.ToResult() is Success<string?> { Value: null }).IsTrue();
        await Assert.That(binderCalledForSuccess).IsFalse();
    }

    [Test]
    public async Task ErrorResultApplyPassesNullSuccessAndMapsNullFailure()
    {
        ErrorResult<string?, Func<string?, string>> successFunction =
            Result.Return<string?, Func<string?, string>>(null).AsErrorResult();
        ErrorResult<string?, Func<string?, string>> failureFunction =
            Result.Failure<string?, Func<string?, string>>(value =>
                value is null ? "function-received-null-error" : value).AsErrorResult();
        ErrorResult<string?, string?> nullFailure =
            Result.Failure<string?, string?>(null).AsErrorResult();

        var passedThrough = successFunction.Apply(nullFailure);
        var appliedFailure = failureFunction.Apply(nullFailure);

        await Assert.That(passedThrough.ToResult() is Success<string?> { Value: null }).IsTrue();
        await Assert.That(appliedFailure.ToResult() is Failure<string> { Value: "function-received-null-error" }).IsTrue();
    }

    [Test]
    public async Task ErrorResultSelectManyPropagatesNullPassThroughAndFailurePayloads()
    {
        ErrorResult<string?, string?> sourceSuccess =
            Result.Return<string?, string?>(null).AsErrorResult();
        ErrorResult<string?, string?> sourceFailure =
            Result.Failure<string?, string?>(null).AsErrorResult();
        ErrorResult<string?, string?> nestedSuccess =
            Result.Return<string?, string?>(null).AsErrorResult();
        ErrorResult<string?, string?> nestedFailure =
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
            is Success<string?> { Value: null }).IsTrue();
        await Assert.That(nestedSuccessPassThrough.ToResult()
            is Success<string?> { Value: null }).IsTrue();
        await Assert.That(projectedFailures.ToResult()
            is Failure<string> { Value: "both-errors-null" }).IsTrue();
    }
}
