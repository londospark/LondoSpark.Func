using System;
using System.Runtime.CompilerServices;
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

        await Assert.That(identity is Some<int> { Value: 7 }).IsTrue();
        await Assert.That(composed is Some<int> { Value: 27 }).IsTrue();
        await Assert.That(separate is Some<int> { Value: 27 }).IsTrue();
    }

    [Test]
    public async Task OptionApplyCombinesPresentValues()
    {
        Option<Func<int, int>> addOne = Option.Return<Func<int, int>>(value => value + 1);
        Option<int> value = Option.Return(41);

        await Assert.That(addOne.Apply(value) is Some<int> { Value: 42 }).IsTrue();
    }

    [Test]
    public async Task OptionApplyShortCircuitsBothInactiveCombinations()
    {
        var functionCalled = false;

        Option<Func<int, int>> absentFunction = Option.Empty<Func<int, int>>();
        Option<int> presentValue = Option.Return(10);
        Option<Func<int, int>> presentFunction =
            Option.Return<Func<int, int>>(value =>
            {
                functionCalled = true;
                return value + 1;
            });
        Option<int> absentValue = Option.Empty<int>();

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

        await Assert.That(result is Some<int> { Value: 42 }).IsTrue();
    }

    [Test]
    public async Task OptionBindSkipsBinderForNone()
    {
        var binderCalled = false;
        Option<int> option = Option.Empty<int>();
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
        Option<int> empty = Option.Empty<int>();
        Option<int> explicitNone = new None();

        await Assert.That(empty).IsEqualTo(explicitNone);
    }

    [Test]
    public async Task OptionDefaultIsNone()
    {
        Option<int> defaultOption = default;
        Option<int> explicitNone = Option.Empty<int>();

        await Assert.That(defaultOption.HasValue).IsTrue();
        await Assert.That(defaultOption.Value is None).IsTrue();
        await Assert.That(defaultOption is None).IsTrue();
        await Assert.That(defaultOption).IsEqualTo(explicitNone);
    }

    [Test]
    public async Task OptionEqualityComparesCaseAndPayload()
    {
        Option<int> some = Option.Return(3);
        Option<int> sameSome = new Some<int>(3);
        Option<int> otherSome = Option.Return(4);
        Option<string?> someNull = Option.Return<string?>(null);
        Option<string?> noneNull = Option.Empty<string?>();

        await Assert.That(some).IsEqualTo(sameSome);
        await Assert.That(some == sameSome).IsTrue();
        await Assert.That(some != otherSome).IsTrue();
        await Assert.That(some.Equals((object)sameSome)).IsTrue();
        await Assert.That(some.GetHashCode()).IsEqualTo(sameSome.GetHashCode());
        await Assert.That(someNull == noneNull).IsFalse();
        await Assert.That(Option.Empty<int>().GetHashCode()).IsEqualTo(default(Option<int>).GetHashCode());
    }

    [Test]
    public async Task NoneValuesAreAlwaysEqual()
    {
        None first = new();
        None second = default;
        None third = new None();

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
        Option<int> fromEmpty = Option.Empty<int>();
        Option<int> fromNoneCase = new Option<int>(new None());
        Option<int> fromParameterlessNew = new();
        Option<string?> fromDefaultNullable = default;
        Option<string?> fromEmptyNullable = Option.Empty<string?>();

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

        await Assert.That(result is Some<string> { Value: "value:value-next" }).IsTrue();
        await Assert.That(none is None).IsTrue();
        await Assert.That(shortCircuited is None).IsTrue();
        await Assert.That(someNumber is Some<int> { Value: 5 }).IsTrue();
        await Assert.That(noNumber is None).IsTrue();
    }

    [Test]
    public async Task OptionQuerySkipsProjectorWhenSecondComputationIsNone()
    {
        var projectorCalled = false;
        Option<int> source = Option.Return(1);
        Option<int> absent = Option.Empty<int>();

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
        Option<string?> someNull = Option.Return<string?>(null);
        Option<string?> none = Option.Empty<string?>();

        var observedSome = someNull.Select(value => value is null ? "some-null" : "some-value");
        var observedNone = none.Select(_ => "should-not-be-produced");

        await Assert.That(observedSome is Some<string> { Value: "some-null" }).IsTrue();
        await Assert.That(observedNone is None).IsTrue();
    }

    [Test]
    public async Task OptionSelectPreservesNullPayloadsAndNullResults()
    {
        Option<string?> someNull = Option.Return<string?>(null);

        var mappedNullPayload = someNull.Select(value =>
            value is null ? "mapped-null-payload" : "unexpected-value");
        Option<string?> nullResult = someNull.Select(_ => (string?)null);
        var observedNullResult = nullResult.Select(value =>
            value is null ? "still-some-null" : "unexpected-value");

        await Assert.That(mappedNullPayload is Some<string> { Value: "mapped-null-payload" }).IsTrue();
        await Assert.That(observedNullResult is Some<string> { Value: "still-some-null" }).IsTrue();
    }

    [Test]
    public async Task OptionBindReceivesNullAndCanReturnPresentNull()
    {
        var binderReceivedNull = false;
        Option<string?> someNull = Option.Return<string?>(null);

        var bound = someNull.Bind(value =>
        {
            binderReceivedNull = value is null;
            return Option.Return<string?>(null);
        });
        var observedBound = bound.Select(value =>
            value is null ? "bound-some-null" : "unexpected-value");

        var noneBinderCalled = false;
        Option<string?> none = Option.Empty<string?>();
        var boundNone = none.Bind(value =>
        {
            noneBinderCalled = true;
            return Option.Return(value);
        });

        await Assert.That(binderReceivedNull).IsTrue();
        await Assert.That(observedBound is Some<string> { Value: "bound-some-null" }).IsTrue();
        await Assert.That(boundNone is None).IsTrue();
        await Assert.That(noneBinderCalled).IsFalse();
    }

    [Test]
    public async Task OptionApplyPassesPresentNullToFunction()
    {
        Option<Func<string?, string>> function =
            Option.Return<Func<string?, string>>(value =>
                value is null ? "received-null" : value);
        Option<string?> someNull = Option.Return<string?>(null);

        var applied = function.Apply(someNull);

        await Assert.That(applied is Some<string> { Value: "received-null" }).IsTrue();
    }

    [Test]
    public async Task OptionSelectManyComposesNullPayloadsAndPreservesNone()
    {
        Option<string?> someNull = Option.Return<string?>(null);
        Option<string?> none = Option.Empty<string?>();

        var composed = someNull.SelectMany(
            _ => Option.Return<string?>(null),
            (first, second) =>
                first is null && second is null ? "both-options-present-null" : "unexpected-value");
        var absent = none.SelectMany(
            _ => Option.Return<string?>(null),
            (_, _) => "should-not-be-produced");

        await Assert.That(composed is Some<string> { Value: "both-options-present-null" }).IsTrue();
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
            new(new Some<int>(42), 0);

        await Assert.That(noneWithStalePayload is None).IsTrue();
        await Assert.That(noneWithStalePayload).IsEqualTo(Option.Empty<int>());
        await Assert.That(noneWithStalePayload == Option.Empty<int>()).IsTrue();
        await Assert.That(noneWithStalePayload.GetHashCode()).IsEqualTo(Option.Empty<int>().GetHashCode());
    }

    private readonly struct OptionMirror<T>
    {
        public OptionMirror(Some<T> someValue, byte tag)
        {
            SomeValue = someValue;
            Tag = tag;
        }

        public Some<T> SomeValue { get; }

        public byte Tag { get; }
    }
}
