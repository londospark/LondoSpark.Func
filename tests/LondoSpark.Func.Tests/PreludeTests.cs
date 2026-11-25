using System;
using System.Linq;
using System.Threading.Tasks;

using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;
using TUnit.FsCheck;

using static LondoSpark.Func.Prelude;

namespace LondoSpark.Func.Tests;

/// <summary>The prelude builds every union in every position a value can appear, with no constructors in sight.</summary>
public sealed class PreludeTests
{
    // ----- Option -----

    [Test]
    public async Task SomeInfersItsOptionType()
    {
        var number = some(3);
        var text = some("text");
        var nothing = some<string?>(null);

        await Assert.That(number).IsEqualTo(Option.Return(3));
        await Assert.That(text).IsEqualTo(Option.Return("text"));
        await Assert.That(nothing.ToString()).IsEqualTo("Some(null)");
        await Assert.That(nothing == Option.Empty<string?>()).IsFalse();
    }

    [Test]
    public async Task NoneConvertsToAnAbsentOptionOfAnyType()
    {
        Option<int> number = none;
        Option<string> text = none;
        Option<Option<Guid>> nested = none;

        await Assert.That(number).IsEqualTo(Option.Empty<int>());
        await Assert.That(text).IsEqualTo(Option.Empty<string>());
        await Assert.That(nested).IsEqualTo(Option.Empty<Option<Guid>>());
        await Assert.That(none).IsEqualTo(default(None));
    }

    [Test]
    public async Task SomeAndNoneMixInConditionalsInEitherOrder()
    {
        var first = Pick(true, 1);
        var second = Pick(false, 1);
        var reversed = DateTime.UtcNow.Year < 2000 ? none : some(2);

        await Assert.That(first).IsEqualTo(Option.Return(1));
        await Assert.That(second).IsEqualTo(Option.Empty<int>());
        await Assert.That(reversed).IsEqualTo(Option.Return(2));

        static Option<int> Pick(bool present, int value) => present ? some(value) : none;
    }

    [Test]
    public async Task SomeAndNoneWorkAsSwitchArmsLambdaResultsAndArguments()
    {
        static Option<string> Describe(int code) =>
            code switch
            {
                0 => none,
                < 0 => some("negative"),
                _ => some($"#{code}")
            };

        Func<int, Option<int>> positive = static value => value > 0 ? some(value) : none;
        static int Count(Option<int> value) => value is None ? 0 : 1;

        await Assert.That(Describe(0)).IsEqualTo(Option.Empty<string>());
        await Assert.That(Describe(-1)).IsEqualTo(Option.Return("negative"));
        await Assert.That(Describe(5)).IsEqualTo(Option.Return("#5"));
        await Assert.That(positive(4)).IsEqualTo(Option.Return(4));
        await Assert.That(positive(-4)).IsEqualTo(Option.Empty<int>());
        await Assert.That(Count(some(1)) + Count(none)).IsEqualTo(1);
    }

    [Test]
    public async Task SomeAndNoneComposeWithBindAndQuerySyntax()
    {
        var halved = some(10).Bind(static value => value % 2 == 0 ? some(value / 2) : none);
        var odd = some(9).Bind(static value => value % 2 == 0 ? some(value / 2) : none);
        var sum = from first in some(1)
                  from second in some(2)
                  select first + second;
        var shortCircuited = from first in some(1)
                             from second in Option.Empty<int>()
                             select first + second;

        await Assert.That(halved).IsEqualTo(Option.Return(5));
        await Assert.That(odd).IsEqualTo(Option.Empty<int>());
        await Assert.That(sum).IsEqualTo(Option.Return(3));
        await Assert.That(shortCircuited).IsEqualTo(Option.Empty<int>());
    }

    [Test]
    public async Task SomeAndNoneFillCollectionsAndNest()
    {
        Option<int>[] values = [some(1), none, some(3)];
        var nested = some(some(1));
        var nestedAbsent = some(Option.Empty<int>());

        await Assert.That(values.Count(static value => value is not None)).IsEqualTo(2);
        await Assert.That(nested).IsEqualTo(Option.Return(Option.Return(1)));
        await Assert.That(nestedAbsent == Option.Empty<Option<int>>()).IsFalse();
    }

    [Test]
    public async Task PreludeOptionsMatchTheirCases()
    {
        static string Show(Option<int> value) =>
            value switch
            {
                Option<int>.Some(var number) => $"some {number}",
                None => "none"
            };

        await Assert.That(Show(some(4))).IsEqualTo("some 4");
        await Assert.That(Show(none)).IsEqualTo("none");
    }

    // ----- Result -----

    [Test]
    public async Task SuccessAndFailureTakeTheirOtherTypeFromAReturn()
    {
        await Assert.That(Parse("42")).IsEqualTo(Result.Return<int, string>(42));
        await Assert.That(Parse("x")).IsEqualTo(Result.Failure<int, string>("'x' is not a number"));
    }

    [Test]
    public async Task SuccessAndFailureWorkAsDeclarationsAssignmentsAndSwitchArms()
    {
        Result<int, string> declared = success(1);
        Result<int, string> assigned;
        assigned = failure("later");

        static Result<string, int> Classify(int code) =>
            code switch
            {
                0 => failure(0),
                < 0 => failure(code),
                _ => success($"#{code}")
            };

        await Assert.That(declared).IsEqualTo(Result.Return<int, string>(1));
        await Assert.That(assigned).IsEqualTo(Result.Failure<int, string>("later"));
        await Assert.That(Classify(3)).IsEqualTo(Result.Return<string, int>("#3"));
        await Assert.That(Classify(-2)).IsEqualTo(Result.Failure<string, int>(-2));
    }

    [Test]
    public async Task SuccessAndFailureKeepSameTypedCasesApart()
    {
        Result<string, string> ok = success("same");
        Result<string, string> error = failure("same");

        await Assert.That(ok.ToExitCode()).IsEqualTo(0);
        await Assert.That(error.ToExitCode()).IsEqualTo(1);
        await Assert.That(ok == error).IsFalse();
    }

    [Test]
    public async Task SuccessAndFailureReturnFromAsyncMethods()
    {
        static async Task<Result<int, string>> LoadAsync(bool found)
        {
            await Task.Yield();
            return found ? success(7) : failure("missing");
        }

        await Assert.That(await LoadAsync(true)).IsEqualTo(Result.Return<int, string>(7));
        await Assert.That(await LoadAsync(false)).IsEqualTo(Result.Failure<int, string>("missing"));
    }

    [Test]
    public async Task SuccessAndFailureWorkInLambdasWithAnExplicitReturnTypeOrMethodGroups()
    {
        // C# does not infer a generic method's type argument through user-defined conversions, so a lambda handed to
        // Bind names its result type, or the step is a method group.
        var source = Result.Return<int, string>(5);

        var explicitLambda = source.Bind(Result<int, string> (value) => value > 0 ? success(value * 2) : failure("negative"));
        var methodGroup = source.Bind(Halve);
        var query = from value in source
                    from half in Halve(value)
                    select value + half;

        await Assert.That(explicitLambda).IsEqualTo(Result.Return<int, string>(10));
        await Assert.That(methodGroup).IsEqualTo(Result.Failure<int, string>("5 is odd"));
        await Assert.That(Result.Return<int, string>(4).Bind(Halve)).IsEqualTo(Result.Return<int, string>(2));
        await Assert.That(query).IsEqualTo(Result.Failure<int, string>("5 is odd"));

        static Result<int, string> Halve(int value) => value % 2 == 0 ? success(value / 2) : failure($"{value} is odd");
    }

    [Test]
    public async Task SuccessAndFailureCarryNullPayloads()
    {
        Result<string?, string?> nullSuccess = success<string?>(null);
        Result<string?, string?> nullFailure = failure<string?>(null);

        await Assert.That(nullSuccess.ToExitCode()).IsEqualTo(0);
        await Assert.That(nullFailure.ToExitCode()).IsEqualTo(1);
        await Assert.That(nullSuccess == nullFailure).IsFalse();
    }

    [Test]
    public async Task PreludeResultsCarryOptions()
    {
        Result<Option<int>, string> present = success(some(1));
        Result<Option<int>, string> absent = success(Option.Empty<int>());

        await Assert.That(present).IsEqualTo(Result.Return<Option<int>, string>(Option.Return(1)));
        await Assert.That(absent).IsEqualTo(Result.Return<Option<int>, string>(Option.Empty<int>()));
    }

    // ----- Validation -----

    [Test]
    public async Task SuccessAndFailureBuildValidations()
    {
        Validation<int, string> valid = success(1);
        Validation<int, string> invalid = failure("name is required");

        await Assert.That(valid.ToString()).IsEqualTo("Success(1)");
        await Assert.That(invalid.ToString()).IsEqualTo("Failure(name is required)");
    }

    [Test]
    public async Task PreludeValidationsAccumulateThroughApply()
    {
        Validation<Func<string, Func<int, string>>, string> make = success<Func<string, Func<int, string>>>(
            static name => age => $"{name} ({age})");
        Validation<string, string> name = failure("name is required");
        Validation<int, string> age = failure("age is required");
        Validation<string, string> goodName = success("Ada");
        Validation<int, string> goodAge = success(36);

        await Assert.That(make.Apply(name).Apply(age).ToString()).IsEqualTo("Failure(name is required, age is required)");
        await Assert.That(make.Apply(goodName).Apply(goodAge).ToString()).IsEqualTo("Success(Ada (36))");
    }

    // ----- Shared None -----

    [Test]
    public async Task EveryOptionSharesOneBoxedNone()
    {
        var fromInt = Option.Empty<int>().Value;
        var fromString = Option.Empty<string>().Value;
        var fromNested = Option.Empty<Option<Guid>>().Value;
        var fromPrelude = ((Option<double>)none).Value;

        await Assert.That(ReferenceEquals(fromInt, fromString)).IsTrue();
        await Assert.That(ReferenceEquals(fromInt, fromNested)).IsTrue();
        await Assert.That(ReferenceEquals(fromInt, fromPrelude)).IsTrue();
        await Assert.That(fromInt is None).IsTrue();
    }

    // ----- Properties -----

    [Test, FsCheckProperty]
    public bool SomeMatchesOptionReturn(int value) =>
        some(value) == Option.Return(value);

    [Test, FsCheckProperty]
    public bool SuccessMatchesResultReturn(int value)
    {
        Result<int, string> built = success(value);
        return built == Result.Return<int, string>(value);
    }

    [Test, FsCheckProperty]
    public bool FailureMatchesResultFailure(string? error)
    {
        Result<int, string?> built = failure(error);
        return built == Result.Failure<int, string?>(error);
    }

    [Test, FsCheckProperty]
    public bool PreludeOptionsAgreeWithNullableConversion(int? value)
    {
        var built = value is { } present ? some(present) : none;
        return built == value.ToOption();
    }

    [Test, FsCheckProperty]
    public bool PreludeValidationFailuresHoldTheirOneError(string error)
    {
        Validation<int, string> built = failure(error);
        return built.ToResult() is Result<int, NonEmptyList<string>>.Failure(var errors)
            && errors.Count == 1
            && errors[0] == error;
    }

    private static Result<int, string> Parse(string text) =>
        int.TryParse(text, out var number) ? success(number) : failure($"'{text}' is not a number");
}
