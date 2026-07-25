namespace AutoAIBuilder.Application.Automation.Adapters;

public interface IAutomationAdapterRegistry
{
    IReadOnlyList<AutomationAdapterDescriptor> GetAll();

    AutomationAdapterResolution Resolve(
        string maskId,
        string maskVersion,
        string contractSha256);
}
