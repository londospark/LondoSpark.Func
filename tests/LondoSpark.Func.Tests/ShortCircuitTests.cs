using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

using static LondoSpark.Func.Prelude;
using static LondoSpark.Func.Result<int, string>;

namespace LondoSpark.Func.Tests;

/// <summary>An <c>async Option&lt;T&gt;</c> or async result type returns the value the method returns, or the first absent or failed value it awaits.</summary>
[AsyncResult<string>]
public readonly partial struct Parsed<T>;

/// <summary>Async methods over options and results: <c>await</c> unwraps a value or short-circuits the method.</summary>
public sealed class ShortCircuitTests
{
    // ----- async Option<T> -----

    [Test]
    public async Task AnAsyncOptionReturnsItsValueWhenEveryAwaitIsPresent()
    {
        await Assert.That(Sum(some(1), some(2))).IsEqualTo(some(3));
    }

    [Test]
    public async Task AnAsyncOptionStopsAtTheFirstAbsentAwait()
    {
        var log = new List<string>();

        await Assert.That(Logged(log, none, some(2))).IsEqualTo(Option.Empty<int>());
        await Assert.That(log).IsEquivalentTo(["start"]);
    }

    [Test]
    public async Task AwaitingNoneEndsTheMethod()
    {
        await Assert.That(Positive(5)).IsEqualTo(some(5));
        await Assert.That(Positive(-5)).IsEqualTo(Option.Empty<int>());

        static async Option<int> Positive(int value)
        {
            if (value <= 0)
                await none;

            return value;
        }
    }

    [Test]
    public async Task FinallyBlocksAndUsingDeclarationsRunWhenAMethodShortCircuits()
    {
        var log = new List<string>();

        _ = Guarded(log, none);

        await Assert.That(log).IsEquivalentTo(["try", "finally", "disposed"]);

        static async Option<int> Guarded(List<string> log, Option<int> input)
        {
            using var resource = new Resource(log);
            try
            {
                log.Add("try");
                return await input;
            }
            finally
            {
                log.Add("finally");
            }
        }
    }

    [Test]
    public async Task OtherExceptionsStillPropagate()
    {
        var exception = Assert.Throws<FormatException>(static () => _ = Throws());

        await Assert.That(exception.Message).IsEqualTo("bad");

        static async Option<int> Throws()
        {
            var value = await some(1);
            throw new FormatException("bad");
        }
    }

    [Test]
    public async Task AnAsyncOptionCannotSuspend()
    {
        var exception = Assert.Throws<NotSupportedException>(static () => _ = Suspends());

        await Assert.That(exception.Message).Contains("ShortCircuitMethodBuilder");

#pragma warning disable LSF0005 // The runtime check is the subject of this test.
        static async Option<int> Suspends()
        {
            await Task.Yield();
            return 1;
        }
#pragma warning restore LSF0005
    }

    // ----- Generated result types -----

    [Test]
    public async Task AnAsyncResultReturnsTheFirstFailureItAwaits()
    {
        await Assert.That(Add("1", "2").AsResult()).IsEqualTo(Result.Return<int, string>(3));
        await Assert.That(Add("x", "2").AsResult()).IsEqualTo(Result.Failure<int, string>("'x' is not a number"));
        await Assert.That(Add("1", "y").AsResult()).IsEqualTo(Result.Failure<int, string>("'y' is not a number"));
    }

    [Test]
    public async Task AwaitingAPreludeFailureFailsTheMethod()
    {
        await Assert.That(Halve(4).AsResult()).IsEqualTo(Result.Return<int, string>(2));
        await Assert.That(Halve(3).AsResult()).IsEqualTo(Result.Failure<int, string>("3 is odd"));

        static async Parsed<int> Halve(int value)
        {
            if (value % 2 != 0)
                await failure($"{value} is odd");

            return value / 2;
        }
    }

    [Test]
    public async Task GeneratedResultTypesAreUnionsOverTheResultCases()
    {
        Parsed<int> parsed = success(4);
        Parsed<int> failed = failure("no");
        Result<int, string> unwrapped = parsed;

        var described = failed switch
        {
            Success(var value) => $"got {value}",
            Failure(var error) => $"failed: {error}"
        };

        await Assert.That(described).IsEqualTo("failed: no");
        await Assert.That(unwrapped).IsEqualTo(Result.Return<int, string>(4));
        await Assert.That(parsed.ToString()).IsEqualTo("Success(4)");
        await Assert.That(parsed == Add("2", "2")).IsTrue();
    }

    [Test]
    public async Task GeneratedResultTypesCanBeAwaitedInsideEachOther()
    {
        await Assert.That(Double("21").AsResult()).IsEqualTo(Result.Return<int, string>(42));
        await Assert.That(Double("z").AsResult()).IsEqualTo(Result.Failure<int, string>("'z' is not a number"));

        static async Parsed<int> Double(string text) => await Add(text, text);
    }

    // ----- ShortCircuitMethodBuilder -----

    [Test]
    public async Task ValueTaskMethodsAwaitTasksAndShortCircuitOnOptions()
    {
        await Assert.That(await LookUpAsync(3)).IsEqualTo(some("user-3"));
        await Assert.That(await LookUpAsync(0)).IsEqualTo(Option.Empty<string>());
    }

    [Test]
    public async Task ValueTaskMethodsShortCircuitOnFailuresAfterRealSuspension()
    {
        await Assert.That(await ChargeAsync(40)).IsEqualTo(Result.Return<int, string>(60));
        await Assert.That(await ChargeAsync(500)).IsEqualTo(Result.Failure<int, string>("500 exceeds the balance"));
    }

    [Test]
    public async Task ValueTaskMethodsCanReturnGeneratedResultTypes()
    {
        await Assert.That((await ParseLaterAsync("8")).AsResult()).IsEqualTo(Result.Return<int, string>(8));
        await Assert.That((await ParseLaterAsync("q")).AsResult()).IsEqualTo(Result.Failure<int, string>("'q' is not a number"));
    }

    [Test]
    public async Task ValueTaskMethodsFaultForOtherExceptions()
    {
        await Assert.ThrowsAsync<FormatException>(async () => await ThrowsAsync());

        [AsyncMethodBuilder(typeof(ShortCircuitMethodBuilder<>))]
        static async ValueTask<Option<int>> ThrowsAsync()
        {
            await Task.Yield();
            throw new FormatException("bad");
        }
    }

    // ----- Helpers -----

    private static async Option<int> Sum(Option<int> first, Option<int> second) =>
        await first + await second;

    private static async Option<int> Logged(List<string> log, Option<int> first, Option<int> second)
    {
        log.Add("start");
        var a = await first;
        log.Add("after first");
        var b = await second;
        return a + b;
    }

    private static Result<int, string> Parse(string text) =>
        int.TryParse(text, CultureInfo.InvariantCulture, out var number) ? success(number) : failure($"'{text}' is not a number");

    private static async Parsed<int> Add(string first, string second) =>
        await Parse(first) + await Parse(second);

    [AsyncMethodBuilder(typeof(ShortCircuitMethodBuilder<>))]
    private static async ValueTask<Option<string>> LookUpAsync(int id)
    {
        await Task.Yield();
        var found = await FindAsync(id);
        return some($"user-{await found}");
    }

    [AsyncMethodBuilder(typeof(ShortCircuitMethodBuilder<>))]
    private static async ValueTask<Result<int, string>> ChargeAsync(int amount)
    {
        var balance = await await DebitAsync(amount);
        return success(balance);
    }

    [AsyncMethodBuilder(typeof(ShortCircuitMethodBuilder<>))]
    private static async ValueTask<Parsed<int>> ParseLaterAsync(string text)
    {
        await Task.Yield();
        return success(await Parse(text));
    }

    private static async Task<Option<int>> FindAsync(int id)
    {
        await Task.Yield();
        return id > 0 ? some(id) : none;
    }

    private static async Task<Result<int, string>> DebitAsync(int amount)
    {
        await Task.Yield();
        return amount <= 100 ? success(100 - amount) : failure($"{amount} exceeds the balance");
    }

    private sealed class Resource(List<string> log) : IDisposable
    {
        public void Dispose() => log.Add("disposed");
    }
}
