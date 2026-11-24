using System;
using System.Threading.Tasks;

using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace LondoSpark.Func.Tests;

/// <summary>Guards the zero-allocation paths so a regression fails CI rather than surfacing in a profiler.</summary>
/// <remarks>
/// Each operation is warmed up first so JIT and static initialisation are excluded, then measured over many
/// iterations on the current thread. Delegates are static lambdas, which the compiler caches.
/// </remarks>
public sealed class AllocationTests
{
    private const int Iterations = 1_000;

    private static readonly Option<int> SomeValue = Option.Return(41);
    private static readonly Option<int> NoneValue = Option.Empty<int>();
    private static readonly Result<int, string> Successful = Result.Return<int, string>(41);
    private static readonly Result<int, string> Failed = Result.Failure<int, string>("failed");
    private static readonly Result<Func<int, int>, string> ResultIncrement =
        Result.Return<Func<int, int>, string>(static value => value + 1);
    private static readonly Option<Func<int, int>> OptionIncrement =
        Option.Return<Func<int, int>>(static value => value + 1);
    private static readonly Validation<Func<int, int>, string> ValidationIncrement =
        Validation.Return<Func<int, int>, string>(static value => value + 1);
    private static readonly NonEmptyList<int> List = new(1, 2, 3);

    [Test]
    public async Task OptionCompositionDoesNotAllocate()
    {
        await AssertNoAllocation(static () => SomeValue.Select(static value => value + 1));
        await AssertNoAllocation(static () => SomeValue.Bind(static value => Option.Return(value + 1)));
        await AssertNoAllocation(static () => NoneValue.Bind(static value => Option.Return(value + 1)));
        await AssertNoAllocation(static () => SomeValue.SelectMany(
            static value => Option.Return(value), static (first, second) => first + second));
        await AssertNoAllocation(static () => OptionIncrement.Apply(SomeValue));
        await AssertNoAllocation(static () => NoneValue.Value);
        await AssertNoAllocation(static () => SomeValue == NoneValue);
        await AssertNoAllocation(static () => SomeValue.GetHashCode());
    }

    [Test]
    public async Task ResultCompositionDoesNotAllocate()
    {
        await AssertNoAllocation(static () => Successful.Select(static value => value + 1));
        await AssertNoAllocation(static () => Successful.Bind(static value => Result.Return<int, string>(value + 1)));
        await AssertNoAllocation(static () => Failed.Bind(static value => Result.Return<int, string>(value + 1)));
        await AssertNoAllocation(static () => Failed.MapError(static error => error.Length));
        await AssertNoAllocation(static () => ResultIncrement.Apply(Successful));
        await AssertNoAllocation(static () => Successful.ToExitCode());
        await AssertNoAllocation(static () => Successful == Failed);
    }

    [Test]
    public async Task ValidationSuccessPathDoesNotAllocate()
    {
        await AssertNoAllocation(static () => Validation.Return<int, string>(41));
        await AssertNoAllocation(static () => Validation.Return<int, string>(41).Select(static value => value + 1));
        await AssertNoAllocation(static () => ValidationIncrement.Apply(Validation.Return<int, string>(41)));
        await AssertNoAllocation(static () => Validation.Return<int, string>(41).ToResult());
    }

    [Test]
    public async Task NonEmptyListEnumerationDoesNotAllocate()
    {
        await AssertNoAllocation(static () =>
        {
            var total = 0;
            foreach (var item in List)
            {
                total += item;
            }

            return total;
        });
    }

    private static async Task AssertNoAllocation<T>(Func<T> operation)
    {
        for (var warmup = 0; warmup < 100; warmup++)
        {
            _ = operation();
        }

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var iteration = 0; iteration < Iterations; iteration++)
        {
            _ = operation();
        }

        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        await Assert.That(allocated).IsEqualTo(0L);
    }
}
