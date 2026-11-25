using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using LondoSpark.Func;

namespace Examples.Runbook;

/// <summary>Prints what the runbook would do and assumes every command succeeds and every step is approved.</summary>
public sealed class DryRun : Steps.IHandler
{
    public CommandResult Handle(Run instruction)
    {
        Console.WriteLine($"  would run   {instruction.Command}");
        return new CommandResult(0, string.Empty);
    }

    public Unit Handle(Note instruction)
    {
        Console.WriteLine($"  note        {instruction.Message}");
        return Unit.Value;
    }

    public bool Handle(Approve instruction)
    {
        Console.WriteLine($"  would ask   {instruction.Question} (assuming yes)");
        return true;
    }
}

/// <summary>Plays the runbook against a simulated machine, asynchronously, with canned command results.</summary>
/// <remarks>This is where a real interpreter would start processes; the runbook would not change.</remarks>
public sealed class Simulated(IReadOnlyDictionary<string, CommandResult> machine, bool approves) : Steps.IAsyncHandler
{
    public async ValueTask<CommandResult> HandleAsync(Run instruction, CancellationToken cancellationToken)
    {
        await Task.Delay(30, cancellationToken);
        var result = machine.TryGetValue(instruction.Command, out var known) ? known : new CommandResult(0, string.Empty);
        Console.WriteLine($"  ran         {instruction.Command} -> exit {result.ExitCode}");
        return result;
    }

    public ValueTask<Unit> HandleAsync(Note instruction, CancellationToken cancellationToken)
    {
        Console.WriteLine($"  note        {instruction.Message}");
        return ValueTask.FromResult(Unit.Value);
    }

    public ValueTask<bool> HandleAsync(Approve instruction, CancellationToken cancellationToken)
    {
        Console.WriteLine($"  asked       {instruction.Question} -> {(approves ? "yes" : "no")}");
        return ValueTask.FromResult(approves);
    }
}
