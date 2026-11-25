using System;
using System.Buffers;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using System.Threading.Tasks;

using LondoSpark.Func.FSharp;
using LondoSpark.Func.Json;
using LondoSpark.Func.Tests.Scripting;
using Microsoft.FSharp.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

using static LondoSpark.Func.Prelude;
using static LondoSpark.Func.Tests.Scripting.Terminal;

namespace LondoSpark.Func.Tests;

/// <summary>Every operation that should not allocate is held to zero bytes, and every deliberate allocation to a tight bound.</summary>
/// <remarks>
/// Each operation is warmed up so JIT tiering and static initialisation are excluded, then measured over many calls on
/// the current thread. Inputs live in static fields and delegates are static lambdas, so the measurement sees only the
/// library. Value-type and reference-type payloads are both covered, because the runtime shares one compiled body for
/// every reference type and specialises each value type.
/// </remarks>
public sealed class AllocationTests
{
    private const int Iterations = 2_000;

    // A short-circuit allocates its exception and the stack trace recorded while it unwinds; a Debug build's class
    // state machine adds to that.
#if DEBUG
    private const long ShortCircuitBound = 448;
#else
    private const long ShortCircuitBound = 320;
#endif

    // ----- Inputs -----

    private static readonly Option<int> SomeInt = Option.Return(41);
    private static readonly Option<int> NoneInt = Option.Empty<int>();
    private static readonly Option<string> SomeText = Option.Return("text");
    private static readonly Option<string> NoneText = Option.Empty<string>();
    private static readonly Option<Func<int, int>> SomeIncrement = Option.Return<Func<int, int>>(static value => value + 1);
    private static readonly int? NullableInt = 3;
    private static readonly string? NullableText = "text";

    private static readonly Result<int, string> SuccessInt = Result.Return<int, string>(41);
    private static readonly Result<int, string> FailureInt = Result.Failure<int, string>("failed");
    private static readonly Result<string, int> SuccessText = Result.Return<string, int>("text");
    private static readonly Result<string, int> FailureText = Result.Failure<string, int>(7);
    private static readonly Result<Func<int, int>, string> SuccessIncrement =
        Result.Return<Func<int, int>, string>(static value => value + 1);
    private static readonly Result<int, Unit> SuccessUnit = Result.Return<int, Unit>(1);

    private static readonly ErrorResult<int, string> FocusedFailure = ErrorResult.Return<int, string>("failed");
    private static readonly ErrorResult<int, string> FocusedSuccess = SuccessInt.AsErrorResult();
    private static readonly ErrorResult<int, Func<string, int>> FocusedLength =
        ErrorResult.Return<int, Func<string, int>>(static error => error.Length);

    private static readonly Validation<int, string> ValidInt = Validation.Return<int, string>(41);
    private static readonly Validation<int, string> InvalidInt = Validation.Failure<int, string>("invalid");
    private static readonly Validation<Func<int, int>, string> ValidIncrement =
        Validation.Return<Func<int, int>, string>(static value => value + 1);
    private static readonly Validation<Func<int, int>, string> InvalidIncrement =
        Validation.Failure<Func<int, int>, string>("no function");

    private static readonly NonEmptyList<int> List = new(1, 2, 3);
    private static readonly NonEmptyList<string> TextList = new("a", "b");

    private static readonly Task<Option<int>> CompletedOption = Task.FromResult(SomeInt);
    private static readonly Task<Result<int, string>> CompletedResult = Task.FromResult(SuccessInt);

    private static readonly Ask AskName = new("name?");
    private static readonly Roll Die = new(6);
    private static readonly Answers Handler = new();

    // ----- Option -----

    [Test]
    public async Task OptionConstructionDoesNotAllocate()
    {
        await AssertNoAllocation(static () => Option.Return(1));
        await AssertNoAllocation(static () => Option.Return("text"));
        await AssertNoAllocation(static () => Option.Some(1));
        await AssertNoAllocation(static () => Option.Empty<int>());
        await AssertNoAllocation(static () => Option.None<string>());
        await AssertNoAllocation(static () => (Option<int>)new Option<int>.Some(1));
        await AssertNoAllocation(static () => (Option<string>)new None());
        await AssertNoAllocation(static () => NullableInt.ToOption());
        await AssertNoAllocation(static () => NullableText.ToOption());
        await AssertNoAllocation(static () => ((string?)null).ToOption());
    }

    [Test]
    public async Task OptionCompositionDoesNotAllocate()
    {
        await AssertNoAllocation(static () => SomeInt.Select(static value => value + 1));
        await AssertNoAllocation(static () => NoneInt.Select(static value => value + 1));
        await AssertNoAllocation(static () => SomeText.Select(static value => value.Length));
        await AssertNoAllocation(static () => SomeInt.Bind(static value => Option.Return(value + 1)));
        await AssertNoAllocation(static () => NoneInt.Bind(static value => Option.Return(value + 1)));
        await AssertNoAllocation(static () => SomeText.Bind(static value => value.Length > 0 ? some(value) : none));
        await AssertNoAllocation(static () => SomeInt.SelectMany(static value => Option.Return(value)));
        await AssertNoAllocation(static () => SomeInt.SelectMany(
            static value => Option.Return(value), static (first, second) => first + second));
        await AssertNoAllocation(static () => SomeIncrement.Apply(SomeInt));
        await AssertNoAllocation(static () => SomeIncrement.Apply(NoneInt));
        await AssertNoAllocation(static () => SomeInt.ToResult());
        await AssertNoAllocation(static () => NoneText.ToResult());
    }

    [Test]
    public async Task OptionInspectionDoesNotAllocate()
    {
        await AssertNoAllocation(static () => SomeInt == NoneInt);
        await AssertNoAllocation(static () => SomeText != NoneText);
        await AssertNoAllocation(static () => SomeInt.Equals(SomeInt));
        await AssertNoAllocation(static () => SomeText.Equals(SomeText));
        await AssertNoAllocation(static () => SomeInt.GetHashCode());
        await AssertNoAllocation(static () => SomeText.GetHashCode());
        await AssertNoAllocation(static () => NoneInt.Value);
        await AssertNoAllocation(static () => NoneText.Value);
        await AssertNoAllocation(static () => SomeInt.TryGetValue(out Option<int>.Some _));
        await AssertNoAllocation(static () => NoneInt.TryGetValue(out None _));
        await AssertNoAllocation(static () => SomeText is None);
        await AssertNoAllocation(static () => SomeInt switch
        {
            Option<int>.Some(var value) => value,
            None => 0
        });
        await AssertNoAllocation(static () => SomeText switch
        {
            Option<string>.Some(var value) => value.Length,
            None => 0
        });
    }

    // ----- Result -----

    [Test]
    public async Task ResultConstructionDoesNotAllocate()
    {
        await AssertNoAllocation(static () => Result.Return<int, string>(1));
        await AssertNoAllocation(static () => Result.Failure<int, string>("failed"));
        await AssertNoAllocation(static () => Result.Return<string, int>("text"));
        await AssertNoAllocation(static () => (Result<int, string>)new Result<int, string>.Success(1));
        await AssertNoAllocation(static () => (Result<int, string>)new Result<int, string>.Failure("failed"));
        await AssertNoAllocation(static () => (Result<int, string>)success(1));
        await AssertNoAllocation(static () => (Result<int, string>)failure("failed"));
        await AssertNoAllocation(static () => (Result<string, string>)success("text"));
    }

    [Test]
    public async Task ResultCompositionDoesNotAllocate()
    {
        await AssertNoAllocation(static () => SuccessInt.Select(static value => value + 1));
        await AssertNoAllocation(static () => FailureInt.Select(static value => value + 1));
        await AssertNoAllocation(static () => SuccessText.Select(static value => value.Length));
        await AssertNoAllocation(static () => SuccessInt.Bind(static value => Result.Return<int, string>(value + 1)));
        await AssertNoAllocation(static () => FailureInt.Bind(static value => Result.Return<int, string>(value + 1)));
        await AssertNoAllocation(static () => SuccessText.Bind(Result<int, int> (value) => success(value.Length)));
        await AssertNoAllocation(static () => SuccessInt.SelectMany(static value => Result.Return<int, string>(value)));
        await AssertNoAllocation(static () => SuccessInt.SelectMany(
            static value => Result.Return<int, string>(value), static (first, second) => first + second));
        await AssertNoAllocation(static () => FailureInt.MapError(static error => error.Length));
        await AssertNoAllocation(static () => SuccessInt.MapError(static error => error.Length));
        await AssertNoAllocation(static () => FailureText.BindError(static error => Result.Failure<string, string>("x")));
        await AssertNoAllocation(static () => SuccessIncrement.Apply(SuccessInt));
        await AssertNoAllocation(static () => SuccessIncrement.Apply(FailureInt));
        await AssertNoAllocation(static () => SuccessInt.ToExitCode());
        await AssertNoAllocation(static () => FailureText.ToExitCode());
        await AssertNoAllocation(static () => SuccessUnit.ToOption());
    }

    [Test]
    public async Task ResultInspectionDoesNotAllocate()
    {
        await AssertNoAllocation(static () => SuccessInt == FailureInt);
        await AssertNoAllocation(static () => SuccessText != FailureText);
        await AssertNoAllocation(static () => SuccessInt.Equals(SuccessInt));
        await AssertNoAllocation(static () => SuccessInt.GetHashCode());
        await AssertNoAllocation(static () => FailureText.GetHashCode());
        await AssertNoAllocation(static () => SuccessInt.HasValue);
        await AssertNoAllocation(static () => SuccessInt.TryGetValue(out Result<int, string>.Success _));
        await AssertNoAllocation(static () => FailureInt.TryGetValue(out Result<int, string>.Failure _));
        await AssertNoAllocation(static () => SuccessInt switch
        {
            Result<int, string>.Success(var value) => value,
            Result<int, string>.Failure(var error) => error.Length
        });
        await AssertNoAllocation(static () => FailureText switch
        {
            Result<string, int>.Success(var value) => value.Length,
            Result<string, int>.Failure(var error) => error
        });
    }

    // ----- ErrorResult -----

    [Test]
    public async Task ErrorResultCompositionDoesNotAllocate()
    {
        await AssertNoAllocation(static () => ErrorResult.Return<int, string>("failed"));
        await AssertNoAllocation(static () => SuccessInt.AsErrorResult());
        await AssertNoAllocation(static () => FocusedFailure.ToResult());
        await AssertNoAllocation(static () => FocusedFailure.Select(static error => error.Length));
        await AssertNoAllocation(static () => FocusedSuccess.Select(static error => error.Length));
        await AssertNoAllocation(static () => FocusedFailure.BindError(static error => ErrorResult.Return<int, int>(error.Length)));
        await AssertNoAllocation(static () => FocusedFailure.SelectMany(static error => ErrorResult.Return<int, int>(error.Length)));
        await AssertNoAllocation(static () => FocusedFailure.SelectMany(
            static error => ErrorResult.Return<int, int>(error.Length), static (error, length) => length));
        await AssertNoAllocation(static () => FocusedLength.Apply(FocusedFailure));
        await AssertNoAllocation(static () => FocusedFailure == FocusedSuccess);
        await AssertNoAllocation(static () => FocusedFailure.GetHashCode());
    }

    // ----- Validation -----

    [Test]
    public async Task ValidationWithoutNewErrorsDoesNotAllocate()
    {
        await AssertNoAllocation(static () => Validation.Return<int, string>(41));
        await AssertNoAllocation(static () => (Validation<int, string>)success(41));
        await AssertNoAllocation(static () => ValidInt.Select(static value => value + 1));
        await AssertNoAllocation(static () => InvalidInt.Select(static value => value + 1));
        await AssertNoAllocation(static () => ValidIncrement.Apply(ValidInt));
        await AssertNoAllocation(static () => ValidIncrement.Apply(InvalidInt));
        await AssertNoAllocation(static () => InvalidIncrement.Apply(ValidInt));
        await AssertNoAllocation(static () => ValidInt.ToResult());
        await AssertNoAllocation(static () => InvalidInt.ToResult());
        await AssertNoAllocation(static () => ValidInt == InvalidInt);
        await AssertNoAllocation(static () => InvalidInt.GetHashCode());
        await AssertNoAllocation(static () => InvalidInt switch
        {
            Validation<int, string>.Success(var value) => value,
            Validation<int, string>.Failure(var errors) => errors.Count
        });
    }

    [Test]
    public async Task NonEmptyListReadsDoNotAllocate()
    {
        await AssertNoAllocation(static () => List.Count);
        await AssertNoAllocation(static () => List[2]);
        await AssertNoAllocation(static () => List.AsSpan().Length);
        await AssertNoAllocation(static () =>
        {
            var total = 0;
            foreach (var item in List)
            {
                total += item;
            }

            return total;
        });
        await AssertNoAllocation(static () =>
        {
            var length = 0;
            foreach (var item in TextList)
            {
                length += item.Length;
            }

            return length;
        });
    }

    // ----- Prelude, Unit, None -----

    [Test]
    public async Task PreludeAndMarkerValuesDoNotAllocate()
    {
        await AssertNoAllocation(static () => some(1));
        await AssertNoAllocation(static () => some("text"));
        await AssertNoAllocation(static () => (Option<int>)none);
        await AssertNoAllocation(static () => (Option<string>)none);
        await AssertNoAllocation(static () => success(1));
        await AssertNoAllocation(static () => failure("failed"));
        await AssertNoAllocation(static () => Unit.Value);
        await AssertNoAllocation(static () => none == default(None));
    }

    // ----- F# interop -----

    [Test]
    public async Task FSharpValueConversionsDoNotAllocate()
    {
        await AssertNoAllocation(static () => SomeInt.ToFSharpValueOption());
        await AssertNoAllocation(static () => SomeText.ToFSharpValueOption());
        await AssertNoAllocation(static () => FSharpValueOption<int>.NewValueSome(1).ToOption());
        await AssertNoAllocation(static () => FSharpValueOption<string>.ValueNone.ToOption());
        await AssertNoAllocation(static () => SuccessInt.ToFSharpResult());
        await AssertNoAllocation(static () => FSharpResult<int, string>.NewOk(1).ToResult());
        await AssertNoAllocation(static () => NoneInt.ToFSharpOption());
        await AssertNoAllocation(static () => FSharpOptionExtensions.ToOption((FSharpOption<int>?)null));
    }

    // ----- JSON -----

    [Test]
    public async Task JsonConvertersAllocateNoMoreThanThePayload()
    {
        // GetTypeInfo needs a resolver up front; serializer calls would install the default one themselves.
        var options = new JsonSerializerOptions { TypeInfoResolver = new DefaultJsonTypeInfoResolver() }
            .AddOptionConverter<int>()
            .AddResultConverter<int, string>();
        var intInfo = options.GetTypeInfo<int>();
        var optionInfo = options.GetTypeInfo<Option<int>>();
        var resultInfo = options.GetTypeInfo<Result<int, string>>();
        var buffer = new ArrayBufferWriter<byte>(256);
        var writer = new Utf8JsonWriter(buffer);
        byte[] intJson = [.. "42"u8];
        byte[] optionJson = [.. """{"some":42}"""u8];
        byte[] resultJson = [.. """{"success":42}"""u8];

        var payloadWrite = Measure(() => Write(writer, buffer, 42, intInfo));
        var optionWrite = Measure(() => Write(writer, buffer, SomeInt, optionInfo));
        var noneWrite = Measure(() => Write(writer, buffer, NoneInt, optionInfo));
        var resultWrite = Measure(() => Write(writer, buffer, SuccessInt, resultInfo));
        var payloadRead = Measure(() => JsonSerializer.Deserialize(intJson, intInfo));
        var optionRead = Measure(() => JsonSerializer.Deserialize(optionJson, optionInfo));
        var resultRead = Measure(() => JsonSerializer.Deserialize(resultJson, resultInfo));

        await Assert.That(optionWrite).IsLessThanOrEqualTo(payloadWrite);
        await Assert.That(noneWrite).IsLessThanOrEqualTo(payloadWrite);
        await Assert.That(resultWrite).IsLessThanOrEqualTo(payloadWrite);
        await Assert.That(optionRead).IsLessThanOrEqualTo(payloadRead);
        await Assert.That(resultRead).IsLessThanOrEqualTo(payloadRead);

        static int Write<T>(Utf8JsonWriter writer, ArrayBufferWriter<byte> buffer, T value, JsonTypeInfo<T> info)
        {
            buffer.ResetWrittenCount();
            writer.Reset(buffer);
            JsonSerializer.Serialize(writer, value, info);
            writer.Flush();
            return buffer.WrittenCount;
        }
    }

    // ----- Asynchronous queries -----

    // A Debug build compiles async methods to class state machines, which allocate on every call, so this holds only
    // for the optimised builds that consumers run and CI tests.
#if !DEBUG
    [Test]
    public async Task AsyncQueriesThatCompleteSynchronouslyDoNotAllocate()
    {
        await AssertNoAllocation(static () => Completed(new ValueTask<Option<int>>(SomeInt).Select(static value => value + 1)));
        await AssertNoAllocation(static () => Completed(new ValueTask<Option<int>>(SomeInt)
            .SelectMany(static value => new ValueTask<Option<int>>(Option.Return(value)), static (a, b) => a + b)));
        await AssertNoAllocation(static () => Completed(new ValueTask<Option<int>>(NoneInt)
            .SelectMany(static value => new ValueTask<Option<int>>(Option.Return(value)), static (a, b) => a + b)));
        await AssertNoAllocation(static () => Completed(CompletedOption
            .SelectMany(static value => Option.Return(value), static (a, b) => a + b)));
        await AssertNoAllocation(static () => Completed(SomeInt
            .SelectMany(static value => CompletedOption, static (a, b) => a + b)));

        await AssertNoAllocation(static () => Completed(new ValueTask<Result<int, string>>(SuccessInt).Select(static value => value + 1)));
        await AssertNoAllocation(static () => Completed(new ValueTask<Result<int, string>>(SuccessInt)
            .SelectMany(static value => new ValueTask<Result<int, string>>(Result.Return<int, string>(value)), static (a, b) => a + b)));
        await AssertNoAllocation(static () => Completed(new ValueTask<Result<int, string>>(FailureInt)
            .SelectMany(static value => CompletedResult, static (a, b) => a + b)));
        await AssertNoAllocation(static () => Completed(CompletedResult
            .SelectMany(static value => Result.Return<int, string>(value), static (a, b) => a + b)));
        await AssertNoAllocation(static () => Completed(SuccessInt
            .SelectMany(static value => CompletedResult, static (a, b) => a + b)));
    }

    [Test]
    public async Task AsyncMethodsThatDoNotShortCircuitDoNotAllocate()
    {
        await AssertNoAllocation(static () => SumOption(SomeInt, SomeInt));
        await AssertNoAllocation(static () => SumParsed(SuccessInt, SuccessInt));
        await AssertNoAllocation(static () => Completed(SumOptionLater(SomeInt)));
        await AssertNoAllocation(static () => Completed(SumResultLater(SuccessInt)));
    }

#endif

    [Test]
    public async Task AShortCircuitAllocatesOnlyItsException()
    {
        // The exception and the stack trace the runtime records while it unwinds the method.
        var absent = Measure(static () => SumOption(SomeInt, NoneInt));
        var failed = Measure(static () => SumParsed(SuccessInt, FailureInt));

        await Assert.That(absent).IsGreaterThan(0L).And.IsLessThanOrEqualTo(ShortCircuitBound);
        await Assert.That(failed).IsGreaterThan(0L).And.IsLessThanOrEqualTo(ShortCircuitBound);
    }

    // ----- Programs -----

    [Test]
    public async Task FinishedProgramsAndLiftedInstructionsDoNotAllocate()
    {
        await AssertNoAllocation(static () => pure(1));
        await AssertNoAllocation(static () => pure("text"));
        await AssertNoAllocation(static () => pure(1).Select(static value => value + 1));
        await AssertNoAllocation(static () => pure(1).Bind(static value => pure(value + 1)));
        await AssertNoAllocation(static () => pure(1).SelectMany(static value => pure(value), static (a, b) => a + b));
        await AssertNoAllocation(static () => ask(AskName));
        await AssertNoAllocation(static () => roll(Die));
        await AssertNoAllocation(static () => pure(1).Run(Handler));
        await AssertNoAllocation(static () => ask(AskName).Run(Handler));
    }

    [Test]
    public async Task ProgramStepsAllocateTheirContinuationAndBoxedValues()
    {
        // A value-typed answer is boxed once on its way through the continuation.
        var valueAnswer = Measure(static () => roll(Die).Run(Handler));
        // Composing a pending program allocates its new continuation and step.
        var composed = Measure(static () => roll(Die).Select(static value => value + 1));
        // Two instructions, a projection, and their boxed values.
        var query = Measure(static () => (from first in roll(Die)
                                          from second in roll(Die)
                                          select first + second).Run(Handler));

        await Assert.That(valueAnswer).IsGreaterThan(0L).And.IsLessThanOrEqualTo(24L);
        await Assert.That(composed).IsGreaterThan(0L).And.IsLessThanOrEqualTo(80L);
        await Assert.That(query).IsGreaterThan(0L).And.IsLessThanOrEqualTo(272L);
    }

    // ----- Deliberate allocations, held to a bound -----

    [Test]
    public async Task BoxingTheValueProtocolAllocatesOneSmallBox()
    {
        var someBox = Measure(static () => SomeInt.Value);
        var resultBox = Measure(static () => SuccessInt.Value);

        await Assert.That(someBox).IsGreaterThan(0L).And.IsLessThanOrEqualTo(32L);
        await Assert.That(resultBox).IsGreaterThan(0L).And.IsLessThanOrEqualTo(32L);
    }

    [Test]
    public async Task NewErrorsAllocateOnlyTheirList()
    {
        var oneError = Measure(static () => Validation.Failure<int, string>("invalid"));
        var preludeError = Measure(static () => (Validation<int, string>)failure("invalid"));
        var combined = Measure(static () => InvalidIncrement.Apply(InvalidInt));

        // A list object and its backing array; combining two adds one grown array and the new list.
        await Assert.That(oneError).IsGreaterThan(0L).And.IsLessThanOrEqualTo(96L);
        await Assert.That(preludeError).IsEqualTo(oneError);
        await Assert.That(combined).IsGreaterThan(0L).And.IsLessThanOrEqualTo(160L);
    }

    [Test]
    public async Task AppendingInPlaceAllocatesOnlyTheNewListNotACopy()
    {
        // Grow a buffer with spare capacity, then measure appends that fit in it: each must cost one small list
        // object however long the list is, which proves earlier errors are not copied.
        var single = new NonEmptyList<int>(0);
        var large = single;
        for (var index = 1; index < 1_000; index++)
        {
            large = large.Append(single);
        }

        var spare = large.Append(single);
        var inPlace = MeasureSingle(() => _ = spare.Append(single));

        // large's next slot now belongs to spare, so a second append to large must copy rather than overwrite it.
        var copied = MeasureSingle(() => _ = large.Append(single));

        await Assert.That(inPlace).IsGreaterThan(0L).And.IsLessThanOrEqualTo(64L);
        await Assert.That(copied).IsGreaterThan(1_000L * sizeof(int));
    }

    [Test]
    public async Task InterfaceEnumerationAllocatesOneEnumerator()
    {
        var boxed = Measure(static () =>
        {
            var total = 0;
            foreach (var item in (IEnumerable<int>)List)
            {
                total += item;
            }

            return total;
        });

        await Assert.That(boxed).IsGreaterThan(0L).And.IsLessThanOrEqualTo(48L);
    }

    [Test]
    public async Task FSharpReferenceOptionAllocatesOnlyForSome()
    {
        var present = Measure(static () => SomeInt.ToFSharpOption());

        await Assert.That(present).IsGreaterThan(0L).And.IsLessThanOrEqualTo(32L);
    }

    // ----- Measurement -----

    private static async Task AssertNoAllocation<T>(Func<T> operation) =>
        await Assert.That(Measure(operation)).IsEqualTo(0L);

    /// <summary>Returns the bytes allocated per call, after warming up.</summary>
    private static long Measure<T>(Func<T> operation)
    {
        for (var warmup = 0; warmup < 200; warmup++)
        {
            _ = operation();
        }

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var iteration = 0; iteration < Iterations; iteration++)
        {
            _ = operation();
        }

        return (GC.GetAllocatedBytesForCurrentThread() - before) / Iterations;
    }

    /// <summary>Returns the bytes one call allocates, for operations that change state and cannot be repeated.</summary>
    private static long MeasureSingle(Action operation)
    {
        var before = GC.GetAllocatedBytesForCurrentThread();
        operation();
        return GC.GetAllocatedBytesForCurrentThread() - before;
    }

    private static async Option<int> SumOption(Option<int> first, Option<int> second) =>
        await first + await second;

    private static async Parsed<int> SumParsed(Result<int, string> first, Result<int, string> second) =>
        await first + await second;

    [System.Runtime.CompilerServices.AsyncMethodBuilder(typeof(ShortCircuitMethodBuilder<>))]
    private static async ValueTask<Option<int>> SumOptionLater(Option<int> value) =>
        some(await value + await await CompletedOption);

    [System.Runtime.CompilerServices.AsyncMethodBuilder(typeof(ShortCircuitMethodBuilder<>))]
    private static async ValueTask<Result<int, string>> SumResultLater(Result<int, string> value) =>
        success(await value + await await CompletedResult);

    // Reads a query that must have completed synchronously; a suspension would be a test failure, not a wait.
    private static T Completed<T>(ValueTask<T> task) =>
        task.IsCompletedSuccessfully ? task.Result : throw new InvalidOperationException("The query suspended.");

    private sealed class Answers : Terminal.IHandler
    {
        public string Handle(Ask instruction) => "Ada";

        public Unit Handle(Tell instruction) => Unit.Value;

        public int Handle(Roll instruction) => instruction.Sides;
    }
}
