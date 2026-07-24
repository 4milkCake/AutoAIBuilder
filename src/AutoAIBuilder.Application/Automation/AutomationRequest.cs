namespace AutoAIBuilder.Application.Automation;

public sealed record AutomationRequest(
    Guid ProjectId,
    string WorkflowId,
    IReadOnlyDictionary<string, string> Parameters)
{
    public static AutomationRequest Create(
        Guid projectId,
        string workflowId,
        IReadOnlyDictionary<string, string>? parameters = null)
    {
        if (projectId == Guid.Empty)
        {
            throw new ArgumentException(
                "O identificador do projeto é obrigatório.",
                nameof(projectId));
        }

        if (string.IsNullOrWhiteSpace(workflowId))
        {
            throw new ArgumentException(
                "O identificador do fluxo é obrigatório.",
                nameof(workflowId));
        }

        return new AutomationRequest(
            projectId,
            workflowId.Trim(),
            parameters ?? new Dictionary<string, string>());
    }
}
