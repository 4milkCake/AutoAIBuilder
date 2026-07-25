namespace AutoAIBuilder.Application.Automation.Catalog;

public enum AutomationMaskImportStatus
{
    Imported,
    AlreadyImported,
    ContentConflict,
    Rejected
}

public sealed record AutomationMaskImportResult(
    AutomationMaskImportStatus Status,
    AutomationMaskCatalogEntry? Entry,
    string Summary)
{
    public bool Succeeded => Status == AutomationMaskImportStatus.Imported;
}
