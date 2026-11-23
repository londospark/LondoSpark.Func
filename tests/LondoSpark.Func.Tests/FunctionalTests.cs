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

public sealed class FunctionalTests
{
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
    public async Task OptionConvertsNullAndSupportsLinq()
    {
        Option<string> some = "value".ToOption();
        Option<string> none = ((string?)null).ToOption();
        Option<int> someNumber = OptionExtensions.ToOption<int>(5);
        Option<int> noNumber = ((int?)null).ToOption();

        var result = from first in some
                     from second in $"{first}-next".ToOption()
                     select $"{first}:{second}";

        var shortCircuited = from first in none
                             from second in $"{first}-next".ToOption()
                             select second;

        await Assert.That(result).IsEqualTo("value:value-next");
        await Assert.That(none is None).IsTrue();
        await Assert.That(shortCircuited is None).IsTrue();
        await Assert.That(someNumber).IsEqualTo(5);
        await Assert.That(noNumber is None).IsTrue();
    }

    [Test]
    public async Task OptionAndUnitResultConvertBothWays()
    {
        Option<string> some = "value".ToOption();
        Option<string> none = ((string?)null).ToOption();

        Result<string, Unit> successResult = some.ToResult();
        Result<string, Unit> failureResult = none.ToResult();

        await Assert.That(successResult.ToOption()).IsEqualTo("value");
        await Assert.That(failureResult is Unit).IsTrue();
        await Assert.That(failureResult.ToOption() is None).IsTrue();
        await Assert.That(Unit.Value).IsEqualTo(default(Unit));
    }
}
