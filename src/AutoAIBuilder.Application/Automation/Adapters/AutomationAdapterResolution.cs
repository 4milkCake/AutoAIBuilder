namespace AutoAIBuilder.Application.Automation.Adapters;

public sealed record AutomationAdapterResolution(
    AutomationAdapterResolutionStatus Status,
    AutomationAdapterDescriptor? Descriptor,
    IAutomationAdapter? Adapter,
    string Summary)
{
    public bool IsResolved =>
        Status == AutomationAdapterResolutionStatus.Ready
        && Descriptor is not null
        && Adapter is not null;
}
