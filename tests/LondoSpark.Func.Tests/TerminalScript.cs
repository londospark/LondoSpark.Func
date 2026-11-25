namespace LondoSpark.Func.Tests.Scripting;

// A small instruction set the program tests run against: each case names the type its interpreter answers with.
public sealed record Ask(string Prompt) : IInstruction<string>;

public sealed record Tell(string Message) : IInstruction<Unit>;

public sealed record Roll(int Sides = 6) : IInstruction<int>;

public union ConsoleOp(Ask, Tell, Roll);

[Instructions<ConsoleOp>]
public static partial class Terminal;
