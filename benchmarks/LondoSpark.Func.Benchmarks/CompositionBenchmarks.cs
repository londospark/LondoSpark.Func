using System;
using BenchmarkDotNet.Attributes;
using LondoSpark.Func;

[MemoryDiagnoser]
public class OptionCompositionBenchmarks
{
    private static readonly Option<int> Some = Option.Return(41);
    private static readonly Option<Func<int, int>> OptionalIncrement =
        Option.Return<Func<int, int>>(static value => value + 1);
    private static readonly Func<int, Option<int>> BindOptionalIncrement =
        static value => Option.Return(value + 1);

    [Benchmark(Baseline = true)]
    public Option<int> OptionDirectCase() =>
        Some switch
        {
            int value => Option.Return(value + 1),
            None none => none
        };

    [Benchmark]
    public Option<int> OptionBind() =>
        Some.Bind(BindOptionalIncrement);

    [Benchmark]
    public Option<int> OptionApply() =>
        OptionalIncrement.Apply(Some);
}

[MemoryDiagnoser]
public class ResultCompositionBenchmarks
{
    private static readonly Result<int, string> Successful = Result.Return<int, string>(41);
    private static readonly Result<Func<int, int>, string> Increment =
        Result.Return<Func<int, int>, string>(static value => value + 1);
    private static readonly Func<int, Result<int, string>> BindIncrement =
        static value => Result.Return<int, string>(value + 1);

    [Benchmark(Baseline = true)]
    public Result<int, string> ResultDirectCase() =>
        Successful switch
        {
            int value => Result.Return<int, string>(value + 1),
            string error => error
        };

    [Benchmark]
    public Result<int, string> ResultBind() =>
        Successful.Bind(BindIncrement);

    [Benchmark]
    public Result<int, string> ResultApply() =>
        Increment.Apply(Successful);
}

[MemoryDiagnoser]
public class OptionQueryCompositionBenchmarks
{
    private static readonly Option<int> Source = Option.Return(41);
    private static readonly Func<int, Option<int>> Second =
        static first => Option.Return(first + 1);
    private static readonly Func<int, int, int> Project =
        static (first, second) => first + second;

    [Benchmark(Baseline = true)]
    public Option<int> DirectCase() =>
        Source switch
        {
            int first => Second(first) switch
            {
                int second => Option.Return(Project(first, second)),
                None none => none
            },
            None none => none
        };

    [Benchmark]
    public Option<int> SelectMany() =>
        Source.SelectMany(Second, Project);
}

[MemoryDiagnoser]
public class ResultQueryCompositionBenchmarks
{
    private static readonly Result<int, string> Source =
        Result.Return<int, string>(41);
    private static readonly Func<int, Result<int, string>> Second =
        static first => Result.Return<int, string>(first + 1);
    private static readonly Func<int, int, int> Project =
        static (first, second) => first + second;

    [Benchmark(Baseline = true)]
    public Result<int, string> DirectCase() =>
        Source switch
        {
            int first => Second(first) switch
            {
                int second => Result.Return<int, string>(Project(first, second)),
                string error => error
            },
            string error => error
        };

    [Benchmark]
    public Result<int, string> SelectMany() =>
        Source.SelectMany(Second, Project);
}

[MemoryDiagnoser]
public class ErrorResultCompositionBenchmarks
{
    private static readonly ErrorResult<int, string> Failed =
        ErrorResult.Return<int, string>("invalid");
    private static readonly ErrorResult<int, Func<string, string>> ErrorFunction =
        ErrorResult.Return<int, Func<string, string>>(static error => $"{error}!");
    private static readonly Func<string, ErrorResult<int, string>> BindError =
        static error => ErrorResult.Return<int, string>($"{error}!");

    [Benchmark(Baseline = true)]
    public ErrorResult<int, string> ErrorResultDirectCase() =>
        Failed.Value switch
        {
            int success => new ErrorResult<int, string>(success),
            string error => ErrorResult.Return<int, string>($"{error}!")
        };

    [Benchmark]
    public ErrorResult<int, string> ErrorResultBind() =>
        Failed.BindError(BindError);

    [Benchmark]
    public ErrorResult<int, string> ErrorResultApply() =>
        ErrorFunction.Apply(Failed);
}

[MemoryDiagnoser]
public class ErrorResultQueryCompositionBenchmarks
{
    private static readonly ErrorResult<int, string> Source =
        ErrorResult.Return<int, string>("invalid");
    private static readonly Func<string, ErrorResult<int, string>> Second =
        static error => ErrorResult.Return<int, string>($"{error}!");
    private static readonly Func<string, string, string> Project =
        static (first, second) => $"{first}:{second}";

    [Benchmark(Baseline = true)]
    public ErrorResult<int, string> DirectCase() =>
        Source.Value switch
        {
            int success => new ErrorResult<int, string>(success),
            string firstError => Second(firstError).Value switch
            {
                int secondSuccess => new ErrorResult<int, string>(secondSuccess),
                string secondError =>
                    ErrorResult.Return<int, string>(Project(firstError, secondError))
            }
        };

    [Benchmark]
    public ErrorResult<int, string> SelectMany() =>
        Source.SelectMany(Second, Project);
}

[MemoryDiagnoser]
public class ValidationSuccessBenchmarks
{
    private static readonly Validation<Func<int, int>, string> Function =
        Validation.Return<Func<int, int>, string>(static value => value + 1);
    private static readonly Validation<int, string> Value =
        Validation.Return<int, string>(41);

    [Benchmark(Baseline = true)]
    public Validation<int, string> DirectFunctionCall() =>
        Function switch
        {
            Func<int, int> function => function(41),
            NonEmptyList<string> errors => errors
        };

    [Benchmark]
    public Validation<int, string> ValidationApply() =>
        Function.Apply(Value);
}

[MemoryDiagnoser]
public class ValidationErrorAccumulationBenchmarks
{
    private static readonly NonEmptyList<string> FunctionErrors = new("first");
    private static readonly NonEmptyList<string> ValueErrors = new("second");
    private static readonly Validation<Func<int, int>, string> FailedFunction = FunctionErrors;
    private static readonly Validation<int, string> FailedValue = ValueErrors;

    [Benchmark(Baseline = true)]
    public NonEmptyList<string> DirectErrorAppend() =>
        FunctionErrors.Append(ValueErrors);

    [Benchmark]
    public Validation<int, string> ValidationApplyAccumulates() =>
        FailedFunction.Apply(FailedValue);
}
