namespace LondoSpark.Func;

/// <summary>Converts between <see cref="Option{TSome}"/> and <see cref="Result{TSuccess, TFailure}"/> using <see cref="Unit"/> as the failure type.</summary>
public static class OptionResultConversions
{
    /// <summary>Converts an option to a result, using <see cref="Unit"/> to represent absence.</summary>
    /// <typeparam name="TSome">The type of the present value.</typeparam>
    /// <param name="option">The option to convert.</param>
    /// <returns>A successful result containing the value, or a failed result containing <see cref="Unit.Value"/> when the option is empty.</returns>
    public static Result<TSome, Unit> ToResult<TSome>(this Option<TSome> option)
        where TSome : notnull =>
        option switch
        {
            TSome some => some,
            None => Unit.Value
        };

    /// <summary>Converts a result with <see cref="Unit"/> failures to an option.</summary>
    /// <typeparam name="TSuccess">The type of the successful value.</typeparam>
    /// <param name="result">The result to convert.</param>
    /// <returns>An option containing the success value, or <see cref="None"/> when the result contains <see cref="Unit"/>.</returns>
    public static Option<TSuccess> ToOption<TSuccess>(this Result<TSuccess, Unit> result)
        where TSuccess : notnull =>
        result switch
        {
            TSuccess success => success,
            Unit => new None()
        };
}
