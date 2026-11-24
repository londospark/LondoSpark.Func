using LondoSpark.Func;
using TUnit.Core;
using TUnit.FsCheck;

namespace LondoSpark.Func.Tests;

public sealed class EqualityProperties
{
    [Test, FsCheckProperty]
    public bool OptionEqualValuesShareHashCode(
        bool firstHasValue,
        int firstValue,
        bool secondHasValue,
        int secondValue)
    {
        var first = CreateOption(firstHasValue, firstValue);
        var second = CreateOption(secondHasValue, secondValue);

        return !first.Equals(second) || first.GetHashCode() == second.GetHashCode();
    }

    [Test, FsCheckProperty]
    public bool OptionEqualityIsSymmetric(
        bool firstHasValue,
        int firstValue,
        bool secondHasValue,
        int secondValue)
    {
        var first = CreateOption(firstHasValue, firstValue);
        var second = CreateOption(secondHasValue, secondValue);

        return first.Equals(second) == second.Equals(first)
            && (first == second) == (second == first)
            && (first != second) == (second != first);
    }

    [Test, FsCheckProperty]
    public bool ResultEqualValuesShareHashCode(
        bool firstIsSuccess,
        int firstValue,
        bool secondIsSuccess,
        int secondValue)
    {
        var first = CreateResult(firstIsSuccess, firstValue);
        var second = CreateResult(secondIsSuccess, secondValue);

        return !first.Equals(second) || first.GetHashCode() == second.GetHashCode();
    }

    [Test, FsCheckProperty]
    public bool ResultEqualityDistinguishesCasesAndPayloads(int value)
    {
        Result<int, int> success = Result.Return<int, int>(value);
        Result<int, int> failure = Result.Failure<int, int>(value);

        return !success.Equals(failure)
            && !failure.Equals(success)
            && success != failure;
    }

    private static Option<int> CreateOption(bool hasValue, int value) =>
        hasValue ? Option.Return(value) : Option.Empty<int>();

    private static Result<int, int> CreateResult(bool isSuccess, int value) =>
        isSuccess
            ? Result.Return<int, int>(value)
            : Result.Failure<int, int>(value);
}
