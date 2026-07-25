namespace AutoAIBuilder.Application.Automation.Contracts;

public sealed record AutomationRuleCatalog(
    string SchemaVersion,
    string CatalogId,
    string Version,
    IReadOnlyList<AutomationRuleDefinition> Rules);
