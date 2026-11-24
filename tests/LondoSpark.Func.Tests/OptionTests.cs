using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;
using static LondoSpark.Func.Option<int>;

namespace LondoSpark.Func.Tests;

public sealed class OptionTests
{
    [Test]
    public async Task OptionFunctorIdentityAndComposition()
    {
        var source = Option.Return(7);
        var identity = source.Select(value => value);
        var composed = source.Select(value => value + 2).Select(value => value * 3);
        var separate = source.Select(value => (value + 2) * 3);

        await Assert.That(identity is Some(7)).IsTrue();
        await Assert.That(composed is Some(27)).IsTrue();
        await Assert.That(separate is Some(27)).IsTrue();
    }

    [Test]
    public async Task OptionApplyCombinesPresentValues()
    {
        var addOne = Option.Return<Func<int, int>>(value => value + 1);
        var value = Option.Return(41);

        await Assert.That(addOne.Apply(value) is Some(42)).IsTrue();
    }

    [Test]
    public async Task OptionApplyShortCircuitsBothInactiveCombinations()
    {
        var functionCalled = false;

        var absentFunction = Option.Empty<Func<int, int>>();
        var presentValue = Option.Return(10);
        var presentFunction =
            Option.Return<Func<int, int>>(value =>
            {
                functionCalled = true;
                return value + 1;
            });
        var absentValue = Option.Empty<int>();

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
        var option = Option.Return(21);
        var result = option.Bind(value => Option.Return(value * 2));

        await Assert.That(result is Some(42)).IsTrue();
    }

    [Test]
    public async Task OptionBindSkipsBinderForNone()
    {
        var binderCalled = false;
        var option = Option.Empty<int>();
        var result = option.Bind(value =>
        {
            binderCalled = true;
            return Option.Return(value * 2);
        });

        await Assert.That(result is None).IsTrue();
        await Assert.That(binderCalled).IsFalse();
    }

    [Test]
    public async Task OptionEmptyEqualsExplicitNoneCase()
    {
        var empty = Option.Empty<int>();
        Option<int> explicitNone = new None();

        await Assert.That(empty).IsEqualTo(explicitNone);
    }

    [Test]
    public async Task OptionDefaultIsNone()
    {
        Option<int> defaultOption = default;
        var explicitNone = Option.Empty<int>();

        await Assert.That(defaultOption.HasValue).IsTrue();
        await Assert.That(defaultOption.Value is None).IsTrue();
        await Assert.That(defaultOption is None).IsTrue();
        await Assert.That(defaultOption).IsEqualTo(explicitNone);
    }

    [Test]
    public async Task OptionEqualityComparesCaseAndPayload()
    {
        var some = Option.Return(3);
        Option<int> sameSome = new Some(3);
        var otherSome = Option.Return(4);
        var someNull = Option.Return<string?>(null);
        var noneNull = Option.Empty<string?>();

        await Assert.That(some).IsEqualTo(sameSome);
        await Assert.That(some == sameSome).IsTrue();
        await Assert.That(some != otherSome).IsTrue();
        await Assert.That(some.Equals((object)sameSome)).IsTrue();
        await Assert.That(some.GetHashCode()).IsEqualTo(sameSome.GetHashCode());
        await Assert.That(someNull == noneNull).IsFalse();
        await Assert.That(Option.Empty<int>().GetHashCode()).IsEqualTo(default(Option<int>).GetHashCode());
    }

    [Test]
    public async Task OptionToStringRendersTheActiveCase()
    {
        await Assert.That(Option.Return(42).ToString()).IsEqualTo("Some(42)");
        await Assert.That(Option.Empty<int>().ToString()).IsEqualTo("None");
        await Assert.That(Option.Return<string?>(null).ToString()).IsEqualTo("Some(null)");
    }

    [Test]
    public async Task NoneValuesAreAlwaysEqual()
    {
        None first = new();
        None second = default;
        var third = new None();

        await Assert.That(first).IsEqualTo(second);
        await Assert.That(first).IsEqualTo(third);
        await Assert.That(first == second).IsTrue();
        await Assert.That(first != third).IsFalse();
        await Assert.That(first.GetHashCode()).IsEqualTo(second.GetHashCode());
        await Assert.That(first.GetHashCode()).IsEqualTo(third.GetHashCode());
    }

    [Test]
    public async Task OptionEveryNoneValueIsEqualAndHashesAlike()
    {
        Option<int> fromDefault = default;
        var fromEmpty = Option.Empty<int>();
        var fromNoneCase = new Option<int>(new None());
        Option<int> fromParameterlessNew = new();
        Option<string?> fromDefaultNullable = default;
        var fromEmptyNullable = Option.Empty<string?>();

        await Assert.That(fromDefault).IsEqualTo(fromEmpty);
        await Assert.That(fromEmpty).IsEqualTo(fromNoneCase);
        await Assert.That(fromNoneCase).IsEqualTo(fromParameterlessNew);
        await Assert.That(fromDefault == fromParameterlessNew).IsTrue();
        await Assert.That(fromDefault.GetHashCode()).IsEqualTo(fromEmpty.GetHashCode());
        await Assert.That(fromEmpty.GetHashCode()).IsEqualTo(fromNoneCase.GetHashCode());
        await Assert.That(fromNoneCase.GetHashCode()).IsEqualTo(fromParameterlessNew.GetHashCode());
        await Assert.That(fromDefaultNullable).IsEqualTo(fromEmptyNullable);
        await Assert.That(fromDefaultNullable.GetHashCode()).IsEqualTo(fromEmptyNullable.GetHashCode());
    }

    [Test]
    public async Task OptionConvertsNullAndSupportsLinq()
    {
        var some = "value".ToOption();
        var none = ((string?)null).ToOption();
        var someNumber = OptionExtensions.ToOption<int>(5);
        var noNumber = ((int?)null).ToOption();

        var result = from first in some
                     from second in $"{first}-next".ToOption()
                     select $"{first}:{second}";

        var shortCircuited = from first in none
                             from second in $"{first}-next".ToOption()
                             select second;

        await Assert.That(result is Option<string>.Some("value:value-next")).IsTrue();
        await Assert.That(none is None).IsTrue();
        await Assert.That(shortCircuited is None).IsTrue();
        await Assert.That(someNumber is Some(5)).IsTrue();
        await Assert.That(noNumber is None).IsTrue();
    }

    [Test]
    public async Task OptionQuerySkipsProjectorWhenSecondComputationIsNone()
    {
        var projectorCalled = false;
        var source = Option.Return(1);
        var absent = Option.Empty<int>();

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

    [Test]
    public async Task OptionSomeNullRemainsDistinctFromNone()
    {
        var someNull = Option.Return<string?>(null);
        var none = Option.Empty<string?>();

        var observedSome = someNull.Select(value => value is null ? "some-null" : "some-value");
        var observedNone = none.Select(_ => "should-not-be-produced");

        await Assert.That(observedSome is Option<string>.Some("some-null")).IsTrue();
        await Assert.That(observedNone is None).IsTrue();
    }

    [Test]
    public async Task OptionSelectPreservesNullPayloadsAndNullResults()
    {
        var someNull = Option.Return<string?>(null);

        var mappedNullPayload = someNull.Select(value =>
            value is null ? "mapped-null-payload" : "unexpected-value");
        var nullResult = someNull.Select(_ => (string?)null);
        var observedNullResult = nullResult.Select(value =>
            value is null ? "still-some-null" : "unexpected-value");

        await Assert.That(mappedNullPayload is Option<string>.Some("mapped-null-payload")).IsTrue();
        await Assert.That(observedNullResult is Option<string>.Some("still-some-null")).IsTrue();
    }

    [Test]
    public async Task OptionBindReceivesNullAndCanReturnPresentNull()
    {
        var binderReceivedNull = false;
        var someNull = Option.Return<string?>(null);

        var bound = someNull.Bind(value =>
        {
            binderReceivedNull = value is null;
            return Option.Return<string?>(null);
        });
        var observedBound = bound.Select(value =>
            value is null ? "bound-some-null" : "unexpected-value");

        var noneBinderCalled = false;
        var none = Option.Empty<string?>();
        var boundNone = none.Bind(value =>
        {
            noneBinderCalled = true;
            return Option.Return(value);
        });

        await Assert.That(binderReceivedNull).IsTrue();
        await Assert.That(observedBound is Option<string>.Some("bound-some-null")).IsTrue();
        await Assert.That(boundNone is None).IsTrue();
        await Assert.That(noneBinderCalled).IsFalse();
    }

    [Test]
    public async Task OptionApplyPassesPresentNullToFunction()
    {
        var function =
            Option.Return<Func<string?, string>>(value =>
                value is null ? "received-null" : value);
        var someNull = Option.Return<string?>(null);

        var applied = function.Apply(someNull);

        await Assert.That(applied is Option<string>.Some("received-null")).IsTrue();
    }

    [Test]
    public async Task OptionSelectManyComposesNullPayloadsAndPreservesNone()
    {
        var someNull = Option.Return<string?>(null);
        var none = Option.Empty<string?>();

        var composed = someNull.SelectMany(
            _ => Option.Return<string?>(null),
            (first, second) =>
                first is null && second is null ? "both-options-present-null" : "unexpected-value");
        var absent = none.SelectMany(
            _ => Option.Return<string?>(null),
            (_, _) => "should-not-be-produced");

        await Assert.That(composed is Option<string>.Some("both-options-present-null")).IsTrue();
        await Assert.That(absent is None).IsTrue();
    }

    [Test]
    public async Task OptionIgnoresPayloadOutsideTheSomeCase()
    {
        await Assert.That(Unsafe.SizeOf<Option<int>>()).IsEqualTo(Unsafe.SizeOf<OptionMirror<int>>());

        // The absent case is tag 0. Give the unused present-case slot a value that
        // None can never carry and confirm the value is still just None.
        Option<int> noneWithStalePayload = default;
        Unsafe.As<Option<int>, OptionMirror<int>>(ref noneWithStalePayload) =
            new(new Some(42), 0);

        await Assert.That(noneWithStalePayload is None).IsTrue();
        await Assert.That(noneWithStalePayload).IsEqualTo(Option.Empty<int>());
        await Assert.That(noneWithStalePayload == Option.Empty<int>()).IsTrue();
        await Assert.That(noneWithStalePayload.GetHashCode()).IsEqualTo(Option.Empty<int>().GetHashCode());
    }

    private readonly struct OptionMirror<T>
    {
        public OptionMirror(Some someValue, byte tag)
        {
            SomeValue = someValue;
            Tag = tag;
        }

        public Some SomeValue { get; }

        public byte Tag { get; }
    }
}
