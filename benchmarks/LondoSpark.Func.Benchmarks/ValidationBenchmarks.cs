using System;
using BenchmarkDotNet.Attributes;

namespace LondoSpark.Func.Benchmarks;

/// <summary>Measures validation construction and applicative accumulation, where errors are gathered.</summary>
[MemoryDiagnoser]
public class ValidationBenchmarks
{
    private static readonly Func<int, Func<int, Func<int, int>>> Sum3 =
        static first => second => third => first + second + third;

    private static readonly Validation<int, string> Valid = Validation.Return<int, string>(1);
    private static readonly Validation<int, string> Invalid = Validation.Failure<int, string>("invalid");

    [Benchmark(Baseline = true)]
    public Validation<int, string> ReturnAndSelect() =>
        Validation.Return<int, string>(41).Select(static value => value + 1);

    [Benchmark]
    public Validation<int, string> ApplyAllValid() =>
        Validation.Return<Func<int, Func<int, Func<int, int>>>, string>(Sum3)
            .Apply(Valid)
            .Apply(Valid)
            .Apply(Valid);

    [Benchmark]
    public Validation<int, string> ApplyAllInvalid() =>
        Validation.Return<Func<int, Func<int, Func<int, int>>>, string>(Sum3)
            .Apply(Invalid)
            .Apply(Invalid)
            .Apply(Invalid);
}

/// <summary>Measures error accumulation as the number of failing fields grows.</summary>
[MemoryDiagnoser]
public class NonEmptyListAppendBenchmarks
{
    private static readonly NonEmptyList<string> Single = new("error");

    [Params(4, 32, 256)]
    public int Errors { get; set; }

    [Benchmark]
    public NonEmptyList<string> AppendChain()
    {
        var errors = Single;
        for (var index = 1; index < Errors; index++)
        {
            errors = errors.Append(Single);
        }

        return errors;
    }

    [Benchmark]
    public int Enumerate()
    {
        var total = 0;
        foreach (var error in Single)
        {
            total += error.Length;
        }

        return total;
    }
}
