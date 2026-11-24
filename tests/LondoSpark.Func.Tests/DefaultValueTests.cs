using System;
using System.Threading.Tasks;

using LondoSpark.Func.FSharp;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;
using static LondoSpark.Func.Validation<int, string>;

namespace LondoSpark.Func.Tests;

// The zero values are the subject of these tests.
#pragma warning disable LSF0001

/// <summary>Zero-initialised unions contain no case; every operation must say so instead of failing obscurely.</summary>
public sealed class DefaultValueTests
{
    [Test]
    public async Task DefaultResultOperationsThrowInvalidOperation()
    {
        Result<int, string> result = default;
        Result<Func<int, int>, string> function = default;

        await AssertNoCase(() => result.Select(static value => value));
        await AssertNoCase(() => result.Bind(static value => Result.Return<int, string>(value)));
        await AssertNoCase(() => result.SelectMany(
            static value => Result.Return<int, string>(value),
            static (first, second) => first + second));
        await AssertNoCase(() => Result.Return<int, string>(1).SelectMany(
            static _ => default(Result<int, string>),
            static (first, second) => first + second));
        await AssertNoCase(() => result.MapError(static error => error.Length));
        await AssertNoCase(() => result.BindError(static error => Result.Failure<int, int>(error.Length)));
        await AssertNoCase(() => result.ToExitCode());
        await AssertNoCase(() => function.Apply(Result.Return<int, string>(1)));
        await AssertNoCase(() => Result.Return<Func<int, int>, string>(static value => value).Apply(result));
        await AssertNoCase(() => default(Result<int, Unit>).ToOption());
        await AssertNoCase(() => result.ToFSharpResult());
    }

    [Test]
    public async Task DefaultErrorResultOperationsThrowInvalidOperation()
    {
        ErrorResult<int, string> result = default;

        await AssertNoCase(() => result.Select(static error => error.Length));
        await AssertNoCase(() => result.BindError(static error => ErrorResult.Return<int, int>(error.Length)));
        await AssertNoCase(() => result.SelectMany(
            static error => ErrorResult.Return<int, int>(error.Length),
            static (error, length) => length));
    }

    [Test]
    public async Task DefaultValidationContainsNoCase()
    {
        Validation<int, string> validation = default;

        await Assert.That(validation.HasValue).IsFalse();
        await Assert.That(validation is Success).IsFalse();
        await Assert.That(validation is Failure).IsFalse();
        await Assert.That(validation.ToString()).IsEqualTo("Validation(default)");
        await Assert.That(validation).IsEqualTo(default(Validation<int, string>));

        var exception = Assert.Throws<InvalidOperationException>(() => _ = validation.Value);
        await Assert.That(exception.Message).Contains("Validation.Return");
    }

    [Test]
    public async Task DefaultValidationOperationsThrowInvalidOperation()
    {
        Validation<int, string> validation = default;
        Validation<Func<int, int>, string> function = default;

        await AssertNoCase(() => validation.Select(static value => value));
        await AssertNoCase(() => validation.ToResult());
        await AssertNoCase(() => function.Apply(Validation.Return<int, string>(1)));
        await AssertNoCase(() => Validation.Return<Func<int, int>, string>(static value => value).Apply(validation));
        await AssertNoCase(() => Validation.Failure<Func<int, int>, string>("error").Apply(validation));
    }

    private static async Task AssertNoCase<T>(Func<T> operation)
    {
        var exception = Assert.Throws<InvalidOperationException>(() => _ = operation());
        await Assert.That(exception.Message).Contains("contains no case");
    }
}
