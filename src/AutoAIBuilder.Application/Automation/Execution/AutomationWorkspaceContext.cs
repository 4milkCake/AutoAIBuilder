namespace AutoAIBuilder.Application.Automation.Execution;

public sealed record AutomationWorkspaceContext(
    Guid ExecutionId,
    string WorkspaceRoot,
    string ResultDirectory,
    IReadOnlyList<string> InputCopies,
    IReadOnlyDictionary<string, string> Parameters);
