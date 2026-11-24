using System;
using System.Collections.Generic;
using System.Linq;

using TUnit.Core;
using TUnit.FsCheck;
using static LondoSpark.Func.Option<int>;
using static LondoSpark.Func.Result<int, string>;

namespace LondoSpark.Func.Tests;

public sealed class ApplicativeLawProperties
{
    [Test, FsCheckProperty]
    public bool OptionFunctorIdentity(bool hasValue, int value)
    {
        var source = hasValue ? Option.Return(value) : Option.Empty<int>();
        return OptionEquivalent(source.Select(item => item), source);
    }

    [Test, FsCheckProperty]
    public bool OptionFunctorComposition(bool hasValue, int value, int offset)
    {
        var source = hasValue ? Option.Return(value) : Option.Empty<int>();
        var left = source.Select(item => unchecked(item + offset)).Select(item => item * 2);
        var right = source.Select(item => unchecked((item + offset) * 2));
        return OptionEquivalent(left, right);
    }

    [Test, FsCheckProperty]
    public bool OptionApplicativeIdentity(bool hasValue, int value)
    {
        var source = hasValue ? Option.Return(value) : Option.Empty<int>();
        var identity = Option.Return<Func<int, int>>(item => item);
        return OptionEquivalent(identity.Apply(source), source);
    }

    [Test, FsCheckProperty]
    public bool OptionApplicativeHomomorphism(int value, int offset)
    {
        Func<int, int> function = item => unchecked(item + offset);
        var left = Option.Return(function).Apply(Option.Return(value));
        var right = Option.Return(function(value));
        return OptionEquivalent(left, right);
    }

    [Test, FsCheckProperty]
    public bool OptionApplicativeInterchange(bool hasFunction, int value, int offset)
    {
        var function = hasFunction
            ? Option.Return<Func<int, int>>(item => unchecked(item + offset))
            : Option.Empty<Func<int, int>>();
        var argument = Option.Return(value);

        var left = function.Apply(argument);
        var right = Option.Return<Func<Func<int, int>, int>>(candidate => candidate(value))
            .Apply(function);
        return OptionEquivalent(left, right);
    }

    [Test, FsCheckProperty]
    public bool OptionApplicativeComposition(bool uHasValue, bool vHasValue, bool wHasValue, int value)
    {
        var u = uHasValue
            ? Option.Return<Func<int, int>>(item => unchecked(item + 1))
            : Option.Empty<Func<int, int>>();
        var v = vHasValue
            ? Option.Return<Func<int, int>>(item => unchecked(item * 2))
            : Option.Empty<Func<int, int>>();
        var w = wHasValue ? Option.Return(value) : Option.Empty<int>();

        Func<Func<int, int>, Func<Func<int, int>, Func<int, int>>> composeFunction =
            ComposeContainer;
        var compose =
            Option.Return(composeFunction);

        var left = compose.Apply(u).Apply(v).Apply(w);
        var right = u.Apply(v.Apply(w));
        return OptionEquivalent(left, right);
    }

    [Test, FsCheckProperty]
    public bool ResultFunctorIdentity(bool isSuccess, int value)
    {
        var source = isSuccess
            ? Result.Return<int, string>(value)
            : Result.Failure<int, string>("failure");
        return ResultEquivalent(source.Select(item => item), source);
    }

    [Test, FsCheckProperty]
    public bool ResultFunctorComposition(bool isSuccess, int value, int offset)
    {
        var source = isSuccess
            ? Result.Return<int, string>(value)
            : Result.Failure<int, string>("failure");
        var left = source.Select(item => unchecked(item + offset)).Select(item => item * 2);
        var right = source.Select(item => unchecked((item + offset) * 2));
        return ResultEquivalent(left, right);
    }

    [Test, FsCheckProperty]
    public bool ResultApplicativeIdentity(bool isSuccess, int value)
    {
        var source = isSuccess
            ? Result.Return<int, string>(value)
            : Result.Failure<int, string>("failure");
        var identity =
            Result.Return<Func<int, int>, string>(item => item);
        return ResultEquivalent(identity.Apply(source), source);
    }

    [Test, FsCheckProperty]
    public bool ResultApplicativeHomomorphism(int value, int offset)
    {
        Func<int, int> function = item => unchecked(item + offset);
        var left = Result.Return<Func<int, int>, string>(function)
            .Apply(Result.Return<int, string>(value));
        var right = Result.Return<int, string>(function(value));
        return ResultEquivalent(left, right);
    }

    [Test, FsCheckProperty]
    public bool ResultApplicativeInterchange(bool functionFails, int value)
    {
        var function = functionFails
            ? Result.Failure<Func<int, int>, string>("function failure")
            : Result.Return<Func<int, int>, string>(item => unchecked(item + 1));
        var argument = Result.Return<int, string>(value);

        var left = function.Apply(argument);
        var right = Result.Return<Func<Func<int, int>, int>, string>(
                candidate => candidate(value))
            .Apply(function);
        return ResultEquivalent(left, right);
    }

    [Test, FsCheckProperty]
    public bool ResultApplicativeComposition(bool uFails, bool vFails, bool wFails, int value)
    {
        var u = uFails
            ? Result.Failure<Func<int, int>, string>("u failure")
            : Result.Return<Func<int, int>, string>(item => unchecked(item + 1));
        var v = vFails
            ? Result.Failure<Func<int, int>, string>("v failure")
            : Result.Return<Func<int, int>, string>(item => unchecked(item * 2));
        var w = wFails
            ? Result.Failure<int, string>("w failure")
            : Result.Return<int, string>(value);

        var compose =
            Result.Return<Func<Func<int, int>, Func<Func<int, int>, Func<int, int>>>, string>(
                ComposeContainer);

        var left = compose.Apply(u).Apply(v).Apply(w);
        var right = u.Apply(v.Apply(w));
        return ResultEquivalent(left, right);
    }

    [Test, FsCheckProperty]
    public bool ErrorResultFunctorIdentity(bool isFailure, int value)
    {
        var source = isFailure
            ? ErrorResult.Return<int, string>("failure")
            : Result.Return<int, string>(value).AsErrorResult();
        return ErrorResultEquivalent(source.Select(error => error), source);
    }

    [Test, FsCheckProperty]
    public bool ErrorResultFunctorComposition(bool isFailure, int value)
    {
        var source = isFailure
            ? ErrorResult.Return<int, string>("failure")
            : Result.Return<int, string>(value).AsErrorResult();
        var left = source.Select(error => error + "-one").Select(error => error + "-two");
        var right = source.Select(error => error + "-one" + "-two");
        return ErrorResultEquivalent(left, right);
    }

    [Test, FsCheckProperty]
    public bool ErrorResultApplicativeIdentity(bool isFailure, int value)
    {
        var source = isFailure
            ? ErrorResult.Return<string, int>(value)
            : Result.Return<string, int>("pass-through").AsErrorResult();
        var identity =
            ErrorResult.Return<string, Func<int, int>>(item => item);
        return ErrorResultEquivalent(identity.Apply(source), source);
    }

    [Test, FsCheckProperty]
    public bool ErrorResultApplicativeHomomorphism(int value, int offset)
    {
        Func<int, int> function = item => unchecked(item + offset);
        var left = ErrorResult.Return<string, Func<int, int>>(function)
            .Apply(ErrorResult.Return<string, int>(value));
        var right = ErrorResult.Return<string, int>(function(value));
        return ErrorResultEquivalent(left, right);
    }

    [Test, FsCheckProperty]
    public bool ErrorResultApplicativeInterchange(bool functionFails, int value)
    {
        var function = functionFails
            ? ErrorResult.Return<string, Func<int, int>>(item => unchecked(item + 1))
            : Result.Return<string, Func<int, int>>("function pass-through").AsErrorResult();
        var argument = ErrorResult.Return<string, int>(value);

        var left = function.Apply(argument);
        var right = ErrorResult.Return<string, Func<Func<int, int>, int>>(
                candidate => candidate(value))
            .Apply(function);
        return ErrorResultEquivalent(left, right);
    }

    [Test, FsCheckProperty]
    public bool ErrorResultApplicativeComposition(bool uFails, bool vFails, bool wFails, int value)
    {
        var u = uFails
            ? ErrorResult.Return<string, Func<int, int>>(item => unchecked(item + 1))
            : Result.Return<string, Func<int, int>>("u pass-through").AsErrorResult();
        var v = vFails
            ? ErrorResult.Return<string, Func<int, int>>(item => unchecked(item * 2))
            : Result.Return<string, Func<int, int>>("v pass-through").AsErrorResult();
        var w = wFails
            ? ErrorResult.Return<string, int>(value)
            : Result.Return<string, int>("w pass-through").AsErrorResult();

        var compose =
            ErrorResult.Return<
                string,
                Func<Func<int, int>, Func<Func<int, int>, Func<int, int>>>>(
                ComposeContainer);

        var left = compose.Apply(u).Apply(v).Apply(w);
        var right = u.Apply(v.Apply(w));
        return ErrorResultEquivalent(left, right);
    }

    [Test, FsCheckProperty]
    public bool ValidationApplicativeIdentity(bool isFailure, int value)
    {
        var source = isFailure
            ? Validation.Failure<int, string>("failure")
            : Validation.Return<int, string>(value);
        var identity =
            Validation.Return<Func<int, int>, string>(item => item);
        return ValidationEquivalent(identity.Apply(source), source);
    }

    [Test, FsCheckProperty]
    public bool ValidationApplicativeHomomorphism(int value, int offset)
    {
        Func<int, int> function = item => unchecked(item + offset);
        var left = Validation.Return<Func<int, int>, string>(function)
            .Apply(Validation.Return<int, string>(value));
        var right = Validation.Return<int, string>(function(value));
        return ValidationEquivalent(left, right);
    }

    [Test, FsCheckProperty]
    public bool ValidationApplicativeInterchange(bool functionFails, int value)
    {
        var function = functionFails
            ? Validation.Failure<Func<int, int>, string>("function failure")
            : Validation.Return<Func<int, int>, string>(item => unchecked(item + 1));
        var argument = Validation.Return<int, string>(value);

        var left = function.Apply(argument);
        var right = Validation.Return<Func<Func<int, int>, int>, string>(
                candidate => candidate(value))
            .Apply(function);
        return ValidationEquivalent(left, right);
    }

    [Test, FsCheckProperty]
    public bool ValidationFunctorIdentity(bool isFailure, int value)
    {
        var source = isFailure
            ? Validation.Failure<int, string>("failure")
            : Validation.Return<int, string>(value);
        return ValidationEquivalent(source.Select(item => item), source);
    }

    [Test, FsCheckProperty]
    public bool ValidationFunctorComposition(bool isFailure, int value, int offset)
    {
        var source = isFailure
            ? Validation.Failure<int, string>("failure")
            : Validation.Return<int, string>(value);
        var left = source.Select(item => unchecked(item + offset)).Select(item => item * 2);
        var right = source.Select(item => unchecked((item + offset) * 2));
        return ValidationEquivalent(left, right);
    }

    [Test, FsCheckProperty]
    public bool ValidationApplicativeComposition(
        bool uFails,
        bool vFails,
        bool wFails,
        int value)
    {
        var u = uFails
            ? Validation.Failure<Func<int, int>, string>("u failure")
            : Validation.Return<Func<int, int>, string>(item => unchecked(item + 1));
        var v = vFails
            ? Validation.Failure<Func<int, int>, string>("v failure")
            : Validation.Return<Func<int, int>, string>(item => unchecked(item * 2));
        var w = wFails
            ? Validation.Failure<int, string>("w failure")
            : Validation.Return<int, string>(value);

        var compose =
            Validation.Return<
                Func<Func<int, int>, Func<Func<int, int>, Func<int, int>>>,
                string>(
                ComposeContainer);

        var left = compose.Apply(u).Apply(v).Apply(w);
        var right = u.Apply(v.Apply(w));
        return ValidationEquivalent(left, right);
    }

    private static bool OptionEquivalent(Option<int> left, Option<int> right) =>
        left switch
        {
            Some leftSome => right is Some rightSome
                && leftSome.Value == rightSome.Value,
            None => right is None
        };

    private static bool ResultEquivalent(Result<int, string> left, Result<int, string> right) =>
        left switch
        {
            Success leftSuccess => right is Success rightSuccess
                && leftSuccess.Value == rightSuccess.Value,
            Failure leftFailure => right is Failure rightFailure
                && leftFailure.Value == rightFailure.Value
        };

    private static bool ErrorResultEquivalent(
        ErrorResult<string, int> left,
        ErrorResult<string, int> right) =>
        left.ToResult() switch
        {
            Result<string, int>.Success leftPass => right.ToResult() is Result<string, int>.Success rightPass
                && leftPass.Value == rightPass.Value,
            Result<string, int>.Failure leftFailure => right.ToResult() is Result<string, int>.Failure rightFailure
                && leftFailure.Value == rightFailure.Value
        };

    private static bool ErrorResultEquivalent<TSuccess, TFailure>(
        ErrorResult<TSuccess, TFailure> left,
        ErrorResult<TSuccess, TFailure> right) =>
        left.ToResult() switch
        {
            Result<TSuccess, TFailure>.Success leftSuccess => right.ToResult() is Result<TSuccess, TFailure>.Success rightSuccess
                && EqualityComparer<TSuccess>.Default.Equals(
                    leftSuccess.Value,
                    rightSuccess.Value),
            Result<TSuccess, TFailure>.Failure leftFailure => right.ToResult() is Result<TSuccess, TFailure>.Failure rightFailure
                && EqualityComparer<TFailure>.Default.Equals(
                    leftFailure.Value,
                    rightFailure.Value)
        };

    private static bool ValidationEquivalent<TSuccess>(
        Validation<TSuccess, string> left,
        Validation<TSuccess, string> right) =>
        left switch
        {
            Validation<TSuccess, string>.Success leftSuccess => right is Validation<TSuccess, string>.Success rightSuccess
                && EqualityComparer<TSuccess>.Default.Equals(
                    leftSuccess.Value,
                    rightSuccess.Value),
            Validation<TSuccess, string>.Failure leftFailure =>
                right is Validation<TSuccess, string>.Failure rightFailure
                && leftFailure.Errors.SequenceEqual(rightFailure.Errors)
        };

    private static Func<Func<int, int>, Func<int, int>> ComposeContainer(
        Func<int, int> first) =>
        second => item => first(second(item));
}
