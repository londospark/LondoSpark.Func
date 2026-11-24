using System;
using System.Linq;
using System.Threading.Tasks;

using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace LondoSpark.Func.Tests;

public sealed class NonEmptyListTests
{
    [Test]
    public async Task AppendingTwiceToTheSameListLeavesBothResultsIntact()
    {
        // The first append grows a buffer with spare capacity; the second append to the same receiver must not
        // overwrite the slots the first result already owns.
        var start = new NonEmptyList<int>(1).Append(new NonEmptyList<int>(2));
        var left = start.Append(new NonEmptyList<int>(3));
        var right = start.Append(new NonEmptyList<int>(4, 5));

        await Assert.That(start.ToArray()).IsEquivalentTo([1, 2]);
        await Assert.That(left.ToArray()).IsEquivalentTo([1, 2, 3]);
        await Assert.That(right.ToArray()).IsEquivalentTo([1, 2, 4, 5]);
    }

    [Test]
    public async Task LongApplicativeChainsAccumulateEveryErrorInOrder()
    {
        var errors = new NonEmptyList<int>(0);
        for (var index = 1; index < 1_000; index++)
        {
            errors = errors.Append(new NonEmptyList<int>(index));
        }

        await Assert.That(errors.Count).IsEqualTo(1_000);
        await Assert.That(errors.AsSpan().SequenceEqual(Enumerable.Range(0, 1_000).ToArray())).IsTrue();
    }

    [Test]
    public async Task AppendingAListToItselfDuplicatesItsItems()
    {
        var list = new NonEmptyList<string>("a").Append(new NonEmptyList<string>("b"));

        var doubled = list.Append(list);

        await Assert.That(doubled.ToSeparatedString(",")).IsEqualTo("a,b,a,b");
    }

    [Test]
    public async Task IndexerRejectsPositionsInsideSpareCapacity()
    {
        var list = new NonEmptyList<int>(1, 2, 3).Append(new NonEmptyList<int>(4));

        await Assert.That(list.Count).IsEqualTo(4);
        await Assert.That(() => list[4]).Throws<ArgumentOutOfRangeException>();
        await Assert.That(() => list[-1]).Throws<ArgumentOutOfRangeException>();
    }

    [Test]
    public async Task EnumerationVisitsOnlyTheListsOwnItems()
    {
        var shorter = new NonEmptyList<int>(1).Append(new NonEmptyList<int>(2));
        _ = shorter.Append(new NonEmptyList<int>(3));

        var visited = 0;
        foreach (var item in shorter)
        {
            visited += item;
        }

        await Assert.That(visited).IsEqualTo(3);
        await Assert.That(Enumerable.Sum(shorter)).IsEqualTo(3);
    }

    [Test]
    public async Task SeparatedStringRendersNullItemsAsEmpty()
    {
        var list = new NonEmptyList<string?>(null, "b", null);

        await Assert.That(list.ToSeparatedString("|")).IsEqualTo("|b|");
        await Assert.That(new NonEmptyList<int>(7).ToSeparatedString(", ")).IsEqualTo("7");
    }

    [Test]
    public async Task ValidationEqualityAndRendering()
    {
        var errors = Validation.Failure<int, string>("a", "b");

        await Assert.That(Validation.Return<int, string>(1)).IsEqualTo(Validation.Return<int, string>(1));
        var sameList = errors;
        await Assert.That(errors == sameList).IsTrue();
        await Assert.That(errors == Validation.Failure<int, string>("a", "b")).IsFalse();
        await Assert.That(errors.ToString()).IsEqualTo("Failure(a, b)");
        await Assert.That(Validation.Return<int, string>(1).ToString()).IsEqualTo("Success(1)");
    }
}
