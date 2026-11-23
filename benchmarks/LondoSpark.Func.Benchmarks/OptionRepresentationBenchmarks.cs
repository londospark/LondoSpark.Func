using System;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using LondoSpark.Func;

[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public class OptionRepresentationBenchmarks
{
    private static readonly Option<int> InlineSome = Option.Return(41);
    private static readonly Option<Func<int, int>> InlineIncrement =
        Option.Return<Func<int, int>>(static value => value + 1);
    private static readonly Func<int, Option<int>> InlineBindIncrement =
        static value => Option.Return(value + 1);

    private static readonly LegacyBoxedUnionOption<int> BoxedSome = new(new Some<int>(41));
    private static readonly LegacyBoxedUnionOption<Func<int, int>> BoxedIncrement =
        new(new Some<Func<int, int>>(static value => value + 1));
    private static readonly Func<int, LegacyBoxedUnionOption<int>> BoxedBindIncrement =
        static value => new Some<int>(value + 1);

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Direct")]
    public LegacyBoxedUnionOption<int> BoxedUnionDirectCase() =>
        BoxedSome switch
        {
            Some<int> value => new Some<int>(value.Value + 1),
            None none => none
        };

    [Benchmark]
    [BenchmarkCategory("Direct")]
    public Option<int> InlineUnionDirectCase() =>
        InlineSome switch
        {
            Some<int> value => new Some<int>(value.Value + 1),
            None none => none
        };

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Bind")]
    public LegacyBoxedUnionOption<int> BoxedUnionBind() =>
        Bind(BoxedSome, BoxedBindIncrement);

    [Benchmark]
    [BenchmarkCategory("Bind")]
    public Option<int> InlineUnionBind() =>
        InlineSome.Bind(InlineBindIncrement);

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Apply")]
    public LegacyBoxedUnionOption<int> BoxedUnionApply() =>
        Apply(BoxedIncrement, BoxedSome);

    [Benchmark]
    [BenchmarkCategory("Apply")]
    public Option<int> InlineUnionApply() =>
        InlineIncrement.Apply(InlineSome);

    private static LegacyBoxedUnionOption<TOutput> Bind<TInput, TOutput>(
        LegacyBoxedUnionOption<TInput> option,
        Func<TInput, LegacyBoxedUnionOption<TOutput>> binder) =>
        option switch
        {
            Some<TInput> some => binder(some.Value),
            None none => none
        };

    private static LegacyBoxedUnionOption<TOutput> Apply<TInput, TOutput>(
        LegacyBoxedUnionOption<Func<TInput, TOutput>> function,
        LegacyBoxedUnionOption<TInput> value) =>
        function switch
        {
            Some<Func<TInput, TOutput>> someFunction => value switch
            {
                Some<TInput> someValue =>
                    new Some<TOutput>(someFunction.Value(someValue.Value)),
                None none => none
            },
            None none => none
        };

    [GlobalSetup]
    public void VerifyUnionCases()
    {
        LegacyBoxedUnionOption<int> boxedNone = new None();
        Option<int> inlineNone = new None();
        LegacyBoxedUnionOption<string?> boxedSomeNull = new Some<string?>((string?)null);
        Option<string?> inlineSomeNull = new Some<string?>((string?)null);

        if (InlineSome is not Some<int> { Value: 41 })
            throw new InvalidOperationException("The inline union failed to read its Some case.");

        if (boxedNone is not None || inlineNone is not None)
            throw new InvalidOperationException("A union failed to read its None case.");

        if (boxedSomeNull is not Some<string?> { Value: null }
            || inlineSomeNull is not Some<string?> { Value: null })
        {
            throw new InvalidOperationException("A union failed to preserve Some(null).");
        }
    }
}

public readonly union LegacyBoxedUnionOption<T>(Some<T>, None);
