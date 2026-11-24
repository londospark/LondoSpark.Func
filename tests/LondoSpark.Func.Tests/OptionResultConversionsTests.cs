using System.Threading.Tasks;

using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;
using static LondoSpark.Func.Option<int>;
using static LondoSpark.Func.Result<int, LondoSpark.Func.Unit>;

namespace LondoSpark.Func.Tests;

public sealed class OptionResultConversionsTests
{
    [Test]
    public async Task OptionAndUnitResultConvertBothWays()
    {
        var some = "value".ToOption();
        var none = ((string?)null).ToOption();

        var successResult = some.ToResult();
        var failureResult = none.ToResult();

        await Assert.That(successResult.ToOption() is Option<string>.Some("value")).IsTrue();
        await Assert.That(successResult is Result<string, Unit>.Success("value")).IsTrue();
        await Assert.That(failureResult is Result<string, Unit>.Failure(var unit) && unit == Unit.Value).IsTrue();
        await Assert.That(failureResult.ToOption() is None).IsTrue();
        await Assert.That(Unit.Value).IsEqualTo(default(Unit));
    }

    [Test]
    public async Task OptionSomeAndNoneConvertToCorrespondingResultCases()
    {
        var some = Option.Return(21);
        var none = Option.Empty<int>();

        var someResult = some.ToResult();
        var noneResult = none.ToResult();

        await Assert.That(someResult is Success(21)).IsTrue();
        await Assert.That(noneResult is Failure(var unit) && unit == Unit.Value).IsTrue();
    }

    [Test]
    public async Task ResultSuccessAndFailureConvertToCorrespondingOptionCases()
    {
        var success = Result.Return<int, Unit>(21);
        var failure = Result.Failure<int, Unit>(Unit.Value);

        var successOption = success.ToOption();
        var failureOption = failure.ToOption();

        await Assert.That(successOption is Some(21)).IsTrue();
        await Assert.That(failureOption is None).IsTrue();
    }
}
