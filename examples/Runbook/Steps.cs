using LondoSpark.Func;

namespace Examples.Runbook;

/// <summary>A runbook step that can fail: a program over <see cref="RunbookOp"/> composed with a result.</summary>
/// <remarks>
/// The generator makes the pair one monad, so a query over it runs instructions in order and stops at the first
/// failure without the steps checking for one.
/// </remarks>
[MonadTransformer]
public readonly partial record struct Script<T>(Program<RunbookOp, Result<T, string>> Value);

// The generator writes run, note, approve, and pure here, plus Steps.IHandler, Steps.IAsyncHandler, Run, and RunAsync.
[Instructions<RunbookOp>]
public static partial class Steps
{
    /// <summary>Releases a version: refuse a dirty tree or failing tests, ask before tagging, then tag and push.</summary>
    /// <remarks>
    /// The runbook says what to do, never how: it does not know whether commands really run, are printed, or are
    /// answered by a test. It finishes with the tag it pushed, or the reason it stopped.
    /// </remarks>
    public static Script<string> Release(string version)
    {
        var tag = $"v{version}";
        return from status in run("git status --porcelain")
               from clean in Require(status.Output.Length == 0, "the working tree has uncommitted changes")
               from tests in run("dotnet test --configuration Release")
               from passed in Require(tests.Succeeded, $"tests failed: {tests.Output}")
               from approved in approve($"Tag and push {tag}?")
               from yes in Require(approved, "the operator said no")
               from tagged in run($"git tag -a {tag} -m \"Release {tag}\"")
               from pushed in run($"git push origin {tag}")
               from published in Require(pushed.Succeeded, $"push failed: {pushed.Output}")
               from _ in note($"Released {tag}.")
               select tag;
    }

    // Carries on when the condition holds; otherwise tells the operator why and fails, which skips every later step.
    private static Script<Unit> Require(bool condition, string reason) =>
        condition
            ? Script<Unit>.Return(Unit.Value)
            : from _ in note($"Stopped: {reason}.")
              from stopped in Script<Unit>.Failure(reason)
              select stopped;
}
