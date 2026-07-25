namespace AutoAIBuilder.Application.Automation.Orchestration;

public sealed record AutomationIntegrationAssessment(
    Guid Id,
    Guid ProjectId,
    Guid CatalogEntryId,
    string MaskId,
    string MaskVersion,
    string ContractSha256,
    AutomationIntegrationStatus Status,
    string? AdapterId,
    string? AdapterVersion,
    string Summary,
    DateTimeOffset EvaluatedAt)
{
    public bool IsReady => Status == AutomationIntegrationStatus.Ready;
}
