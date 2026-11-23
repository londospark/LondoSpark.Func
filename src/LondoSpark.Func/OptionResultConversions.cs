namespace LondoSpark.Func;

public static class OptionResultConversions
{
    public static Result<TSome, Unit> ToResult<TSome>(this Option<TSome> option)
        where TSome : notnull =>
        option switch
        {
            TSome some => some,
            None => Unit.Value
        };

    public static Option<TSuccess> ToOption<TSuccess>(this Result<TSuccess, Unit> result)
        where TSuccess : notnull =>
        result switch
        {
            TSuccess success => success,
            Unit => new None()
        };
}
