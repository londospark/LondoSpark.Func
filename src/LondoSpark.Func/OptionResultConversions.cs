namespace LondoSpark.Func;

/// <summary>Moves between optional values and expected-success results when absence is the only failure information needed.</summary>
public static class OptionResultConversions
{
    extension<TSome>(Option<TSome> option)
    {
        /// <summary>Converts absence into a payload-free failure so optional data can enter a result-based pipeline.</summary>
        /// <returns>A success containing the value, or a failure containing <see cref="Unit.Value"/> when empty.</returns>
        public Result<TSome, Unit> ToResult() =>
            option switch
            {
                Some<TSome> some => Result.Return<TSome, Unit>(some.Value),
                None => Result.Failure<TSome, Unit>(Unit.Value)
            };
    }

    extension<TSuccess>(Result<TSuccess, Unit> result)
    {
        /// <summary>Converts a payload-free failure into absence so an option-based pipeline can continue without inventing an error value.</summary>
        /// <returns>An option containing the success value, or <see cref="None"/> when the result contains <see cref="Unit"/>.</returns>
        public Option<TSuccess> ToOption() =>
            result switch
            {
                Success<TSuccess> success => new Some<TSuccess>(success.Value),
                Failure<Unit> => new None()
            };
    }
}
