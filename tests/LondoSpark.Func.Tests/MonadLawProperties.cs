using LondoSpark.Func;
using TUnit.Core;
using TUnit.FsCheck;

namespace LondoSpark.Func.Tests;

public sealed class MonadLawProperties
{
    [Test, FsCheckProperty]
    public bool OptionLeftIdentity(int value, int offset, bool returnsNone)
    {
        Option<int> Bind(int input) =>
            returnsNone ? Option.Empty<int>() : Option.Return(unchecked(input + offset));

        return Equivalent(
            Option.Return(value).SelectMany(Bind),
            Bind(value));
    }

    [Test, FsCheckProperty]
    public bool OptionRightIdentity(bool hasValue, int value)
    {
        Option<int> source = hasValue ? Option.Return(value) : Option.Empty<int>();
        var bound = source.SelectMany(item => Option.Return(item));

        return Equivalent(bound, source);
    }

    [Test, FsCheckProperty]
    public bool OptionAssociativity(
        bool hasValue,
        int value,
        int firstOffset,
        bool firstReturnsNone,
        int secondOffset,
        bool secondReturnsNone)
    {
        Option<int> source = hasValue ? Option.Return(value) : Option.Empty<int>();

        Option<int> First(int input) =>
            firstReturnsNone ? Option.Empty<int>() : Option.Return(unchecked(input + firstOffset));

        Option<int> Second(int input) =>
            secondReturnsNone ? Option.Empty<int>() : Option.Return(unchecked(input + secondOffset));

        var left = source.SelectMany(First).SelectMany(Second);
        var right = source.SelectMany(input => First(input).SelectMany(Second));

        return Equivalent(left, right);
    }

    [Test, FsCheckProperty]
    public bool ResultLeftIdentity(int value, int offset, bool returnsFailure)
    {
        Result<int, string> Bind(int input) => ResultStep(input, offset, returnsFailure);

        return Equivalent(
            Success(value).SelectMany(Bind),
            Bind(value));
    }

    [Test, FsCheckProperty]
    public bool ResultRightIdentity(bool isSuccess, int value)
    {
        var source = isSuccess ? Success(value) : Failure(value);
        var bound = source.SelectMany(Success);

        return Equivalent(bound, source);
    }

    [Test, FsCheckProperty]
    public bool ResultAssociativity(
        bool isSuccess,
        int value,
        int firstOffset,
        bool firstReturnsFailure,
        int secondOffset,
        bool secondReturnsFailure)
    {
        var source = isSuccess ? Success(value) : Failure(value);
        Result<int, string> First(int input) => ResultStep(input, firstOffset, firstReturnsFailure);
        Result<int, string> Second(int input) => ResultStep(input, secondOffset, secondReturnsFailure);

        var left = source.SelectMany(First).SelectMany(Second);
        var right = source.SelectMany(input => First(input).SelectMany(Second));

        return Equivalent(left, right);
    }

    [Test, FsCheckProperty]
    public bool ErrorResultLeftIdentity(int errorCode, int nextErrorCode, bool returnsSuccess)
    {
        var error = Failure(errorCode).AsErrorResult();

        ErrorResult<int, string> Bind(string currentError) =>
            ErrorStep(currentError, nextErrorCode, returnsSuccess);

        return Equivalent(
            error.BindError(Bind),
            Bind(ErrorMessage(errorCode)));
    }

    [Test, FsCheckProperty]
    public bool ErrorResultRightIdentity(bool isFailure, int value)
    {
        var source = (isFailure ? Failure(value) : Success(value)).AsErrorResult();
        var bound = source.BindError(error => ErrorResult.Return<int, string>(error));

        return Equivalent(bound, source);
    }

    [Test, FsCheckProperty]
    public bool ErrorResultAssociativity(
        bool isFailure,
        int value,
        int firstErrorCode,
        bool firstReturnsSuccess,
        int secondErrorCode,
        bool secondReturnsSuccess)
    {
        var source = (isFailure ? Failure(value) : Success(value)).AsErrorResult();

        ErrorResult<int, string> First(string error) =>
            ErrorStep(error, firstErrorCode, firstReturnsSuccess);

        ErrorResult<int, string> Second(string error) =>
            ErrorStep(error, secondErrorCode, secondReturnsSuccess);

        var left = source.BindError(First).BindError(Second);
        var right = source.BindError(error => First(error).BindError(Second));

        return Equivalent(left, right);
    }

    private static bool Equivalent(Option<int> left, Option<int> right) =>
        left switch
        {
            Some<int> leftSome => right switch
            {
                Some<int> rightSome => leftSome.Value == rightSome.Value,
                None _ => false
            },
            None _ => right is None
        };

    private static Result<int, string> Success(int value) =>
        Result.Return<int, string>(value);

    private static Result<int, string> Failure(int errorCode) =>
        Result.Failure<int, string>(ErrorMessage(errorCode));

    private static string ErrorMessage(int errorCode) => $"error:{errorCode}";

    private static Result<int, string> ResultStep(int input, int offset, bool returnsFailure) =>
        returnsFailure
            ? Failure(offset)
            : Success(unchecked(input + offset));

    private static ErrorResult<int, string> ErrorStep(
        string currentError,
        int errorCode,
        bool returnsSuccess) =>
        new(returnsSuccess
            ? Success(errorCode)
            : Result.Failure<int, string>($"{currentError}|{errorCode}"));

    private static bool Equivalent(
        Result<int, string> left,
        Result<int, string> right) =>
        left switch
        {
            Success<int> leftSuccess => right switch
            {
                Success<int> rightSuccess => leftSuccess.Value == rightSuccess.Value,
                Failure<string> _ => false
            },
            Failure<string> leftFailure => right switch
            {
                Failure<string> rightFailure => leftFailure.Value == rightFailure.Value,
                Success<int> _ => false
            }
        };

    private static bool Equivalent(
        ErrorResult<int, string> left,
        ErrorResult<int, string> right) =>
        Equivalent(left.ToResult(), right.ToResult());
}
