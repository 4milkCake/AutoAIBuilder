namespace AutoAIBuilder.Application.Automation.Catalog;

public interface IAutomationMaskCatalogService
{
    Task<AutomationMaskPackagePreview> AnalyzeAsync(
        string maskFilePath,
        string ruleCatalogFilePath,
        CancellationToken cancellationToken = default);

    AutomationMaskImportResult Import(
        AutomationMaskPackagePreview preview);

    IReadOnlyList<AutomationMaskCatalogEntry> GetAll();

    AutomationMaskCatalogEntry SetActive(Guid id, bool isActive);
}
