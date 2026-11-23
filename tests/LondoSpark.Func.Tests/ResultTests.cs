using System;
using System.Threading.Tasks;

using LondoSpark.Func;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace LondoSpark.Func.Tests;

public record DivideByZeroError();

public static class MathOperations
{
    public static Result<double, DivideByZeroError> Divide(double numerator, double denominator) =>
        denominator == 0
            ? new DivideByZeroError()
            : numerator / denominator;
}

public sealed class ResultTests
{
    [Test]
    public async Task ResultFunctorIdentityAndComposition()
    {
        Result<int, string> source = Result.Return<int, string>(7);
        var identity = source.Select(value => value);
        var composed = source.Select(value => value + 2).Select(value => value * 3);
        var separate = source.Select(value => (value + 2) * 3);

        await Assert.That(identity).IsEqualTo(7);
        await Assert.That(composed).IsEqualTo(separate);
    }

    [Test]
    public async Task ResultApplyUsesFunctionAndValueSuccesses()
    {
        Result<Func<int, int>, string> function = Result.Return<Func<int, int>, string>(value => value * 6);
        Result<int, string> value = Result.Return<int, string>(7);

        await Assert.That(function.Apply(value)).IsEqualTo(42);
    }

    [Test]
    public async Task ResultApplyPreservesFunctionFailureBeforeValueFailure()
    {
        var functionCallbackCalled = false;

        Result<Func<int, int>, string> functionFailure = "function failure";
        Result<int, string> valueFailure = "value failure";

        var bothFailures = functionFailure.Apply(valueFailure);
        var functionSuccessValueFailure =
            Result.Return<Func<int, int>, string>(_ => { functionCallbackCalled = true; return 0; })
                .Apply(valueFailure);
        var functionFailureValueSuccess =
            functionFailure.Apply(Result.Return<int, string>(0));

        await Assert.That(bothFailures).IsEqualTo("function failure");
        await Assert.That(functionSuccessValueFailure).IsEqualTo("value failure");
        await Assert.That(functionFailureValueSuccess).IsEqualTo("function failure");
        await Assert.That(functionCallbackCalled).IsFalse();
    }

    [Test]
    public async Task ResultBindAndSelectCallbacksDoNotRunOnFailure()
    {
        var bindCalled = false;
        var selectCalled = false;
        Result<int, string> failure = "inactive";

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

        await Assert.That(bound).IsEqualTo("inactive");
        await Assert.That(selected).IsEqualTo("inactive");
        await Assert.That(bindCalled).IsFalse();
        await Assert.That(selectCalled).IsFalse();
    }

    [Test]
    public async Task ResultBindComposesSuccessfulValues()
    {
        Result<int, string> result = 21;
        var bound = result.Bind(value => new Result<long, string>(value * 2L));

        await Assert.That(bound).IsEqualTo(42L);
    }

    [Test]
    public async Task ResultBindSkipsBinderForFailure()
    {
        var binderCalled = false;
        Result<int, string> result = "invalid";
        var bound = result.Bind(value =>
        {
            binderCalled = true;
            return new Result<int, string>(value * 2);
        });

        await Assert.That(bound).IsEqualTo("invalid");
        await Assert.That(binderCalled).IsFalse();
    }

    [Test]
    public async Task ResultQueryBindsOnSuccess()
    {
        var result = from _1 in MathOperations.Divide(10.0, 2.0)
                     from _2 in MathOperations.Divide(10.0, _1)
                     select _2;
        await Assert.That(result).IsEqualTo(2.0);
    }

    [Test]
    public async Task ResultQueryUsesProjector()
    {
        var result = from first in MathOperations.Divide(10.0, 2.0)
                     from second in MathOperations.Divide(10.0, first)
                     select first + second;
        await Assert.That(result).IsEqualTo(7.0);
    }

    [Test]
    public async Task ResultQueryProjectorChangesType()
    {
        Result<string, DivideByZeroError> result =
            from first in MathOperations.Divide(10.0, 2.0)
            from second in MathOperations.Divide(10.0, first)
            select $"{first:F0}:{second:F0}";

        await Assert.That(result).IsEqualTo("5:2");
    }

    [Test]
    public async Task ResultCanMapAndBindErrors()
    {
        var mapped = MathOperations.Divide(10.0, 0.0)
            .MapError(_ => "division failed");
        var recovered = MathOperations.Divide(10.0, 0.0)
            .BindError(_ => new Result<double, string>(42.0));

        await Assert.That(mapped).IsEqualTo("division failed");
        await Assert.That(recovered).IsEqualTo(42.0);
    }

    [Test]
    public async Task ResultQueryPreservesFirstFailureAndSkipsLaterBinder()
    {
        var laterBinderCalled = false;
        Result<int, string> failure = "first failure";

        var result = from first in failure
                     from second in ResultStep(first)
                     select first + second;

        await Assert.That(result).IsEqualTo("first failure");
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
        Result<int, string> source = Result.Return<int, string>(1);
        Result<int, string> secondFailure = "second failure";

        var result = source.SelectMany(
            _ => secondFailure,
            (first, second) =>
            {
                projectorCalled = true;
                return first + second;
            });

        await Assert.That(result).IsEqualTo("second failure");
        await Assert.That(projectorCalled).IsFalse();
    }
}
