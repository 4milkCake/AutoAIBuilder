namespace AutoAIBuilder.Application.Automation.Catalog;

public interface IAutomationMaskCatalogRepository
{
    IReadOnlyList<AutomationMaskCatalogEntry> GetAll();

    AutomationMaskCatalogEntry? Get(Guid id);

    AutomationMaskCatalogEntry? GetByIdentity(
        string maskId,
        string maskVersion);

    void Add(AutomationMaskCatalogEntry entry);

    AutomationMaskCatalogEntry SetActive(
        Guid id,
        bool isActive,
        DateTimeOffset updatedAt);
}
