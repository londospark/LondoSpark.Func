using System;
using System.Threading.Tasks;

using LondoSpark.Func;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace LondoSpark.Func.Tests;

public sealed class ValidationTests
{
    [Test]
    public async Task ValidationFunctorIdentityAndComposition()
    {
        Validation<int, string> source = Validation.Return<int, string>(7);
        var identity = source.Select(value => value);
        var composed = source.Select(value => value + 2).Select(value => value * 3);
        var separate = source.Select(value => (value + 2) * 3);

        await Assert.That(identity).IsEqualTo(7);
        await Assert.That(composed).IsEqualTo(separate);
    }

    [Test]
    public async Task ValidationApplyReturnsSuccessWhenBothInputsPass()
    {
        Validation<Func<int, int>, string> function =
            Validation.Return<Func<int, int>, string>(value => value * 6);
        Validation<int, string> value = Validation.Return<int, string>(7);

        await Assert.That(function.Apply(value)).IsEqualTo(42);
    }

    [Test]
    public async Task ValidationApplyCollectsFunctionErrorsBeforeValueErrors()
    {
        var callbackCalled = false;
        Validation<Func<int, int>, string> functionErrors =
            Validation.Failure<Func<int, int>, string>("function 1", "function 2");
        Validation<int, string> valueErrors =
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
        Validation<Func<int, int>, string> function =
            Validation.Return<Func<int, int>, string>(value =>
            {
                callbackCalled = true;
                return value + 1;
            });
        Validation<int, string> value = Validation.Return<int, string>(41);

        var applied = function.Apply(value);

        await Assert.That(applied).IsEqualTo(42);
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

    private static string ValidationErrors<TSuccess>(
        Validation<TSuccess, string> validation)
        where TSuccess : notnull =>
        validation switch
        {
            TSuccess _ => "",
            NonEmptyList<string> errors => string.Join("|", errors)
        };
}
