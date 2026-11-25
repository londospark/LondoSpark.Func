using LondoSpark.Func;

namespace Examples.Runbook;

/// <summary>What a shell command reported.</summary>
public sealed record CommandResult(int ExitCode, string Output)
{
    public bool Succeeded => ExitCode == 0;
}

/// <summary>Runs a shell command.</summary>
public sealed record Run(string Command) : IInstruction<CommandResult>;

/// <summary>Tells the operator something.</summary>
public sealed record Note(string Message) : IInstruction<Unit>;

/// <summary>Asks the operator to approve the next step.</summary>
public sealed record Approve(string Question) : IInstruction<bool>;

/// <summary>Everything a runbook may ask of whatever runs it.</summary>
public union RunbookOp(Run, Note, Approve);
