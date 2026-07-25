using System.Text.RegularExpressions;
using AutoAIBuilder.Application.Automation.Contracts;

namespace AutoAIBuilder.Application.Automation.Validation;

public sealed partial class AutomationContractValidator
{
    private static readonly StringComparer IdentifierComparer =
        StringComparer.OrdinalIgnoreCase;

    public AutomationValidationResult Validate(
        AutomationRuleCatalog catalog,
        AutomationMaskDefinition mask)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(mask);

        var issues = new List<AutomationValidationIssue>();
        ValidateCatalog(catalog, issues);
        ValidateMask(mask, issues);
        ValidateRuleReferences(catalog, mask, issues);
        return new AutomationValidationResult(issues);
    }

    private static void ValidateCatalog(
        AutomationRuleCatalog catalog,
        ICollection<AutomationValidationIssue> issues)
    {
        RequireSchema(
            catalog.SchemaVersion,
            AutomationContractVersions.RuleCatalogSchema,
            "catálogo de regras",
            issues);
        RequireIdentifier(catalog.CatalogId, "catalog.id", "catálogo", issues);
        RequireSemanticVersion(
            catalog.Version,
            "catalog.version",
            "catálogo",
            issues);

        var duplicateRuleIds = catalog.Rules
            .Where(rule => !string.IsNullOrWhiteSpace(rule.Id))
            .GroupBy(rule => rule.Id, IdentifierComparer)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key);
        foreach (var duplicateId in duplicateRuleIds)
        {
            issues.Add(AutomationValidationIssue.Error(
                "contrato",
                "catalog.duplicate-rule",
                $"A regra '{duplicateId}' aparece mais de uma vez no catálogo."));
        }

        foreach (var rule in catalog.Rules)
        {
            RequireIdentifier(rule.Id, "rule.id", "regra", issues);
            RequireSemanticVersion(
                rule.Version,
                "rule.version",
                $"regra '{rule.Id}'",
                issues);
            RequireText(rule.Name, "rule.name", $"regra '{rule.Id}'", issues);
            RequireText(
                rule.Discipline,
                "rule.discipline",
                $"regra '{rule.Id}'",
                issues);
            RequireText(
                rule.Condition,
                "rule.condition",
                $"regra '{rule.Id}'",
                issues);
            RequireText(
                rule.Description,
                "rule.description",
                $"regra '{rule.Id}'",
                issues);
            RequireText(
                rule.Source,
                "rule.source",
                $"regra '{rule.Id}'",
                issues);
            ValidateExtensions(
                rule.SupportedExtensions,
                $"regra '{rule.Id}'",
                issues);
            ValidateUniqueNames(
                rule.SupportedExtensions,
                "rule.duplicate-extension",
                "extensão",
                issues);
        }
    }

    private static void ValidateMask(
        AutomationMaskDefinition mask,
        ICollection<AutomationValidationIssue> issues)
    {
        RequireSchema(
            mask.SchemaVersion,
            AutomationContractVersions.MaskSchema,
            "máscara",
            issues);
        RequireIdentifier(mask.Id, "mask.id", "máscara", issues);
        RequireSemanticVersion(mask.Version, "mask.version", "máscara", issues);
        RequireSemanticVersion(
            mask.MinimumApplicationVersion,
            "mask.minimum-application-version",
            "máscara",
            issues);
        RequireText(mask.Name, "mask.name", "máscara", issues);
        RequireText(mask.Discipline, "mask.discipline", "máscara", issues);
        RequireText(mask.Description, "mask.description", "máscara", issues);
        ValidateExtensions(mask.AcceptedExtensions, "máscara", issues);
        ValidateUniqueNames(
            mask.AcceptedExtensions,
            "mask.duplicate-extension",
            "extensão",
            issues);

        if (mask.AcceptedExtensions.Count == 0)
        {
            issues.Add(AutomationValidationIssue.Error(
                "contrato",
                "mask.no-input-extension",
                "A máscara deve declarar ao menos uma extensão de entrada."));
        }

        ValidateUniqueNames(
            mask.Parameters.Select(parameter => parameter.Name),
            "mask.duplicate-parameter",
            "parâmetro",
            issues);
        ValidateUniqueNames(
            mask.Outputs.Select(output => output.Id),
            "mask.duplicate-output",
            "saída",
            issues);
        ValidateUniqueNames(
            mask.Dependencies.Select(dependency => dependency.Id),
            "mask.duplicate-dependency",
            "dependência",
            issues);
        ValidateUniqueNames(
            mask.RequiredRuleIds,
            "mask.duplicate-rule-reference",
            "referência de regra",
            issues);
        ValidateUniqueNames(
            mask.Preconditions.Select(requirement => requirement.Id),
            "mask.duplicate-precondition",
            "pré-condição",
            issues);
        ValidateUniqueNames(
            mask.Postconditions.Select(requirement => requirement.Id),
            "mask.duplicate-postcondition",
            "pós-condição",
            issues);

        if (mask.Outputs.Count == 0)
        {
            issues.Add(AutomationValidationIssue.Error(
                "contrato",
                "mask.no-output",
                "A máscara deve declarar ao menos uma saída."));
        }

        foreach (var parameter in mask.Parameters)
        {
            RequireIdentifier(
                parameter.Name,
                "mask.parameter-name",
                "parâmetro",
                issues);
            RequireText(
                parameter.Type,
                "mask.parameter-type",
                $"parâmetro '{parameter.Name}'",
                issues);

            if (parameter.AllowedValues is { Count: > 0 }
                && parameter.DefaultValue is not null
                && !parameter.AllowedValues.Contains(
                    parameter.DefaultValue,
                    StringComparer.OrdinalIgnoreCase))
            {
                issues.Add(AutomationValidationIssue.Error(
                    "contrato",
                    "mask.invalid-default",
                    $"O valor padrão do parâmetro '{parameter.Name}' não está "
                    + "entre os valores permitidos."));
            }
        }

        foreach (var dependency in mask.Dependencies)
        {
            RequireIdentifier(
                dependency.Id,
                "mask.dependency-id",
                "dependência",
                issues);
            RequireSemanticVersion(
                dependency.MinimumVersion,
                "mask.dependency-version",
                $"dependência '{dependency.Id}'",
                issues);
        }

        foreach (var output in mask.Outputs)
        {
            RequireIdentifier(output.Id, "mask.output-id", "saída", issues);
            RequireText(
                output.Description,
                "mask.output-description",
                $"saída '{output.Id}'",
                issues);
            if (!IsSafeRelativePath(output.RelativePath))
            {
                issues.Add(AutomationValidationIssue.Error(
                    "contrato",
                    "mask.unsafe-output-path",
                    $"A saída '{output.Id}' deve usar um caminho relativo seguro."));
            }
        }

        foreach (var requirement in mask.Preconditions.Concat(mask.Postconditions))
        {
            RequireIdentifier(
                requirement.Id,
                "mask.validation-id",
                "condição de validação",
                issues);
            RequireText(
                requirement.Description,
                "mask.validation-description",
                $"condição '{requirement.Id}'",
                issues);
        }
    }

    private static void ValidateRuleReferences(
        AutomationRuleCatalog catalog,
        AutomationMaskDefinition mask,
        ICollection<AutomationValidationIssue> issues)
    {
        var enabledRuleIds = catalog.Rules
            .Where(rule => rule.IsEnabled)
            .Select(rule => rule.Id)
            .ToHashSet(IdentifierComparer);

        foreach (var ruleId in mask.RequiredRuleIds.Distinct(IdentifierComparer))
        {
            RequireIdentifier(
                ruleId,
                "mask.rule-reference",
                "referência de regra",
                issues);
            if (!enabledRuleIds.Contains(ruleId))
            {
                issues.Add(AutomationValidationIssue.Error(
                    "contrato",
                    "mask.missing-rule",
                    $"A regra obrigatória '{ruleId}' não existe ou está desabilitada."));
            }
        }
    }

    private static void ValidateExtensions(
        IEnumerable<string> extensions,
        string owner,
        ICollection<AutomationValidationIssue> issues)
    {
        foreach (var extension in extensions)
        {
            if (string.IsNullOrWhiteSpace(extension)
                || !extension.StartsWith('.')
                || extension.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0
                || extension.Contains(Path.DirectorySeparatorChar)
                || extension.Contains(Path.AltDirectorySeparatorChar))
            {
                issues.Add(AutomationValidationIssue.Error(
                    "contrato",
                    "contract.invalid-extension",
                    $"A extensão '{extension}' declarada pela {owner} é inválida."));
            }
        }
    }

    private static void ValidateUniqueNames(
        IEnumerable<string> values,
        string code,
        string label,
        ICollection<AutomationValidationIssue> issues)
    {
        foreach (var duplicate in values
                     .Where(value => !string.IsNullOrWhiteSpace(value))
                     .GroupBy(value => value, IdentifierComparer)
                     .Where(group => group.Count() > 1)
                     .Select(group => group.Key))
        {
            issues.Add(AutomationValidationIssue.Error(
                "contrato",
                code,
                $"O {label} '{duplicate}' foi declarado mais de uma vez."));
        }
    }

    private static void RequireSchema(
        string value,
        string expected,
        string owner,
        ICollection<AutomationValidationIssue> issues)
    {
        if (!string.Equals(value, expected, StringComparison.Ordinal))
        {
            issues.Add(AutomationValidationIssue.Error(
                "contrato",
                "contract.unsupported-schema",
                $"O esquema da {owner} deve ser '{expected}', mas foi informado "
                + $"'{value}'."));
        }
    }

    private static void RequireSemanticVersion(
        string value,
        string code,
        string owner,
        ICollection<AutomationValidationIssue> issues)
    {
        if (string.IsNullOrWhiteSpace(value)
            || !SemanticVersionPattern().IsMatch(value))
        {
            issues.Add(AutomationValidationIssue.Error(
                "contrato",
                code,
                $"A versão da {owner} deve usar o formato estável X.Y.Z."));
        }
    }

    private static void RequireIdentifier(
        string value,
        string code,
        string owner,
        ICollection<AutomationValidationIssue> issues)
    {
        if (string.IsNullOrWhiteSpace(value)
            || !IdentifierPattern().IsMatch(value))
        {
            issues.Add(AutomationValidationIssue.Error(
                "contrato",
                code,
                $"O identificador de {owner} deve conter apenas letras "
                + "minúsculas, números, pontos ou hífens."));
        }
    }

    private static void RequireText(
        string value,
        string code,
        string owner,
        ICollection<AutomationValidationIssue> issues)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            issues.Add(AutomationValidationIssue.Error(
                "contrato",
                code,
                $"O campo obrigatório de {owner} não foi informado."));
        }
    }

    private static bool IsSafeRelativePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || Path.IsPathRooted(path))
        {
            return false;
        }

        return !path
            .Split(
                [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
                StringSplitOptions.RemoveEmptyEntries)
            .Any(segment => segment is "." or "..");
    }

    [GeneratedRegex(@"^[a-z0-9]+(?:[.-][a-z0-9]+)*$", RegexOptions.CultureInvariant)]
    private static partial Regex IdentifierPattern();

    [GeneratedRegex(@"^(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)$", RegexOptions.CultureInvariant)]
    private static partial Regex SemanticVersionPattern();
}
