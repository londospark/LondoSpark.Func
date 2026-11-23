using System;
using System.Collections.Generic;
using System.Linq;

using LondoSpark.Func;
using TUnit.Core;
using TUnit.FsCheck;

namespace LondoSpark.Func.Tests;

public sealed class ApplicativeLawProperties
{
    [Test, FsCheckProperty]
    public bool OptionFunctorIdentity(bool hasValue, int value)
    {
        Option<int> source = hasValue ? Option.Return(value) : new None();
        return OptionEquivalent(source.Select(item => item), source);
    }

    [Test, FsCheckProperty]
    public bool OptionFunctorComposition(bool hasValue, int value, int offset)
    {
        Option<int> source = hasValue ? Option.Return(value) : new None();
        var left = source.Select(item => unchecked(item + offset)).Select(item => item * 2);
        var right = source.Select(item => unchecked((item + offset) * 2));
        return OptionEquivalent(left, right);
    }

    [Test, FsCheckProperty]
    public bool OptionApplicativeIdentity(bool hasValue, int value)
    {
        Option<int> source = hasValue ? Option.Return(value) : new None();
        Option<Func<int, int>> identity = Option.Return<Func<int, int>>(item => item);
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
        Option<Func<int, int>> function = hasFunction
            ? Option.Return<Func<int, int>>(item => unchecked(item + offset))
            : new None();
        Option<int> argument = Option.Return(value);

        var left = function.Apply(argument);
        var right = Option.Return<Func<Func<int, int>, int>>(candidate => candidate(value))
            .Apply(function);
        return OptionEquivalent(left, right);
    }

    [Test, FsCheckProperty]
    public bool OptionApplicativeComposition(bool uHasValue, bool vHasValue, bool wHasValue, int value)
    {
        Option<Func<int, int>> u = uHasValue
            ? Option.Return<Func<int, int>>(item => unchecked(item + 1))
            : new None();
        Option<Func<int, int>> v = vHasValue
            ? Option.Return<Func<int, int>>(item => unchecked(item * 2))
            : new None();
        Option<int> w = wHasValue ? Option.Return(value) : new None();

        Func<Func<int, int>, Func<Func<int, int>, Func<int, int>>> composeFunction =
            ComposeContainer;
        Option<Func<Func<int, int>, Func<Func<int, int>, Func<int, int>>>> compose =
            Option.Return(composeFunction);

        var left = compose.Apply(u).Apply(v).Apply(w);
        var right = u.Apply(v.Apply(w));
        return OptionEquivalent(left, right);
    }

    [Test, FsCheckProperty]
    public bool ResultFunctorIdentity(bool isSuccess, int value)
    {
        Result<int, string> source = isSuccess ? Result.Return<int, string>(value) : "failure";
        return ResultEquivalent(source.Select(item => item), source);
    }

    [Test, FsCheckProperty]
    public bool ResultFunctorComposition(bool isSuccess, int value, int offset)
    {
        Result<int, string> source = isSuccess ? Result.Return<int, string>(value) : "failure";
        var left = source.Select(item => unchecked(item + offset)).Select(item => item * 2);
        var right = source.Select(item => unchecked((item + offset) * 2));
        return ResultEquivalent(left, right);
    }

    [Test, FsCheckProperty]
    public bool ResultApplicativeIdentity(bool isSuccess, int value)
    {
        Result<int, string> source = isSuccess ? Result.Return<int, string>(value) : "failure";
        Result<Func<int, int>, string> identity =
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
        Result<Func<int, int>, string> function = functionFails
            ? "function failure"
            : Result.Return<Func<int, int>, string>(item => unchecked(item + 1));
        Result<int, string> argument = Result.Return<int, string>(value);

        var left = function.Apply(argument);
        var right = Result.Return<Func<Func<int, int>, int>, string>(
                candidate => candidate(value))
            .Apply(function);
        return ResultEquivalent(left, right);
    }

    [Test, FsCheckProperty]
    public bool ResultApplicativeComposition(bool uFails, bool vFails, bool wFails, int value)
    {
        Result<Func<int, int>, string> u = uFails
            ? "u failure"
            : Result.Return<Func<int, int>, string>(item => unchecked(item + 1));
        Result<Func<int, int>, string> v = vFails
            ? "v failure"
            : Result.Return<Func<int, int>, string>(item => unchecked(item * 2));
        Result<int, string> w = wFails ? "w failure" : Result.Return<int, string>(value);

        Result<Func<Func<int, int>, Func<Func<int, int>, Func<int, int>>>, string> compose =
            Result.Return<Func<Func<int, int>, Func<Func<int, int>, Func<int, int>>>, string>(
                ComposeContainer);

        var left = compose.Apply(u).Apply(v).Apply(w);
        var right = u.Apply(v.Apply(w));
        return ResultEquivalent(left, right);
    }

    [Test, FsCheckProperty]
    public bool ErrorResultFunctorIdentity(bool isFailure, int value)
    {
        ErrorResult<int, string> source = isFailure
            ? ErrorResult.Return<int, string>("failure")
            : new Result<int, string>(value).AsErrorResult();
        return ErrorResultEquivalent(source.Select(error => error), source);
    }

    [Test, FsCheckProperty]
    public bool ErrorResultFunctorComposition(bool isFailure, int value)
    {
        ErrorResult<int, string> source = isFailure
            ? ErrorResult.Return<int, string>("failure")
            : new Result<int, string>(value).AsErrorResult();
        var left = source.Select(error => error + "-one").Select(error => error + "-two");
        var right = source.Select(error => error + "-one" + "-two");
        return ErrorResultEquivalent(left, right);
    }

    [Test, FsCheckProperty]
    public bool ErrorResultApplicativeIdentity(bool isFailure, int value)
    {
        ErrorResult<string, int> source = isFailure
            ? ErrorResult.Return<string, int>(value)
            : new Result<string, int>("pass-through").AsErrorResult();
        ErrorResult<string, Func<int, int>> identity =
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
        ErrorResult<string, Func<int, int>> function = functionFails
            ? ErrorResult.Return<string, Func<int, int>>(item => unchecked(item + 1))
            : new Result<string, Func<int, int>>("function pass-through").AsErrorResult();
        ErrorResult<string, int> argument = ErrorResult.Return<string, int>(value);

        var left = function.Apply(argument);
        var right = ErrorResult.Return<string, Func<Func<int, int>, int>>(
                candidate => candidate(value))
            .Apply(function);
        return ErrorResultEquivalent(left, right);
    }

    [Test, FsCheckProperty]
    public bool ErrorResultApplicativeComposition(bool uFails, bool vFails, bool wFails, int value)
    {
        ErrorResult<string, Func<int, int>> u = uFails
            ? ErrorResult.Return<string, Func<int, int>>(item => unchecked(item + 1))
            : new Result<string, Func<int, int>>("u pass-through").AsErrorResult();
        ErrorResult<string, Func<int, int>> v = vFails
            ? ErrorResult.Return<string, Func<int, int>>(item => unchecked(item * 2))
            : new Result<string, Func<int, int>>("v pass-through").AsErrorResult();
        ErrorResult<string, int> w = wFails
            ? ErrorResult.Return<string, int>(value)
            : new Result<string, int>("w pass-through").AsErrorResult();

        ErrorResult<string, Func<Func<int, int>, Func<Func<int, int>, Func<int, int>>>> compose =
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
        Validation<int, string> source = isFailure
            ? Validation.Failure<int, string>("failure")
            : Validation.Return<int, string>(value);
        Validation<Func<int, int>, string> identity =
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
        Validation<Func<int, int>, string> function = functionFails
            ? Validation.Failure<Func<int, int>, string>("function failure")
            : Validation.Return<Func<int, int>, string>(item => unchecked(item + 1));
        Validation<int, string> argument = Validation.Return<int, string>(value);

        var left = function.Apply(argument);
        var right = Validation.Return<Func<Func<int, int>, int>, string>(
                candidate => candidate(value))
            .Apply(function);
        return ValidationEquivalent(left, right);
    }

    [Test, FsCheckProperty]
    public bool ValidationFunctorIdentity(bool isFailure, int value)
    {
        Validation<int, string> source = isFailure
            ? Validation.Failure<int, string>("failure")
            : Validation.Return<int, string>(value);
        return ValidationEquivalent(source.Select(item => item), source);
    }

    [Test, FsCheckProperty]
    public bool ValidationFunctorComposition(bool isFailure, int value, int offset)
    {
        Validation<int, string> source = isFailure
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
        Validation<Func<int, int>, string> u = uFails
            ? Validation.Failure<Func<int, int>, string>("u failure")
            : Validation.Return<Func<int, int>, string>(item => unchecked(item + 1));
        Validation<Func<int, int>, string> v = vFails
            ? Validation.Failure<Func<int, int>, string>("v failure")
            : Validation.Return<Func<int, int>, string>(item => unchecked(item * 2));
        Validation<int, string> w = wFails
            ? Validation.Failure<int, string>("w failure")
            : Validation.Return<int, string>(value);

        Validation<Func<Func<int, int>, Func<Func<int, int>, Func<int, int>>>, string> compose =
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
            int leftValue => right is int rightValue && leftValue == rightValue,
            None _ => right is None
        };

    private static bool ResultEquivalent(Result<int, string> left, Result<int, string> right) =>
        left switch
        {
            int leftValue => right is int rightValue && leftValue == rightValue,
            string leftError => right is string rightError && leftError == rightError
        };

    private static bool ErrorResultEquivalent(
        ErrorResult<string, int> left,
        ErrorResult<string, int> right) =>
        left.ToResult() switch
        {
            string leftPass => right.ToResult() is string rightPass && leftPass == rightPass,
            int leftFailure => right.ToResult() is int rightFailure && leftFailure == rightFailure
        };

    private static bool ErrorResultEquivalent<TSuccess, TFailure>(
        ErrorResult<TSuccess, TFailure> left,
        ErrorResult<TSuccess, TFailure> right)
        where TSuccess : notnull
        where TFailure : notnull =>
        left.ToResult() switch
        {
            TSuccess leftSuccess => right.ToResult() is TSuccess rightSuccess
                && EqualityComparer<TSuccess>.Default.Equals(leftSuccess, rightSuccess),
            TFailure leftFailure => right.ToResult() is TFailure rightFailure
                && EqualityComparer<TFailure>.Default.Equals(leftFailure, rightFailure)
        };

    private static bool ValidationEquivalent<TSuccess>(
        Validation<TSuccess, string> left,
        Validation<TSuccess, string> right)
        where TSuccess : notnull =>
        left switch
        {
            TSuccess leftValue => right is TSuccess rightValue
                && EqualityComparer<TSuccess>.Default.Equals(leftValue, rightValue),
            NonEmptyList<string> leftErrors => right is NonEmptyList<string> rightErrors
                && leftErrors.SequenceEqual(rightErrors)
        };

    private static Func<Func<int, int>, Func<int, int>> ComposeContainer(
        Func<int, int> first) =>
        second => item => first(second(item));
}
