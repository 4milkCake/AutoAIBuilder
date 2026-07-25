namespace AutoAIBuilder.Application.Automation.Contracts;

public interface IAutomationContractSerializer
{
    AutomationRuleCatalog DeserializeRuleCatalog(string json);

    AutomationMaskDefinition DeserializeMask(string json);

    string Serialize(AutomationRuleCatalog catalog);

    string Serialize(AutomationMaskDefinition mask);
}
