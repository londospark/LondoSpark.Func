using System.Globalization;

using LondoSpark.Func;

namespace Examples.Scripting;

// The instruction set: what a game script may ask its host to do, and what each request answers with.
public sealed record Say(string Text) : IInstruction<Unit>;

public sealed record Ask(string Prompt) : IInstruction<string>;

public sealed record Pick(int Max) : IInstruction<int>;

public union GameOp(Say, Ask, Pick);

// The generator writes say, ask, pick, and pure here, plus Game.IHandler and Run.
[Instructions<GameOp>]
public static partial class Game
{
    /// <summary>A guessing game, written once and run by any interpreter; it finishes with the number of guesses.</summary>
    public static Program<GameOp, int> Guessing =>
        from secret in pick(100)
        from _ in say("I'm thinking of a number from 1 to 100.")
        from guesses in Guess(secret, attempt: 1)
        select guesses;

    // A loop is a recursive program; it runs in constant stack space however many guesses it takes.
    private static Program<GameOp, int> Guess(int secret, int attempt) =>
        from text in ask($"Guess #{attempt}:")
        from guesses in int.TryParse(text, CultureInfo.InvariantCulture, out var guess)
            ? guess == secret
                ? from _ in say($"Yes! {secret} in {attempt}.") select attempt
                : from _ in say(guess < secret ? "Higher." : "Lower.") from more in Guess(secret, attempt + 1) select more
            : from _ in say($"'{text}' is not a number.") from more in Guess(secret, attempt) select more
        select guesses;
}
