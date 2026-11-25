using System;
using System.Threading.Tasks;

namespace LondoSpark.Func;

/// <summary>Query syntax over asynchronous options, so <c>from</c> clauses can mix <see cref="Task{TResult}"/>, <see cref="ValueTask{TResult}"/>, and plain options.</summary>
/// <remarks>
/// <para>
/// Each query produces a <see cref="ValueTask{TResult}"/> of an option and stops at the first absent value without
/// running later steps:
/// <code>
/// var order = await (from customer in customers.FindAsync(id)
///                    from basket in baskets.FindAsync(customer.Id)
///                    from discount in discounts.For(customer)
///                    select basket.Total - discount);
/// </code>
/// </para>
/// <para>
/// A query whose steps all complete synchronously does not allocate beyond its own lambdas; the asynchronous state
/// machine moves to the heap only when a step really suspends.
/// </para>
/// </remarks>
public static class OptionAsyncQueries
{
    /// <summary>Transforms the eventual present value, leaving absence unchanged.</summary>
    /// <typeparam name="TSome">The type of the first present value.</typeparam>
    /// <typeparam name="TOut">The type of the transformed value.</typeparam>
    /// <param name="source">The asynchronous option to continue from.</param>
    /// <param name="selector">The transformation applied only when a value is present.</param>
    /// <returns>The transformed option once <paramref name="source"/> completes.</returns>
    public static async ValueTask<Option<TOut>> Select<TSome, TOut>(
        this ValueTask<Option<TSome>> source,
        Func<TSome, TOut> selector) =>
        (await source.ConfigureAwait(false)).Select(selector);

    /// <summary>Continues with an asynchronous optional step when the first value is present.</summary>
    /// <typeparam name="TSome">The type of the first present value.</typeparam>
    /// <typeparam name="TNext">The type of the second value.</typeparam>
    /// <typeparam name="TResult">The type of the projected result.</typeparam>
    /// <param name="source">The asynchronous option to continue from.</param>
    /// <param name="next">Produces the second option from the first value.</param>
    /// <param name="project">Combines both values.</param>
    /// <returns>The projected value, or <see cref="None"/> when either step is absent.</returns>
    public static async ValueTask<Option<TResult>> SelectMany<TSome, TNext, TResult>(
        this ValueTask<Option<TSome>> source,
        Func<TSome, ValueTask<Option<TNext>>> next,
        Func<TSome, TNext, TResult> project) =>
        await source.ConfigureAwait(false) is Option<TSome>.Some(var first)
            ? Project(first, await next(first).ConfigureAwait(false), project)
            : new None();

    /// <summary>Continues with a task-returning optional step when the first value is present.</summary>
    /// <typeparam name="TSome">The type of the first present value.</typeparam>
    /// <typeparam name="TNext">The type of the second value.</typeparam>
    /// <typeparam name="TResult">The type of the projected result.</typeparam>
    /// <param name="source">The asynchronous option to continue from.</param>
    /// <param name="next">Produces the second option from the first value.</param>
    /// <param name="project">Combines both values.</param>
    /// <returns>The projected value, or <see cref="None"/> when either step is absent.</returns>
    public static async ValueTask<Option<TResult>> SelectMany<TSome, TNext, TResult>(
        this ValueTask<Option<TSome>> source,
        Func<TSome, Task<Option<TNext>>> next,
        Func<TSome, TNext, TResult> project) =>
        await source.ConfigureAwait(false) is Option<TSome>.Some(var first)
            ? Project(first, await next(first).ConfigureAwait(false), project)
            : new None();

    /// <summary>Continues with a synchronous optional step when the first value is present.</summary>
    /// <typeparam name="TSome">The type of the first present value.</typeparam>
    /// <typeparam name="TNext">The type of the second value.</typeparam>
    /// <typeparam name="TResult">The type of the projected result.</typeparam>
    /// <param name="source">The asynchronous option to continue from.</param>
    /// <param name="next">Produces the second option from the first value.</param>
    /// <param name="project">Combines both values.</param>
    /// <returns>The projected value, or <see cref="None"/> when either step is absent.</returns>
    public static async ValueTask<Option<TResult>> SelectMany<TSome, TNext, TResult>(
        this ValueTask<Option<TSome>> source,
        Func<TSome, Option<TNext>> next,
        Func<TSome, TNext, TResult> project) =>
        await source.ConfigureAwait(false) is Option<TSome>.Some(var first)
            ? Project(first, next(first), project)
            : new None();


    extension<TSome>(Task<Option<TSome>> source)
    {
        /// <summary>Transforms the eventual present value, leaving absence unchanged.</summary>
        /// <typeparam name="TOut">The type of the transformed value.</typeparam>
        /// <param name="selector">The transformation applied only when a value is present.</param>
        /// <returns>The transformed option once <paramref name="source"/> completes.</returns>
        public async ValueTask<Option<TOut>> Select<TOut>(Func<TSome, TOut> selector) =>
            (await source.ConfigureAwait(false)).Select(selector);

        /// <summary>Continues with an asynchronous optional step when the first value is present.</summary>
        /// <typeparam name="TNext">The type of the second value.</typeparam>
        /// <typeparam name="TResult">The type of the projected result.</typeparam>
        /// <param name="next">Produces the second option from the first value.</param>
        /// <param name="project">Combines both values.</param>
        /// <returns>The projected value, or <see cref="None"/> when either step is absent.</returns>
        public async ValueTask<Option<TResult>> SelectMany<TNext, TResult>(
            Func<TSome, ValueTask<Option<TNext>>> next,
            Func<TSome, TNext, TResult> project) =>
            await source.ConfigureAwait(false) is Option<TSome>.Some(var first)
                ? Project(first, await next(first).ConfigureAwait(false), project)
                : new None();

        /// <summary>Continues with a task-returning optional step when the first value is present.</summary>
        /// <typeparam name="TNext">The type of the second value.</typeparam>
        /// <typeparam name="TResult">The type of the projected result.</typeparam>
        /// <param name="next">Produces the second option from the first value.</param>
        /// <param name="project">Combines both values.</param>
        /// <returns>The projected value, or <see cref="None"/> when either step is absent.</returns>
        public async ValueTask<Option<TResult>> SelectMany<TNext, TResult>(
            Func<TSome, Task<Option<TNext>>> next,
            Func<TSome, TNext, TResult> project) =>
            await source.ConfigureAwait(false) is Option<TSome>.Some(var first)
                ? Project(first, await next(first).ConfigureAwait(false), project)
                : new None();

        /// <summary>Continues with a synchronous optional step when the first value is present.</summary>
        /// <typeparam name="TNext">The type of the second value.</typeparam>
        /// <typeparam name="TResult">The type of the projected result.</typeparam>
        /// <param name="next">Produces the second option from the first value.</param>
        /// <param name="project">Combines both values.</param>
        /// <returns>The projected value, or <see cref="None"/> when either step is absent.</returns>
        public async ValueTask<Option<TResult>> SelectMany<TNext, TResult>(
            Func<TSome, Option<TNext>> next,
            Func<TSome, TNext, TResult> project) =>
            await source.ConfigureAwait(false) is Option<TSome>.Some(var first)
                ? Project(first, next(first), project)
                : new None();
    }

    extension<TSome>(Option<TSome> source)
    {
        /// <summary>Continues a plain option with an asynchronous optional step, so a query can start synchronously.</summary>
        /// <typeparam name="TNext">The type of the second value.</typeparam>
        /// <typeparam name="TResult">The type of the projected result.</typeparam>
        /// <param name="next">Produces the second option from the first value.</param>
        /// <param name="project">Combines both values.</param>
        /// <returns>The projected value, or <see cref="None"/> when either step is absent.</returns>
        public async ValueTask<Option<TResult>> SelectMany<TNext, TResult>(
            Func<TSome, ValueTask<Option<TNext>>> next,
            Func<TSome, TNext, TResult> project) =>
            source is Option<TSome>.Some(var first)
                ? Project(first, await next(first).ConfigureAwait(false), project)
                : new None();

        /// <summary>Continues a plain option with a task-returning optional step, so a query can start synchronously.</summary>
        /// <typeparam name="TNext">The type of the second value.</typeparam>
        /// <typeparam name="TResult">The type of the projected result.</typeparam>
        /// <param name="next">Produces the second option from the first value.</param>
        /// <param name="project">Combines both values.</param>
        /// <returns>The projected value, or <see cref="None"/> when either step is absent.</returns>
        public async ValueTask<Option<TResult>> SelectMany<TNext, TResult>(
            Func<TSome, Task<Option<TNext>>> next,
            Func<TSome, TNext, TResult> project) =>
            source is Option<TSome>.Some(var first)
                ? Project(first, await next(first).ConfigureAwait(false), project)
                : new None();
    }

    private static Option<TResult> Project<TSome, TNext, TResult>(
        TSome first,
        Option<TNext> second,
        Func<TSome, TNext, TResult> project) =>
        second switch
        {
            Option<TNext>.Some(var value) => new Option<TResult>.Some(project(first, value)),
            None none => none
        };
}
