namespace AutoAIBuilder.Domain.Automation;

public sealed record WorkflowStep(
    int Order,
    string Name,
    WorkflowState State);
