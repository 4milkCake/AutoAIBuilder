namespace AutoAIBuilder.Application.Automation.Execution;

public sealed record AutomationAdapterResult(
    bool Succeeded,
    string Summary,
    IReadOnlyList<string> OutputPaths,
    IReadOnlyList<string> Diagnostics)
{
    public static AutomationAdapterResult Success(
        string summary,
        IReadOnlyList<string>? outputPaths = null,
        IReadOnlyList<string>? diagnostics = null) =>
        new(true, summary, outputPaths ?? [], diagnostics ?? []);

    public static AutomationAdapterResult Failure(
        string summary,
        IReadOnlyList<string>? diagnostics = null) =>
        new(false, summary, [], diagnostics ?? []);
}
