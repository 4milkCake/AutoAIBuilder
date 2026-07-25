namespace AutoAIBuilder.Application.Automation.Adapters;

public sealed record AutomationAdapterDescriptor(
    string AdapterId,
    string AdapterVersion,
    string DisplayName,
    string Description,
    string Provider,
    string MaskId,
    string MaskVersion,
    string ContractSha256,
    bool SupportsSimulation,
    bool SupportsApply,
    bool IsEnabled,
    bool CatalogExecutionEnabled,
    AutomationAdapterOrigin Origin);
