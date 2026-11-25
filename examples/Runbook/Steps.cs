using LondoSpark.Func;

namespace Examples.Runbook;

// The generator writes run, note, approve, and pure here, plus Steps.IHandler, Steps.IAsyncHandler, Run, and RunAsync.
[Instructions<RunbookOp>]
public static partial class Steps
{
    /// <summary>Releases a version: refuse a dirty tree or failing tests, ask before tagging, then tag and push.</summary>
    /// <remarks>
    /// The runbook says what to do, never how: it does not know whether commands really run, are printed, or are
    /// answered by a test. It finishes with the tag it pushed, or the reason it stopped.
    /// </remarks>
    public static Program<RunbookOp, Result<string, string>> Release(string version) =>
        from status in run("git status --porcelain")
        from outcome in status.Output.Length > 0
            ? Stop("the working tree has uncommitted changes")
            : TestThenTag($"v{version}")
        select outcome;

    private static Program<RunbookOp, Result<string, string>> TestThenTag(string tag) =>
        from tests in run("dotnet test --configuration Release")
        from outcome in tests.Succeeded
            ? AskThenTag(tag)
            : Stop($"tests failed: {tests.Output}")
        select outcome;

    private static Program<RunbookOp, Result<string, string>> AskThenTag(string tag) =>
        from approved in approve($"Tag and push {tag}?")
        from outcome in approved
            ? TagAndPush(tag)
            : Stop("the operator said no")
        select outcome;

    private static Program<RunbookOp, Result<string, string>> TagAndPush(string tag) =>
        from tagged in run($"git tag -a {tag} -m \"Release {tag}\"")
        from pushed in run($"git push origin {tag}")
        from outcome in pushed.Succeeded
            ? from _ in note($"Released {tag}.") select Result.Return<string, string>(tag)
            : Stop($"push failed: {pushed.Output}")
        select outcome;

    private static Program<RunbookOp, Result<string, string>> Stop(string reason) =>
        from _ in note($"Stopped: {reason}.")
        select Result.Failure<string, string>(reason);
}
