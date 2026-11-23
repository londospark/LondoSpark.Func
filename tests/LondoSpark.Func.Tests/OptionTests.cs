using System;
using System.Threading.Tasks;

using LondoSpark.Func;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace LondoSpark.Func.Tests;

public sealed class OptionTests
{
    [Test]
    public async Task OptionFunctorIdentityAndComposition()
    {
        Option<int> source = Option.Return(7);
        var identity = source.Select(value => value);
        var composed = source.Select(value => value + 2).Select(value => value * 3);
        var separate = source.Select(value => (value + 2) * 3);

        await Assert.That(identity).IsEqualTo(7);
        await Assert.That(composed).IsEqualTo(separate);
    }

    [Test]
    public async Task OptionApplyCombinesPresentValues()
    {
        Option<Func<int, int>> addOne = Option.Return<Func<int, int>>(value => value + 1);
        Option<int> value = Option.Return(41);

        await Assert.That(addOne.Apply(value)).IsEqualTo(42);
    }

    [Test]
    public async Task OptionApplyShortCircuitsBothInactiveCombinations()
    {
        var functionCalled = false;

        Option<Func<int, int>> absentFunction = new None();
        Option<int> presentValue = Option.Return(10);
        Option<Func<int, int>> presentFunction =
            Option.Return<Func<int, int>>(value =>
            {
                functionCalled = true;
                return value + 1;
            });
        Option<int> absentValue = new None();

        var absentFunctionResult = absentFunction.Apply(presentValue);
        var absentValueResult = presentFunction.Apply(absentValue);
        var bothAbsentResult = absentFunction.Apply(absentValue);

        await Assert.That(absentFunctionResult is None).IsTrue();
        await Assert.That(absentValueResult is None).IsTrue();
        await Assert.That(bothAbsentResult is None).IsTrue();
        await Assert.That(functionCalled).IsFalse();
    }

    [Test]
    public async Task OptionBindComposesPresentValues()
    {
        Option<int> option = Option.Return(21);
        var result = option.Bind(value => Option.Return(value * 2));

        await Assert.That(result).IsEqualTo(42);
    }

    [Test]
    public async Task OptionBindSkipsBinderForNone()
    {
        var binderCalled = false;
        Option<int> option = new None();
        var result = option.Bind(value =>
        {
            binderCalled = true;
            return Option.Return(value * 2);
        });

        await Assert.That(result is None).IsTrue();
        await Assert.That(binderCalled).IsFalse();
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
    public async Task OptionQuerySkipsProjectorWhenSecondComputationIsNone()
    {
        var projectorCalled = false;
        Option<int> source = Option.Return(1);
        Option<int> absent = new None();

        var result = source.SelectMany(
            _ => absent,
            (first, second) =>
            {
                projectorCalled = true;
                return first + second;
            });

        await Assert.That(result is None).IsTrue();
        await Assert.That(projectorCalled).IsFalse();
    }
}
