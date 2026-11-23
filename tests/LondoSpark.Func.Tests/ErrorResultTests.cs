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
        var success = new Result<int, string>(7).AsErrorResult();
        var successMapped = success.Select(error => $"{error}:mapped");

        await Assert.That(mapped.ToResult()).IsEqualTo("first:mapped");
        await Assert.That(successMapped.ToResult()).IsEqualTo(7);
    }

    [Test]
    public async Task ErrorResultFunctorIdentityAndComposition()
    {
        ErrorResult<int, string> source = ErrorResult.Return<int, string>("error");
        var identity = source.Select(error => error);
        var composed = source.Select(error => error + "-one").Select(error => error + "-two");
        var separate = source.Select(error => error + "-one" + "-two");

        await Assert.That(identity.ToResult()).IsEqualTo("error");
        await Assert.That(composed.ToResult()).IsEqualTo(separate.ToResult());
    }

    [Test]
    public async Task ErrorResultApplyAppliesFunctionToFailure()
    {
        ErrorResult<string, Func<int, int>> function =
            ErrorResult.Return<string, Func<int, int>>(value => value + 1);
        ErrorResult<string, int> value = ErrorResult.Return<string, int>(41);

        var applied = function.Apply(value);

        await Assert.That(applied.ToResult()).IsEqualTo(42);
    }

    [Test]
    public async Task ErrorResultApplyShortCircuitsSuccessPassThroughCases()
    {
        var functionCalled = false;

        ErrorResult<string, Func<int, int>> successFunction =
            new Result<string, Func<int, int>>("function pass-through").AsErrorResult();
        ErrorResult<string, int> failureValue = ErrorResult.Return<string, int>(41);
        ErrorResult<string, Func<int, int>> failureFunction =
            ErrorResult.Return<string, Func<int, int>>(value =>
            {
                functionCalled = true;
                return value + 1;
            });
        ErrorResult<string, int> successValue =
            new Result<string, int>("value pass-through").AsErrorResult();

        var successFunctionFailureValue = successFunction.Apply(failureValue);
        var failureFunctionSuccessValue = failureFunction.Apply(successValue);
        var bothFailures = failureFunction.Apply(failureValue);

        await Assert.That(successFunctionFailureValue.ToResult()).IsEqualTo("function pass-through");
        await Assert.That(failureFunctionSuccessValue.ToResult()).IsEqualTo("value pass-through");
        await Assert.That(bothFailures.ToResult()).IsEqualTo(42);
        await Assert.That(functionCalled).IsTrue();
    }

    [Test]
    public async Task ErrorResultBindErrorCallbacksDoNotRunOnSuccess()
    {
        var callbackCalled = false;
        ErrorResult<int, string> success = new Result<int, string>(7).AsErrorResult();

        var bound = success.BindError(error =>
        {
            callbackCalled = true;
            return ErrorResult.Return<int, string>($"{error}:mapped");
        });

        await Assert.That(bound.ToResult()).IsEqualTo(7);
        await Assert.That(callbackCalled).IsFalse();
    }

    [Test]
    public async Task ErrorResultQueryBindsOnFailure()
    {
        static ErrorResult<double, string> AddContext(DivideByZeroError _) =>
            new(new Result<double, string>("division by zero"));

        var result = from error in MathOperations.Divide(10.0, 0.0).AsErrorResult()
                     from context in AddContext(error)
                     select $"{context}: {error.GetType().Name}";

        await Assert.That(result.ToResult()).IsEqualTo("division by zero: DivideByZeroError");
        await Assert.That(MathOperations.Divide(10.0, 2.0).AsErrorResult().ToResult()).IsEqualTo(5.0);
    }

    [Test]
    public async Task ErrorResultQueryPassesThroughSuccessFromSecondComputation()
    {
        var projectorCalled = false;
        ErrorResult<int, string> source = ErrorResult.Return<int, string>("first failure");
        ErrorResult<int, bool> secondSuccess = new Result<int, bool>(42).AsErrorResult();

        var result = source.SelectMany(
            _ => secondSuccess,
            (first, second) =>
            {
                projectorCalled = true;
                return $"{first}: {second}";
            });

        await Assert.That(result.ToResult()).IsEqualTo(42);
        await Assert.That(projectorCalled).IsFalse();
    }
}
