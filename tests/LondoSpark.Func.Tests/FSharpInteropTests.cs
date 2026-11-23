using System.Threading.Tasks;

using LondoSpark.Func;
using LondoSpark.Func.FSharp;
using Microsoft.FSharp.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace LondoSpark.Func.Tests;

public sealed class FSharpInteropTests
{
    [Test]
    public async Task FSharpNoneRoundTripsWithoutCaseLoss()
    {
        var coreOption = FSharpOptionExtensions.ToOption(FSharpOption<string>.None);

        await Assert.That(coreOption is None).IsTrue();
        await Assert.That(FSharpOptionExtensions.ToFSharpOption(coreOption) is null).IsTrue();
    }

    [Test]
    public async Task FSharpSomeRoundTripsItsValue()
    {
        var coreOption = FSharpOptionExtensions.ToOption(FSharpOption<string>.Some("present"));

        await Assert.That(coreOption is Some<string> { Value: "present" }).IsTrue();
        var roundTripped = FSharpOptionExtensions.ToFSharpOption(coreOption);
        if (roundTripped is null)
        {
            throw new System.InvalidOperationException("F# Some must remain present after round-trip.");
        }

        await Assert.That(roundTripped.Value).IsEqualTo("present");
    }

    [Test]
    public async Task FSharpSomeNullRoundTripsAsSomeNull()
    {
        var coreOption = FSharpOptionExtensions.ToOption(FSharpOption<string?>.Some(null));

        await Assert.That(coreOption is Some<string?> { Value: null }).IsTrue();
        var roundTripped = FSharpOptionExtensions.ToFSharpOption(coreOption);
        await Assert.That(roundTripped is not null).IsTrue();
        if (roundTripped is null)
        {
            throw new System.InvalidOperationException("F# Some null must remain present after round-trip.");
        }

        await Assert.That(roundTripped.Value is null).IsTrue();
    }

    [Test]
    public async Task FSharpOkRoundTripsItsSuccessCase()
    {
        var coreResult = FSharpResultExtensions.ToResult(
            FSharpResult<string, string>.NewOk("success"));

        await Assert.That(coreResult is Success<string> { Value: "success" }).IsTrue();
        var roundTripped = FSharpResultExtensions.ToFSharpResult(coreResult);
        await Assert.That(roundTripped.IsOk).IsTrue();
        await Assert.That(roundTripped.ResultValue).IsEqualTo("success");
    }

    [Test]
    public async Task FSharpErrorRoundTripsItsFailureCase()
    {
        var coreResult = FSharpResultExtensions.ToResult(
            FSharpResult<string, string>.NewError("failure"));

        await Assert.That(coreResult is Failure<string> { Value: "failure" }).IsTrue();
        var roundTripped = FSharpResultExtensions.ToFSharpResult(coreResult);
        await Assert.That(roundTripped.IsError).IsTrue();
        await Assert.That(roundTripped.ErrorValue).IsEqualTo("failure");
    }

    [Test]
    public async Task FSharpOkNullRoundTripsAsSuccessNull()
    {
        var coreResult = FSharpResultExtensions.ToResult(
            FSharpResult<string?, string?>.NewOk(null));

        await Assert.That(coreResult is Success<string?> { Value: null }).IsTrue();
        var roundTripped = FSharpResultExtensions.ToFSharpResult(coreResult);
        await Assert.That(roundTripped.IsOk).IsTrue();
        await Assert.That(roundTripped.ResultValue is null).IsTrue();
    }

    [Test]
    public async Task FSharpErrorNullRoundTripsAsFailureNull()
    {
        var coreResult = FSharpResultExtensions.ToResult(
            FSharpResult<string?, string?>.NewError(null));

        await Assert.That(coreResult is Failure<string?> { Value: null }).IsTrue();
        var roundTripped = FSharpResultExtensions.ToFSharpResult(coreResult);
        await Assert.That(roundTripped.IsError).IsTrue();
        await Assert.That(roundTripped.ErrorValue is null).IsTrue();
    }
}
