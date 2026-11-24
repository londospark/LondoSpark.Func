using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;
using static LondoSpark.Func.Result<int, string>;

namespace LondoSpark.Func.Tests;

public record DivideByZeroError();

public static class MathOperations
{
    public static Result<double, DivideByZeroError> Divide(double numerator, double denominator) =>
        denominator == 0
            ? Result.Failure<double, DivideByZeroError>(new DivideByZeroError())
            : Result.Return<double, DivideByZeroError>(numerator / denominator);
}

public sealed class ResultTests
{
    [Test]
    public async Task ResultFunctorIdentityAndComposition()
    {
        var source = Result.Return<int, string>(7);
        var identity = source.Select(value => value);
        var composed = source.Select(value => value + 2).Select(value => value * 3);
        var separate = source.Select(value => (value + 2) * 3);

        await Assert.That(identity is Success(7)).IsTrue();
        await Assert.That(composed is Success(27)).IsTrue();
        await Assert.That(separate is Success(27)).IsTrue();
    }

    [Test]
    public async Task ResultApplyUsesFunctionAndValueSuccesses()
    {
        var function = Result.Return<Func<int, int>, string>(value => value * 6);
        var value = Result.Return<int, string>(7);

        await Assert.That(function.Apply(value) is Success(42)).IsTrue();
    }

    [Test]
    public async Task ResultApplyPreservesFunctionFailureBeforeValueFailure()
    {
        var functionCallbackCalled = false;

        var functionFailure =
            Result.Failure<Func<int, int>, string>("function failure");
        var valueFailure = Result.Failure<int, string>("value failure");

        var bothFailures = functionFailure.Apply(valueFailure);
        var functionSuccessValueFailure =
            Result.Return<Func<int, int>, string>(_ => { functionCallbackCalled = true; return 0; })
                .Apply(valueFailure);
        var functionFailureValueSuccess =
            functionFailure.Apply(Result.Return<int, string>(0));

        await Assert.That(bothFailures is Failure("function failure")).IsTrue();
        await Assert.That(functionSuccessValueFailure is Failure("value failure")).IsTrue();
        await Assert.That(functionFailureValueSuccess is Failure("function failure")).IsTrue();
        await Assert.That(functionCallbackCalled).IsFalse();
    }

    [Test]
    public async Task ResultBindAndSelectCallbacksDoNotRunOnFailure()
    {
        var bindCalled = false;
        var selectCalled = false;
        var failure = Result.Failure<int, string>("inactive");

        var bound = failure.Bind(value =>
        {
            bindCalled = true;
            return Result.Return<int, string>(value);
        });
        var selected = failure.Select(value =>
        {
            selectCalled = true;
            return value + 1;
        });

        await Assert.That(bound is Failure("inactive")).IsTrue();
        await Assert.That(selected is Failure("inactive")).IsTrue();
        await Assert.That(bindCalled).IsFalse();
        await Assert.That(selectCalled).IsFalse();
    }

    [Test]
    public async Task ResultBindComposesSuccessfulValues()
    {
        var result = Result.Return<int, string>(21);
        var bound = result.Bind(value => Result.Return<long, string>(value * 2L));

        await Assert.That(bound is Result<long, string>.Success(42L)).IsTrue();
    }

    [Test]
    public async Task ResultBindSkipsBinderForFailure()
    {
        var binderCalled = false;
        var result = Result.Failure<int, string>("invalid");
        var bound = result.Bind(value =>
        {
            binderCalled = true;
            return Result.Return<int, string>(value * 2);
        });

        await Assert.That(bound is Failure("invalid")).IsTrue();
        await Assert.That(binderCalled).IsFalse();
    }

    [Test]
    public async Task ResultQueryBindsOnSuccess()
    {
        var result = from _1 in MathOperations.Divide(10.0, 2.0)
                     from _2 in MathOperations.Divide(10.0, _1)
                     select _2;
        await Assert.That(result is Result<double, DivideByZeroError>.Success(2.0)).IsTrue();
    }

    [Test]
    public async Task ResultQueryUsesProjector()
    {
        var result = from first in MathOperations.Divide(10.0, 2.0)
                     from second in MathOperations.Divide(10.0, first)
                     select first + second;
        await Assert.That(result is Result<double, DivideByZeroError>.Success(7.0)).IsTrue();
    }

    [Test]
    public async Task ResultQueryProjectorChangesType()
    {
        var result =
            from first in MathOperations.Divide(10.0, 2.0)
            from second in MathOperations.Divide(10.0, first)
            select $"{first:F0}:{second:F0}";

        await Assert.That(result is Result<string, DivideByZeroError>.Success("5:2")).IsTrue();
    }

    [Test]
    public async Task ResultCanMapAndBindErrors()
    {
        var mapped = MathOperations.Divide(10.0, 0.0)
            .MapError(_ => "division failed");
        var recovered = MathOperations.Divide(10.0, 0.0)
            .BindError(_ => Result.Return<double, string>(42.0));

        await Assert.That(mapped is Result<double, string>.Failure("division failed")).IsTrue();
        await Assert.That(recovered is Result<double, string>.Success(42.0)).IsTrue();
    }

    [Test]
    public async Task ResultQueryPreservesFirstFailureAndSkipsLaterBinder()
    {
        var laterBinderCalled = false;
        var failure = Result.Failure<int, string>("first failure");

        var result = from first in failure
                     from second in ResultStep(first)
                     select first + second;

        await Assert.That(result is Failure("first failure")).IsTrue();
        await Assert.That(laterBinderCalled).IsFalse();

        Result<int, string> ResultStep(int value)
        {
            laterBinderCalled = true;
            return Result.Return<int, string>(value + 1);
        }
    }

    [Test]
    public async Task ResultQuerySkipsProjectorWhenSecondComputationFails()
    {
        var projectorCalled = false;
        var source = Result.Return<int, string>(1);
        var secondFailure = Result.Failure<int, string>("second failure");

        var result = source.SelectMany(
            _ => secondFailure,
            (first, second) =>
            {
                projectorCalled = true;
                return first + second;
            });

        await Assert.That(result is Failure("second failure")).IsTrue();
        await Assert.That(projectorCalled).IsFalse();
    }

    [Test]
    public async Task ResultSuccessNullAndFailureNullRetainDistinctCases()
    {
        var successNull =
            Result.Return<string?, string?>(null);
        var failureNull =
            Result.Failure<string?, string?>(null);

        await Assert.That(successNull is Result<string?, string?>.Success(null)).IsTrue();
        await Assert.That(failureNull is Result<string?, string?>.Failure(null)).IsTrue();
    }

    [Test]
    public async Task ResultDefaultContainsNoCase()
    {
        Result<int, string> defaultResult = default;
        var successful = Result.Return<int, string>(1);
        var failed = Result.Failure<int, string>("invalid");

        await Assert.That(defaultResult.HasValue).IsFalse();
        await Assert.That(defaultResult is Success).IsFalse();
        await Assert.That(defaultResult is Failure).IsFalse();
        await Assert.That(successful.HasValue).IsTrue();
        await Assert.That(failed.HasValue).IsTrue();

        var exception = Assert.Throws<InvalidOperationException>(() => _ = defaultResult.Value);
        await Assert.That(exception.Message).Contains("no case");
    }

    [Test]
    public async Task ResultToStringRendersTheActiveCase()
    {
        await Assert.That(Result.Return<int, string>(7).ToString()).IsEqualTo("Success(7)");
        await Assert.That(Result.Failure<int, string>("invalid").ToString()).IsEqualTo("Failure(invalid)");
        await Assert.That(default(Result<int, string>).ToString()).IsEqualTo("Result(default)");
    }

    [Test]
    public async Task ResultToExitCodeMapsEachCase()
    {
        await Assert.That(Result.Return<int, string>(1).ToExitCode()).IsEqualTo(0);
        await Assert.That(Result.Failure<int, string>("invalid").ToExitCode()).IsEqualTo(1);
    }

    [Test]
    public async Task ResultEqualityComparesCaseAndPayload()
    {
        var success = Result.Return<int, string>(3);
        Result<int, string> sameSuccess = new Success(3);
        var otherSuccess = Result.Return<int, string>(4);
        var failure = Result.Failure<int, string>("invalid");
        Result<int, string> sameFailure = new Failure("invalid");
        var otherFailure = Result.Failure<int, string>("different");

        await Assert.That(success).IsEqualTo(sameSuccess);
        await Assert.That(success == sameSuccess).IsTrue();
        await Assert.That(success != otherSuccess).IsTrue();
        await Assert.That(success.Equals((object)sameSuccess)).IsTrue();
        await Assert.That(success.GetHashCode()).IsEqualTo(sameSuccess.GetHashCode());
        await Assert.That(failure).IsEqualTo(sameFailure);
        await Assert.That(failure != otherFailure).IsTrue();
        await Assert.That(failure.GetHashCode()).IsEqualTo(sameFailure.GetHashCode());
        await Assert.That(success == failure).IsFalse();
    }

    [Test]
    public async Task ResultSelectMapsNullSuccessAndPreservesNullFailure()
    {
        var selectCalledForSuccess = false;
        var selectCalledForFailure = false;
        var successNull =
            Result.Return<string?, string?>(null);
        var failureNull =
            Result.Failure<string?, string?>(null);

        var mappedSuccess = successNull.Select(value =>
        {
            selectCalledForSuccess = true;
            return value is null ? "mapped-null-success" : "unexpected-value";
        });
        var mappedFailure = failureNull.Select(_ =>
        {
            selectCalledForFailure = true;
            return "should-not-be-produced";
        });

        await Assert.That(mappedSuccess is Result<string, string>.Success("mapped-null-success")).IsTrue();
        await Assert.That(mappedFailure is Result<string, string>.Failure(null)).IsTrue();
        await Assert.That(selectCalledForSuccess).IsTrue();
        await Assert.That(selectCalledForFailure).IsFalse();
    }

    [Test]
    public async Task ResultBindPassesNullSuccessAndSkipsNullFailure()
    {
        var binderReceivedNull = false;
        var successNull =
            Result.Return<string?, string?>(null);
        var failureNull =
            Result.Failure<string?, string?>(null);

        var boundSuccess = successNull.Bind(value =>
        {
            binderReceivedNull = value is null;
            return Result.Return<string?, string?>(null);
        });

        var failureBinderCalled = false;
        var boundFailure = failureNull.Bind(value =>
        {
            failureBinderCalled = true;
            return Result.Return<string?, string?>(value);
        });

        await Assert.That(binderReceivedNull).IsTrue();
        await Assert.That(boundSuccess is Result<string, string>.Success(null)).IsTrue();
        await Assert.That(boundFailure is Result<string, string>.Failure(null)).IsTrue();
        await Assert.That(failureBinderCalled).IsFalse();
    }

    [Test]
    public async Task ResultMapErrorAndBindErrorPreserveNullCaseIdentity()
    {
        var errorBinderCalledForSuccess = false;
        var successNull =
            Result.Return<string?, string?>(null);
        var failureNull =
            Result.Failure<string?, string?>(null);

        var mappedSuccess = successNull.MapError(_ => "should-not-be-produced");
        var mappedFailure = failureNull.MapError(error =>
            error is null ? "mapped-null-failure" : "unexpected-error");
        var recoveredFailure = failureNull.BindError(_ =>
            Result.Return<string?, string?>(null));
        var preservedSuccess = successNull.BindError(_ =>
        {
            errorBinderCalledForSuccess = true;
            return failureNull;
        });

        await Assert.That(mappedSuccess is Result<string, string>.Success(null)).IsTrue();
        await Assert.That(mappedFailure is Result<string, string>.Failure("mapped-null-failure")).IsTrue();
        await Assert.That(recoveredFailure is Result<string, string>.Success(null)).IsTrue();
        await Assert.That(preservedSuccess is Result<string, string>.Success(null)).IsTrue();
        await Assert.That(errorBinderCalledForSuccess).IsFalse();
    }

    [Test]
    public async Task ResultApplyDistinguishesNullSuccessAndFailurePayloads()
    {
        var functionSuccess =
            Result.Return<Func<string?, string>, string?>(value =>
                value is null ? "function-received-null" : value);
        var valueSuccess =
            Result.Return<string?, string?>(null);
        var functionFailure =
            Result.Failure<Func<string?, string>, string?>(null);
        var valueFailure =
            Result.Failure<string?, string?>(null);

        var appliedSuccess = functionSuccess.Apply(valueSuccess);
        var functionFailureResult = functionFailure.Apply(valueSuccess);
        var valueFailureResult = functionSuccess.Apply(valueFailure);

        await Assert.That(appliedSuccess is Result<string, string>.Success("function-received-null")).IsTrue();
        await Assert.That(functionFailureResult is Result<string, string>.Failure(null)).IsTrue();
        await Assert.That(valueFailureResult is Result<string, string>.Failure(null)).IsTrue();
    }

    [Test]
    public async Task ResultSelectManyComposesNullSuccessesAndPreservesNullFailures()
    {
        var projectorCalled = false;
        var successNull =
            Result.Return<string?, string?>(null);
        var failureNull =
            Result.Failure<string?, string?>(null);
        var secondFailureNull =
            Result.Failure<string?, string?>(null);

        var composedSuccess = successNull.SelectMany(
            _ => Result.Return<string?, string?>(null),
            (first, second) =>
            {
                projectorCalled = true;
                return first is null && second is null ? "both-successes-null" : "unexpected-value";
            });
        var composedFailure = failureNull.SelectMany(
            _ => Result.Return<string?, string?>("should-not-run"),
            (_, _) => "should-not-be-projected");
        var composedSecondFailure = successNull.SelectMany(
            _ => secondFailureNull,
            (_, _) => "should-not-be-projected");

        await Assert.That(composedSuccess is Result<string, string>.Success("both-successes-null")).IsTrue();
        await Assert.That(composedFailure is Result<string, string>.Failure(null)).IsTrue();
        await Assert.That(composedSecondFailure is Result<string, string>.Failure(null)).IsTrue();
        await Assert.That(projectorCalled).IsTrue();
    }

    [Test]
    public async Task ResultIgnoresPayloadOutsideItsActiveCase()
    {
        await Assert.That(Unsafe.SizeOf<Result<int, int>>()).IsEqualTo(Unsafe.SizeOf<ResultMirror>());

        // Success is tag 1. Fill the unused failure slot with a value that Success
        // can never carry and confirm the value is still just the same success.
        var successWithStaleFailure = Result.Return<int, int>(1);
        Unsafe.As<Result<int, int>, ResultMirror>(ref successWithStaleFailure) =
            new(1, 99, 1);

        await Assert.That(successWithStaleFailure is Result<int, int>.Success(1)).IsTrue();
        await Assert.That(successWithStaleFailure).IsEqualTo(Result.Return<int, int>(1));
        await Assert.That(successWithStaleFailure == Result.Return<int, int>(1)).IsTrue();
        await Assert.That(successWithStaleFailure.GetHashCode())
            .IsEqualTo(Result.Return<int, int>(1).GetHashCode());
    }

    private readonly struct ResultMirror
    {
        public ResultMirror(int successValue, int failureValue, byte tag)
        {
            SuccessValue = successValue;
            FailureValue = failureValue;
            Tag = tag;
        }

        public int SuccessValue { get; }

        public int FailureValue { get; }

        public byte Tag { get; }
    }
}
