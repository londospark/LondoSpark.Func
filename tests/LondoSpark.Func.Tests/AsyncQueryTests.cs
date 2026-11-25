using System.Threading.Tasks;

using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

using static LondoSpark.Func.Prelude;

namespace LondoSpark.Func.Tests;

/// <summary>Query syntax composes options and results across plain values, <see cref="Task{TResult}"/>, and <see cref="ValueTask{TResult}"/>.</summary>
public sealed class AsyncQueryTests
{
    // ----- Option -----

    [Test]
    public async Task OptionQueriesMixPlainTaskAndValueTaskSteps()
    {
        var steps = new Counter();

        var described = await (from id in FindAsync(steps, 3)
                                from name in NameAsync(steps, id)
                                from length in some(name.Length)
                                select $"{name}:{length}");

        await Assert.That(described).IsEqualTo(Option.Return("user-3:6"));
        await Assert.That(steps.Count).IsEqualTo(2);
    }

    [Test]
    public async Task OptionQueriesCanStartFromAPlainOption()
    {
        var steps = new Counter();

        var fromValueTask = await (from id in some(4)
                                   from found in FindAsync(steps, id)
                                   select id + found);
        var fromTask = await (from id in some(10)
                              from name in NameAsync(steps, id)
                              select name);

        await Assert.That(fromValueTask).IsEqualTo(Option.Return(12));
        await Assert.That(fromTask).IsEqualTo(Option.Return("user-5"));
    }

    [Test]
    public async Task OptionQueriesStopAtTheFirstAbsentStep()
    {
        var steps = new Counter();

        var missingFirst = await (from id in FindAsync(steps, 0)
                                  from name in NameAsync(steps, id)
                                  select name);
        var missingSecond = await (from id in FindAsync(steps, 1)
                                   from name in NameAsync(steps, -id)
                                   from again in FindAsync(steps, 1)
                                   select name);
        var missingPlain = await (from id in NameAsync(steps, 2)
                                  from nothing in Option.Empty<int>()
                                  select id);

        await Assert.That(missingFirst).IsEqualTo(Option.Empty<string>());
        await Assert.That(missingSecond).IsEqualTo(Option.Empty<string>());
        await Assert.That(missingPlain).IsEqualTo(Option.Empty<string>());
        await Assert.That(steps.Count).IsEqualTo(4);
    }

    [Test]
    public async Task OptionQueriesSurviveRealSuspension()
    {
        var total = await (from first in SlowAsync(some(1))
                           from second in SlowTaskAsync(some(2))
                           select first + second);
        var absent = await (from first in SlowAsync(some(1))
                            from second in SlowTaskAsync(Option.Empty<int>())
                            select first + second);
        var selected = await SlowTaskAsync(some(20)).Select(static value => value + 1);

        await Assert.That(total).IsEqualTo(Option.Return(3));
        await Assert.That(absent).IsEqualTo(Option.Empty<int>());
        await Assert.That(selected).IsEqualTo(Option.Return(21));
    }

    // ----- Result -----

    [Test]
    public async Task ResultQueriesMixPlainTaskAndValueTaskSteps()
    {
        var steps = new Counter();

        var receipt = await (from amount in Parse("40")
                             from balance in DebitAsync(steps, amount)
                             from reference in ReceiptAsync(steps, balance)
                             from echoed in Parse(reference.ToString(System.Globalization.CultureInfo.InvariantCulture))
                             select $"{amount} leaves {balance} as #{echoed}");

        await Assert.That(receipt).IsEqualTo(Result.Return<string, string>("40 leaves 60 as #61"));
        await Assert.That(steps.Count).IsEqualTo(2);
    }

    [Test]
    public async Task ResultQueriesStopAtTheFirstFailureAndKeepIt()
    {
        var steps = new Counter();

        var badInput = await (from amount in Parse("x")
                              from balance in DebitAsync(steps, amount)
                              select balance);
        var overdrawn = await (from balance in DebitAsync(steps, 500)
                               from reference in ReceiptAsync(steps, balance)
                               select reference);
        var badEcho = await (from balance in ReceiptAsync(steps, 1)
                             from echoed in Parse("y")
                             select echoed);

        await Assert.That(badInput).IsEqualTo(Result.Failure<int, string>("'x' is not a number"));
        await Assert.That(overdrawn).IsEqualTo(Result.Failure<int, string>("500 exceeds the balance"));
        await Assert.That(badEcho).IsEqualTo(Result.Failure<int, string>("'y' is not a number"));
        await Assert.That(steps.Count).IsEqualTo(2);
    }

    [Test]
    public async Task ResultQueriesSurviveRealSuspension()
    {
        var total = await (from first in SlowAsync(Result.Return<int, string>(1))
                           from second in SlowTaskAsync(Result.Return<int, string>(2))
                           select first + second);
        var failed = await (from first in SlowTaskAsync(Result.Return<int, string>(1))
                            from second in SlowAsync(Result.Failure<int, string>("late"))
                            select first + second);
        var selected = await SlowAsync(Result.Return<int, string>(20)).Select(static value => value + 1);

        await Assert.That(total).IsEqualTo(Result.Return<int, string>(3));
        await Assert.That(failed).IsEqualTo(Result.Failure<int, string>("late"));
        await Assert.That(selected).IsEqualTo(Result.Return<int, string>(21));
    }

    // ----- Helpers -----

    private sealed class Counter
    {
        public int Count { get; private set; }

        public void Step() => Count++;
    }

    private static ValueTask<Option<int>> FindAsync(Counter steps, int id)
    {
        steps.Step();
        return ValueTask.FromResult(id > 0 ? some(id * 2) : Option.Empty<int>());
    }

    private static Task<Option<string>> NameAsync(Counter steps, int id)
    {
        steps.Step();
        return Task.FromResult(id > 0 ? some($"user-{id / 2}") : Option.Empty<string>());
    }

    private static Result<int, string> Parse(string text) =>
        int.TryParse(text, out var number) ? success(number) : failure($"'{text}' is not a number");

    private static Task<Result<int, string>> DebitAsync(Counter steps, int amount)
    {
        steps.Step();
        Result<int, string> debited = amount <= 100 ? success(100 - amount) : failure($"{amount} exceeds the balance");
        return Task.FromResult(debited);
    }

    private static ValueTask<Result<int, string>> ReceiptAsync(Counter steps, int balance)
    {
        steps.Step();
        Result<int, string> receipt = success(balance + 1);
        return ValueTask.FromResult(receipt);
    }

    private static async ValueTask<T> SlowAsync<T>(T value)
    {
        await Task.Yield();
        return value;
    }

    private static async Task<T> SlowTaskAsync<T>(T value)
    {
        await Task.Yield();
        return value;
    }
}
