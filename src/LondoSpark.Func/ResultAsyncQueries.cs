using System;
using System.Threading.Tasks;

namespace LondoSpark.Func;

/// <summary>Query syntax over asynchronous results, so <c>from</c> clauses can mix <see cref="Task{TResult}"/>, <see cref="ValueTask{TResult}"/>, and plain results.</summary>
/// <remarks>
/// <para>
/// Each query produces a <see cref="ValueTask{TResult}"/> of a result and stops at the first failure without running
/// later steps:
/// <code>
/// var receipt = await (from candidate in Validate(input)             // Result
///                      from customer in directory.RegisterAsync(candidate) // Task&lt;Result&gt;
///                      from email in mailer.WelcomeAsync(customer)     // ValueTask&lt;Result&gt;
///                      select new Receipt(customer.Id, email.Id));
/// </code>
/// Every step shares one failure type; adapt a step's errors with <c>MapError</c> first.
/// </para>
/// <para>
/// A query whose steps all complete synchronously does not allocate beyond its own lambdas; the asynchronous state
/// machine moves to the heap only when a step really suspends.
/// </para>
/// </remarks>
public static class ResultAsyncQueries
{
    /// <summary>Transforms the eventual success, carrying a failure unchanged.</summary>
    /// <typeparam name="TSuccess">The success type of the first step.</typeparam>
    /// <typeparam name="TFailure">The failure type every step shares.</typeparam>
    /// <typeparam name="TOut">The transformed success type.</typeparam>
    /// <param name="source">The asynchronous result to continue from.</param>
    /// <param name="selector">The transformation applied only to a success.</param>
    /// <returns>The transformed result once <paramref name="source"/> completes.</returns>
    public static async ValueTask<Result<TOut, TFailure>> Select<TSuccess, TFailure, TOut>(
        this ValueTask<Result<TSuccess, TFailure>> source,
        Func<TSuccess, TOut> selector) =>
        (await source.ConfigureAwait(false)).Select(selector);

    /// <summary>Continues with an asynchronous fallible step after a success.</summary>
    /// <typeparam name="TSuccess">The success type of the first step.</typeparam>
    /// <typeparam name="TFailure">The failure type every step shares.</typeparam>
    /// <typeparam name="TNext">The success type of the second step.</typeparam>
    /// <typeparam name="TResult">The projected success type.</typeparam>
    /// <param name="source">The asynchronous result to continue from.</param>
    /// <param name="next">Produces the second result from the first success.</param>
    /// <param name="project">Combines both successes.</param>
    /// <returns>The projected success, or the first failure.</returns>
    public static async ValueTask<Result<TResult, TFailure>> SelectMany<TSuccess, TFailure, TNext, TResult>(
        this ValueTask<Result<TSuccess, TFailure>> source,
        Func<TSuccess, ValueTask<Result<TNext, TFailure>>> next,
        Func<TSuccess, TNext, TResult> project) =>
        await source.ConfigureAwait(false) switch
        {
            Result<TSuccess, TFailure>.Success(var first) => Project(first, await next(first).ConfigureAwait(false), project),
            Result<TSuccess, TFailure>.Failure(var failure) => new Result<TResult, TFailure>.Failure(failure),
            null => throw NoCase.Result()
        };

    /// <summary>Continues with a task-returning fallible step after a success.</summary>
    /// <typeparam name="TSuccess">The success type of the first step.</typeparam>
    /// <typeparam name="TFailure">The failure type every step shares.</typeparam>
    /// <typeparam name="TNext">The success type of the second step.</typeparam>
    /// <typeparam name="TResult">The projected success type.</typeparam>
    /// <param name="source">The asynchronous result to continue from.</param>
    /// <param name="next">Produces the second result from the first success.</param>
    /// <param name="project">Combines both successes.</param>
    /// <returns>The projected success, or the first failure.</returns>
    public static async ValueTask<Result<TResult, TFailure>> SelectMany<TSuccess, TFailure, TNext, TResult>(
        this ValueTask<Result<TSuccess, TFailure>> source,
        Func<TSuccess, Task<Result<TNext, TFailure>>> next,
        Func<TSuccess, TNext, TResult> project) =>
        await source.ConfigureAwait(false) switch
        {
            Result<TSuccess, TFailure>.Success(var first) => Project(first, await next(first).ConfigureAwait(false), project),
            Result<TSuccess, TFailure>.Failure(var failure) => new Result<TResult, TFailure>.Failure(failure),
            null => throw NoCase.Result()
        };

    /// <summary>Continues with a synchronous fallible step after a success.</summary>
    /// <typeparam name="TSuccess">The success type of the first step.</typeparam>
    /// <typeparam name="TFailure">The failure type every step shares.</typeparam>
    /// <typeparam name="TNext">The success type of the second step.</typeparam>
    /// <typeparam name="TResult">The projected success type.</typeparam>
    /// <param name="source">The asynchronous result to continue from.</param>
    /// <param name="next">Produces the second result from the first success.</param>
    /// <param name="project">Combines both successes.</param>
    /// <returns>The projected success, or the first failure.</returns>
    public static async ValueTask<Result<TResult, TFailure>> SelectMany<TSuccess, TFailure, TNext, TResult>(
        this ValueTask<Result<TSuccess, TFailure>> source,
        Func<TSuccess, Result<TNext, TFailure>> next,
        Func<TSuccess, TNext, TResult> project) =>
        (await source.ConfigureAwait(false)).SelectMany(next, project);


    extension<TSuccess, TFailure>(Task<Result<TSuccess, TFailure>> source)
    {
        /// <summary>Transforms the eventual success, carrying a failure unchanged.</summary>
        /// <typeparam name="TOut">The transformed success type.</typeparam>
        /// <param name="selector">The transformation applied only to a success.</param>
        /// <returns>The transformed result once <paramref name="source"/> completes.</returns>
        public async ValueTask<Result<TOut, TFailure>> Select<TOut>(Func<TSuccess, TOut> selector) =>
            (await source.ConfigureAwait(false)).Select(selector);

        /// <summary>Continues with an asynchronous fallible step after a success.</summary>
        /// <typeparam name="TNext">The success type of the second step.</typeparam>
        /// <typeparam name="TResult">The projected success type.</typeparam>
        /// <param name="next">Produces the second result from the first success.</param>
        /// <param name="project">Combines both successes.</param>
        /// <returns>The projected success, or the first failure.</returns>
        public async ValueTask<Result<TResult, TFailure>> SelectMany<TNext, TResult>(
            Func<TSuccess, ValueTask<Result<TNext, TFailure>>> next,
            Func<TSuccess, TNext, TResult> project) =>
            await source.ConfigureAwait(false) switch
            {
                Result<TSuccess, TFailure>.Success(var first) => Project(first, await next(first).ConfigureAwait(false), project),
                Result<TSuccess, TFailure>.Failure(var failure) => new Result<TResult, TFailure>.Failure(failure),
                null => throw NoCase.Result()
            };

        /// <summary>Continues with a task-returning fallible step after a success.</summary>
        /// <typeparam name="TNext">The success type of the second step.</typeparam>
        /// <typeparam name="TResult">The projected success type.</typeparam>
        /// <param name="next">Produces the second result from the first success.</param>
        /// <param name="project">Combines both successes.</param>
        /// <returns>The projected success, or the first failure.</returns>
        public async ValueTask<Result<TResult, TFailure>> SelectMany<TNext, TResult>(
            Func<TSuccess, Task<Result<TNext, TFailure>>> next,
            Func<TSuccess, TNext, TResult> project) =>
            await source.ConfigureAwait(false) switch
            {
                Result<TSuccess, TFailure>.Success(var first) => Project(first, await next(first).ConfigureAwait(false), project),
                Result<TSuccess, TFailure>.Failure(var failure) => new Result<TResult, TFailure>.Failure(failure),
                null => throw NoCase.Result()
            };

        /// <summary>Continues with a synchronous fallible step after a success.</summary>
        /// <typeparam name="TNext">The success type of the second step.</typeparam>
        /// <typeparam name="TResult">The projected success type.</typeparam>
        /// <param name="next">Produces the second result from the first success.</param>
        /// <param name="project">Combines both successes.</param>
        /// <returns>The projected success, or the first failure.</returns>
        public async ValueTask<Result<TResult, TFailure>> SelectMany<TNext, TResult>(
            Func<TSuccess, Result<TNext, TFailure>> next,
            Func<TSuccess, TNext, TResult> project) =>
            (await source.ConfigureAwait(false)).SelectMany(next, project);
    }

    extension<TSuccess, TFailure>(Result<TSuccess, TFailure> source)
    {
        /// <summary>Continues a plain result with an asynchronous fallible step, so a query can start synchronously.</summary>
        /// <typeparam name="TNext">The success type of the second step.</typeparam>
        /// <typeparam name="TResult">The projected success type.</typeparam>
        /// <param name="next">Produces the second result from the first success.</param>
        /// <param name="project">Combines both successes.</param>
        /// <returns>The projected success, or the first failure.</returns>
        public async ValueTask<Result<TResult, TFailure>> SelectMany<TNext, TResult>(
            Func<TSuccess, ValueTask<Result<TNext, TFailure>>> next,
            Func<TSuccess, TNext, TResult> project) =>
            source switch
            {
                Result<TSuccess, TFailure>.Success(var first) => Project(first, await next(first).ConfigureAwait(false), project),
                Result<TSuccess, TFailure>.Failure(var failure) => new Result<TResult, TFailure>.Failure(failure),
                null => throw NoCase.Result()
            };

        /// <summary>Continues a plain result with a task-returning fallible step, so a query can start synchronously.</summary>
        /// <typeparam name="TNext">The success type of the second step.</typeparam>
        /// <typeparam name="TResult">The projected success type.</typeparam>
        /// <param name="next">Produces the second result from the first success.</param>
        /// <param name="project">Combines both successes.</param>
        /// <returns>The projected success, or the first failure.</returns>
        public async ValueTask<Result<TResult, TFailure>> SelectMany<TNext, TResult>(
            Func<TSuccess, Task<Result<TNext, TFailure>>> next,
            Func<TSuccess, TNext, TResult> project) =>
            source switch
            {
                Result<TSuccess, TFailure>.Success(var first) => Project(first, await next(first).ConfigureAwait(false), project),
                Result<TSuccess, TFailure>.Failure(var failure) => new Result<TResult, TFailure>.Failure(failure),
                null => throw NoCase.Result()
            };
    }

    private static Result<TResult, TFailure> Project<TSuccess, TNext, TResult, TFailure>(
        TSuccess first,
        Result<TNext, TFailure> second,
        Func<TSuccess, TNext, TResult> project) =>
        second switch
        {
            Result<TNext, TFailure>.Success(var value) => new Result<TResult, TFailure>.Success(project(first, value)),
            Result<TNext, TFailure>.Failure(var failure) => new Result<TResult, TFailure>.Failure(failure),
            null => throw NoCase.Result()
        };
}
