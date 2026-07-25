using AutoAIBuilder.Application.Automation.Contracts;
using AutoAIBuilder.Application.Automation.Validation;

namespace AutoAIBuilder.Application.Automation.Catalog;

public sealed record AutomationMaskPackagePreview(
    string MaskSourceFileName,
    string RuleCatalogSourceFileName,
    AutomationMaskDefinition? Mask,
    AutomationRuleCatalog? RuleCatalog,
    string MaskJson,
    string RuleCatalogJson,
    string ContentSha256,
    AutomationMaskCatalogConflict Conflict,
    IReadOnlyList<AutomationValidationIssue> Issues)
{
    public bool IsValid =>
        Mask is not null
        && RuleCatalog is not null
        && Issues.All(
            issue => issue.Severity != AutomationValidationSeverity.Error);

    public bool CanImport =>
        IsValid && Conflict == AutomationMaskCatalogConflict.None;
}
