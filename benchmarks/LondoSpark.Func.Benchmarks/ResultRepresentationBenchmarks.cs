using System;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;

namespace LondoSpark.Func.Benchmarks;

[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public class ResultRepresentationBenchmarks
{
    private static readonly Result<int, string> InlineSuccess = Result.Return<int, string>(41);
    private static readonly Result<Func<int, int>, string> InlineIncrement =
        Result.Return<Func<int, int>, string>(static value => value + 1);
    private static readonly Func<int, Result<int, string>> InlineBindIncrement =
        static value => Result.Return<int, string>(value + 1);
    private static readonly Func<int, Result<int, string>> InlineReject =
        static _ => Result.Failure<int, string>("invalid");

    private static readonly LegacyBoxedUnionResult<int, string> BoxedSuccess =
        new(new Success<int>(41));
    private static readonly LegacyBoxedUnionResult<Func<int, int>, string> BoxedIncrement =
        new(new Success<Func<int, int>>(static value => value + 1));
    private static readonly Func<int, LegacyBoxedUnionResult<int, string>> BoxedBindIncrement =
        static value => new Success<int>(value + 1);
    private static readonly Func<int, LegacyBoxedUnionResult<int, string>> BoxedReject =
        static _ => new Failure<string>("invalid");

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Direct")]
    public LegacyBoxedUnionResult<int, string> BoxedUnionDirectCase() =>
        BoxedSuccess switch
        {
            Success<int> value => new Success<int>(value.Value + 1),
            Failure<string> error => error
        };

    [Benchmark]
    [BenchmarkCategory("Direct")]
    public Result<int, string> InlineUnionDirectCase() =>
        InlineSuccess switch
        {
            Result<int, string>.Success value => new Result<int, string>.Success(value.Value + 1),
            Result<int, string>.Failure error => error
        };

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Bind")]
    public LegacyBoxedUnionResult<int, string> BoxedUnionBind() =>
        Bind(BoxedSuccess, BoxedBindIncrement);

    [Benchmark]
    [BenchmarkCategory("Bind")]
    public Result<int, string> InlineUnionBind() =>
        InlineSuccess.Bind(InlineBindIncrement);

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("BindFailure")]
    public LegacyBoxedUnionResult<int, string> BoxedUnionBindFailure() =>
        Bind(BoxedSuccess, BoxedReject);

    [Benchmark]
    [BenchmarkCategory("BindFailure")]
    public Result<int, string> InlineUnionBindFailure() =>
        InlineSuccess.Bind(InlineReject);

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Apply")]
    public LegacyBoxedUnionResult<int, string> BoxedUnionApply() =>
        Apply(BoxedIncrement, BoxedSuccess);

    [Benchmark]
    [BenchmarkCategory("Apply")]
    public Result<int, string> InlineUnionApply() =>
        InlineIncrement.Apply(InlineSuccess);

    private static LegacyBoxedUnionResult<TOut, TFailure> Bind<TIn, TOut, TFailure>(
        LegacyBoxedUnionResult<TIn, TFailure> result,
        Func<TIn, LegacyBoxedUnionResult<TOut, TFailure>> binder) =>
        result switch
        {
            Success<TIn> success => binder(success.Value),
            Failure<TFailure> failure => new Failure<TFailure>(failure.Value)
        };

    private static LegacyBoxedUnionResult<TOut, TFailure> Apply<TIn, TOut, TFailure>(
        LegacyBoxedUnionResult<Func<TIn, TOut>, TFailure> function,
        LegacyBoxedUnionResult<TIn, TFailure> value) =>
        function switch
        {
            Success<Func<TIn, TOut>> someFunction => value switch
            {
                Success<TIn> someValue =>
                    new Success<TOut>(someFunction.Value(someValue.Value)),
                Failure<TFailure> failure => new Failure<TFailure>(failure.Value)
            },
            Failure<TFailure> failure => new Failure<TFailure>(failure.Value)
        };

    [GlobalSetup]
    public void VerifyUnionCases()
    {
        LegacyBoxedUnionResult<int, string> boxedFailed = new(new Failure<string>("invalid"));
        Result<int, string> inlineFailed = new Result<int, string>.Failure("invalid");
        LegacyBoxedUnionResult<string?, string?> boxedSuccessNull =
            new(new Success<string?>(null));
        Result<string?, string?> inlineSuccessNull = new Result<string?, string?>.Success(null);

        if (InlineSuccess is not Result<int, string>.Success { Value: 41 })
            throw new InvalidOperationException("The inline union failed to read its success case.");

        if (boxedFailed is not Failure<string> { Value: "invalid" }
            || inlineFailed is not Result<int, string>.Failure { Value: "invalid" })
        {
            throw new InvalidOperationException("A union failed to read its failure case.");
        }

        if (boxedSuccessNull is not Success<string?> { Value: null }
            || inlineSuccessNull is not Result<string?, string?>.Success { Value: null })
        {
            throw new InvalidOperationException("A union failed to preserve a null success.");
        }
    }
}

public readonly union LegacyBoxedUnionResult<TSuccess, TFailure>(Success<TSuccess>, Failure<TFailure>);
