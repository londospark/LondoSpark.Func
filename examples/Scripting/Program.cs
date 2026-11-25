using System;

using Examples.Scripting;

// The same script runs against the terminal, or, with --demo, against scripted answers.
if (args is ["--demo"])
{
    var replay = new ReplayHost(42, "50", "x", "25", "42");
    var guesses = Game.Guessing.Run(replay);
    Console.WriteLine(string.Join(Environment.NewLine, replay.Transcript));
    Console.WriteLine($"Finished in {guesses} guesses.");
    return;
}

Game.Guessing.Run(new ConsoleHost());
