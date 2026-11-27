using System;
using System.Collections.Generic;

using Examples.Runbook;

using LondoSpark.Func;

using static LondoSpark.Func.Program<Examples.Runbook.RunbookOp, LondoSpark.Func.Result<string, string>>;
using static LondoSpark.Func.Result<string, string>;

// One runbook, three ways to run it. Nothing in the runbook changes between them.
var release = Steps.Release("1.4.0");

Console.WriteLine("Dry run: print every step, assume success.");
Report(release.Value.Run(new DryRun()));

Console.WriteLine();
Console.WriteLine("Simulation: a machine whose tests fail.");
var failingTests = new Dictionary<string, CommandResult>
{
    ["git status --porcelain"] = new(0, string.Empty),
    ["dotnet test --configuration Release"] = new(1, "2 tests failed"),
};
Report(await release.Value.RunAsync(new Simulated(failingTests, approves: true)));

Console.WriteLine();
Console.WriteLine("What if: step to the approval, then resume the same program both ways.");
var program = release.Value;
while (program is Pending(var instruction and not Approve, var next))
{
    // Answer everything before the approval by hand: a program is a union you can step through.
    program = instruction switch
    {
        Run => next.Resume(new CommandResult(0, string.Empty)),
        Note => next.Resume(Unit.Value),
        Approve => program // unreachable: the loop stops at the approval
    };
}

if (program is Pending(Approve(var question), var decide))
{
    Console.WriteLine($"  paused at   {question}");
    Console.WriteLine("  if yes:");
    Report(decide.Resume(true).Run(new DryRun()));
    Console.WriteLine("  if no:");
    Report(decide.Resume(false).Run(new DryRun()));
}

static void Report(Result<string, string> outcome) =>
    Console.WriteLine(outcome switch
    {
        Success(var tag) => $"  => released {tag}",
        Failure(var reason) => $"  => stopped: {reason}"
    });
