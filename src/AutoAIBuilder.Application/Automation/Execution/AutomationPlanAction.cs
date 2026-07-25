namespace AutoAIBuilder.Application.Automation.Execution;

public sealed record AutomationPlanAction(
    int Order,
    string Code,
    string Description,
    bool WritesDuringApply);
