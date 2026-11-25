using System;
using System.Collections.Generic;

using LondoSpark.Func;

namespace Examples.Scripting;

/// <summary>Plays the game on the terminal.</summary>
public sealed class ConsoleHost : Game.IHandler
{
    public Unit Handle(Say instruction)
    {
        Console.WriteLine(instruction.Text);
        return Unit.Value;
    }

    public string Handle(Ask instruction)
    {
        Console.Write(instruction.Prompt + " ");
        return Console.ReadLine() ?? string.Empty;
    }

    public int Handle(Pick instruction) =>
        Random.Shared.Next(1, instruction.Max + 1);
}

/// <summary>Plays the game from a script of answers and a fixed secret, recording a transcript: a test double, no mocks needed.</summary>
public sealed class ReplayHost(int secret, params string[] answers) : Game.IHandler
{
    private readonly Queue<string> _answers = new(answers);

    public List<string> Transcript { get; } = [];

    public Unit Handle(Say instruction)
    {
        Transcript.Add(instruction.Text);
        return Unit.Value;
    }

    public string Handle(Ask instruction)
    {
        var answer = _answers.Dequeue();
        Transcript.Add($"{instruction.Prompt} {answer}");
        return answer;
    }

    public int Handle(Pick instruction) => secret;
}
