using System.Threading.Tasks;

using LondoSpark.Func;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace LondoSpark.Func.Tests;

public sealed class OptionResultConversionsTests
{
    [Test]
    public async Task OptionAndUnitResultConvertBothWays()
    {
        Option<string> some = "value".ToOption();
        Option<string> none = ((string?)null).ToOption();

        Result<string, Unit> successResult = some.ToResult();
        Result<string, Unit> failureResult = none.ToResult();

        await Assert.That(successResult.ToOption() is Some<string> { Value: "value" }).IsTrue();
        await Assert.That(successResult is Success<string> { Value: "value" }).IsTrue();
        await Assert.That(failureResult is Failure<Unit> { Value: var unit } && unit == Unit.Value).IsTrue();
        await Assert.That(failureResult.ToOption() is None).IsTrue();
        await Assert.That(Unit.Value).IsEqualTo(default(Unit));
    }

    [Test]
    public async Task OptionSomeAndNoneConvertToCorrespondingResultCases()
    {
        Option<int> some = Option.Return(21);
        Option<int> none = Option.Empty<int>();

        Result<int, Unit> someResult = some.ToResult();
        Result<int, Unit> noneResult = none.ToResult();

        await Assert.That(someResult is Success<int> { Value: 21 }).IsTrue();
        await Assert.That(noneResult is Failure<Unit> { Value: var unit } && unit == Unit.Value).IsTrue();
    }

    [Test]
    public async Task ResultSuccessAndFailureConvertToCorrespondingOptionCases()
    {
        Result<int, Unit> success = Result.Return<int, Unit>(21);
        Result<int, Unit> failure = Result.Failure<int, Unit>(Unit.Value);

        Option<int> successOption = success.ToOption();
        Option<int> failureOption = failure.ToOption();

        await Assert.That(successOption is Some<int> { Value: 21 }).IsTrue();
        await Assert.That(failureOption is None).IsTrue();
    }
}
