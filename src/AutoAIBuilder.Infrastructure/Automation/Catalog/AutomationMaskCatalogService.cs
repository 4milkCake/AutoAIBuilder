using System.Text;
using AutoAIBuilder.Application.Automation.Catalog;
using AutoAIBuilder.Application.Automation.Contracts;
using AutoAIBuilder.Application.Automation.Validation;

namespace AutoAIBuilder.Infrastructure.Automation.Catalog;

public sealed class AutomationMaskCatalogService(
    IAutomationMaskCatalogRepository repository,
    IAutomationContractSerializer serializer,
    AutomationContractValidator validator,
    TimeProvider? timeProvider = null) : IAutomationMaskCatalogService
{
    public const long MaximumContractFileSizeBytes = 1_048_576;

    private readonly TimeProvider _timeProvider =
        timeProvider ?? TimeProvider.System;

    public async Task<AutomationMaskPackagePreview> AnalyzeAsync(
        string maskFilePath,
        string ruleCatalogFilePath,
        CancellationToken cancellationToken = default)
    {
        var issues = new List<AutomationValidationIssue>();
        var maskFileName = GetSourceFileName(maskFilePath, "máscara");
        var catalogFileName = GetSourceFileName(
            ruleCatalogFilePath,
            "catálogo");
        var maskDocument = await ReadContractFileAsync(
                maskFilePath,
                "máscara",
                issues,
                cancellationToken)
            .ConfigureAwait(false);
        var catalogDocument = await ReadContractFileAsync(
                ruleCatalogFilePath,
                "catálogo de regras",
                issues,
                cancellationToken)
            .ConfigureAwait(false);

        AutomationMaskDefinition? mask = null;
        AutomationRuleCatalog? catalog = null;
        if (maskDocument is not null)
        {
            try
            {
                mask = serializer.DeserializeMask(maskDocument);
            }
            catch (InvalidDataException exception)
            {
                issues.Add(AutomationValidationIssue.Error(
                    "importação",
                    "catalog.invalid-mask-document",
                    exception.Message));
            }
        }

        if (catalogDocument is not null)
        {
            try
            {
                catalog = serializer.DeserializeRuleCatalog(catalogDocument);
            }
            catch (InvalidDataException exception)
            {
                issues.Add(AutomationValidationIssue.Error(
                    "importação",
                    "catalog.invalid-rule-document",
                    exception.Message));
            }
        }

        var normalizedMask = string.Empty;
        var normalizedCatalog = string.Empty;
        var contentSha256 = string.Empty;
        var conflict = AutomationMaskCatalogConflict.None;
        if (mask is not null && catalog is not null)
        {
            issues.AddRange(ValidatePackage(mask, catalog));
            normalizedMask = serializer.Serialize(mask);
            normalizedCatalog = serializer.Serialize(catalog);
            contentSha256 = AutomationMaskPackageFingerprint.Compute(
                normalizedMask,
                normalizedCatalog);

            var existing = repository.GetByIdentity(mask.Id, mask.Version);
            if (existing is not null)
            {
                if (string.Equals(
                        existing.ContentSha256,
                        contentSha256,
                        StringComparison.OrdinalIgnoreCase))
                {
                    conflict = AutomationMaskCatalogConflict.AlreadyImported;
                    issues.Add(new AutomationValidationIssue(
                        "importação",
                        "catalog.already-imported",
                        "Esta mesma máscara e versão já estão no catálogo; "
                        + "nenhum conteúdo será duplicado.",
                        AutomationValidationSeverity.Information));
                }
                else
                {
                    conflict = AutomationMaskCatalogConflict.ContentConflict;
                    issues.Add(AutomationValidationIssue.Error(
                        "importação",
                        "catalog.version-content-conflict",
                        $"A máscara '{mask.Id}@{mask.Version}' já existe com "
                        + "outro conteúdo. Use uma nova versão; o registro "
                        + "existente não será sobrescrito."));
                }
            }
        }

        return new AutomationMaskPackagePreview(
            maskFileName,
            catalogFileName,
            mask,
            catalog,
            normalizedMask,
            normalizedCatalog,
            contentSha256,
            conflict,
            issues);
    }

    public AutomationMaskImportResult Import(
        AutomationMaskPackagePreview preview)
    {
        ArgumentNullException.ThrowIfNull(preview);
        if (preview.Conflict
            == AutomationMaskCatalogConflict.ContentConflict)
        {
            return new AutomationMaskImportResult(
                AutomationMaskImportStatus.ContentConflict,
                null,
                "A mesma versão já existe com outro conteúdo.");
        }

        if (!preview.IsValid
            || preview.Mask is null
            || preview.RuleCatalog is null)
        {
            return new AutomationMaskImportResult(
                AutomationMaskImportStatus.Rejected,
                null,
                "O pacote não passou pela análise de segurança.");
        }

        var mask = serializer.DeserializeMask(preview.MaskJson);
        var catalog = serializer.DeserializeRuleCatalog(
            preview.RuleCatalogJson);
        var issues = validator.Validate(catalog, mask).Issues
            .Concat(ValidatePackageLimits(mask, catalog))
            .ToArray();
        if (issues.Any(
                issue => issue.Severity
                    == AutomationValidationSeverity.Error))
        {
            return new AutomationMaskImportResult(
                AutomationMaskImportStatus.Rejected,
                null,
                "O pacote deixou de atender às regras de segurança.");
        }

        var contentSha256 = AutomationMaskPackageFingerprint.Compute(
            preview.MaskJson,
            preview.RuleCatalogJson);
        if (!string.Equals(
                contentSha256,
                preview.ContentSha256,
                StringComparison.OrdinalIgnoreCase))
        {
            return new AutomationMaskImportResult(
                AutomationMaskImportStatus.Rejected,
                null,
                "O conteúdo analisado foi alterado antes da importação.");
        }

        var existing = repository.GetByIdentity(mask.Id, mask.Version);
        if (existing is not null)
        {
            return string.Equals(
                    existing.ContentSha256,
                    contentSha256,
                    StringComparison.OrdinalIgnoreCase)
                ? new AutomationMaskImportResult(
                    AutomationMaskImportStatus.AlreadyImported,
                    existing,
                    "A mesma máscara e versão já estavam no catálogo.")
                : new AutomationMaskImportResult(
                    AutomationMaskImportStatus.ContentConflict,
                    existing,
                    "A versão informada já existe com outro conteúdo.");
        }

        var now = _timeProvider.GetUtcNow();
        var entry = new AutomationMaskCatalogEntry(
            Guid.NewGuid(),
            mask.Id,
            mask.Version,
            mask.Name.Trim(),
            mask.Discipline.Trim(),
            mask.Description.Trim(),
            mask.MinimumApplicationVersion,
            catalog.CatalogId,
            catalog.Version,
            catalog.Rules.Count,
            mask.Dependencies.Count,
            mask.Parameters.Count,
            mask.Outputs.Count,
            mask.SupportsSimulation,
            mask.IsIdempotent,
            preview.MaskJson,
            preview.RuleCatalogJson,
            contentSha256,
            preview.MaskSourceFileName,
            preview.RuleCatalogSourceFileName,
            false,
            now,
            now);
        repository.Add(entry);
        return new AutomationMaskImportResult(
            AutomationMaskImportStatus.Imported,
            entry,
            "Máscara importada inativa; nenhuma automação foi executada.");
    }

    public IReadOnlyList<AutomationMaskCatalogEntry> GetAll() =>
        repository.GetAll();

    public AutomationMaskCatalogEntry SetActive(Guid id, bool isActive)
    {
        var entry = repository.Get(id)
            ?? throw new InvalidOperationException(
                "A máscara selecionada não existe mais no catálogo.");
        if (isActive
            && !IsApplicationVersionCompatible(
                entry.MinimumApplicationVersion))
        {
            throw new InvalidOperationException(
                $"A máscara requer o AutoAIBuilder "
                + $"{entry.MinimumApplicationVersion} ou superior.");
        }

        return repository.SetActive(
            id,
            isActive,
            _timeProvider.GetUtcNow());
    }

    private IReadOnlyList<AutomationValidationIssue> ValidatePackage(
        AutomationMaskDefinition mask,
        AutomationRuleCatalog catalog) =>
        validator.Validate(catalog, mask).Issues
            .Concat(ValidatePackageLimits(mask, catalog))
            .ToArray();

    private static IReadOnlyList<AutomationValidationIssue>
        ValidatePackageLimits(
            AutomationMaskDefinition mask,
            AutomationRuleCatalog catalog)
    {
        var issues = new List<AutomationValidationIssue>();
        AddLimitIssue(
            catalog.Rules.Count,
            500,
            "catalog.too-many-rules",
            "regras",
            issues);
        AddLimitIssue(
            mask.AcceptedExtensions.Count,
            50,
            "catalog.too-many-extensions",
            "extensões",
            issues);
        AddLimitIssue(
            mask.Dependencies.Count,
            50,
            "catalog.too-many-dependencies",
            "dependências",
            issues);
        AddLimitIssue(
            mask.Parameters.Count,
            100,
            "catalog.too-many-parameters",
            "parâmetros",
            issues);
        AddLimitIssue(
            mask.Outputs.Count,
            100,
            "catalog.too-many-outputs",
            "saídas",
            issues);
        AddLimitIssue(
            mask.Preconditions.Count,
            100,
            "catalog.too-many-preconditions",
            "pré-condições",
            issues);
        AddLimitIssue(
            mask.Postconditions.Count,
            100,
            "catalog.too-many-postconditions",
            "pós-condições",
            issues);

        if (!IsApplicationVersionCompatible(
                mask.MinimumApplicationVersion))
        {
            issues.Add(AutomationValidationIssue.Error(
                "importação",
                "catalog.incompatible-application-version",
                $"A máscara requer o AutoAIBuilder "
                + $"{mask.MinimumApplicationVersion} ou superior; esta versão "
                + $"usa contratos "
                + $"{AutomationContractVersions.CurrentApplicationVersion}."));
        }

        return issues;
    }

    private static void AddLimitIssue(
        int count,
        int maximum,
        string code,
        string label,
        ICollection<AutomationValidationIssue> issues)
    {
        if (count <= maximum)
        {
            return;
        }

        issues.Add(AutomationValidationIssue.Error(
            "importação",
            code,
            $"O pacote declara {count} {label}; o limite seguro é {maximum}."));
    }

    private static bool IsApplicationVersionCompatible(
        string minimumVersion) =>
        Version.TryParse(
            AutomationContractVersions.CurrentApplicationVersion,
            out var current)
        && Version.TryParse(minimumVersion, out var minimum)
        && current >= minimum;

    private static async Task<string?> ReadContractFileAsync(
        string suppliedPath,
        string label,
        ICollection<AutomationValidationIssue> issues,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(suppliedPath))
        {
            issues.Add(AutomationValidationIssue.Error(
                "importação",
                "catalog.file-required",
                $"Selecione o arquivo JSON da {label}."));
            return null;
        }

        try
        {
            var path = Path.GetFullPath(suppliedPath);
            if (!string.Equals(
                    Path.GetExtension(path),
                    ".json",
                    StringComparison.OrdinalIgnoreCase))
            {
                issues.Add(AutomationValidationIssue.Error(
                    "importação",
                    "catalog.json-required",
                    $"O arquivo da {label} deve usar a extensão .json."));
                return null;
            }

            if (!File.Exists(path))
            {
                issues.Add(AutomationValidationIssue.Error(
                    "importação",
                    "catalog.file-not-found",
                    $"O arquivo da {label} não foi encontrado."));
                return null;
            }

            var attributes = File.GetAttributes(path);
            if ((attributes & FileAttributes.ReparsePoint) != 0)
            {
                issues.Add(AutomationValidationIssue.Error(
                    "importação",
                    "catalog.reparse-point-rejected",
                    $"O arquivo da {label} é um link ou ponto de nova análise "
                    + "e não pode ser importado."));
                return null;
            }

            var before = new FileInfo(path);
            if (before.Length == 0
                || before.Length > MaximumContractFileSizeBytes)
            {
                issues.Add(AutomationValidationIssue.Error(
                    "importação",
                    "catalog.invalid-file-size",
                    $"O arquivo da {label} deve ter entre 1 byte e "
                    + $"{MaximumContractFileSizeBytes:N0} bytes."));
                return null;
            }
            var originalLength = before.Length;
            var originalLastWriteTimeUtc = before.LastWriteTimeUtc;

            await using var stream = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 16_384,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            using var reader = new StreamReader(
                stream,
                new UTF8Encoding(
                    encoderShouldEmitUTF8Identifier: false,
                    throwOnInvalidBytes: true),
                detectEncodingFromByteOrderMarks: true);
            var content = await reader
                .ReadToEndAsync(cancellationToken)
                .ConfigureAwait(false);
            before.Refresh();
            if (before.Length != originalLength
                || before.LastWriteTimeUtc != originalLastWriteTimeUtc)
            {
                issues.Add(AutomationValidationIssue.Error(
                    "importação",
                    "catalog.file-changed",
                    $"O arquivo da {label} mudou durante a leitura. Analise-o "
                    + "novamente."));
                return null;
            }

            if (string.IsNullOrWhiteSpace(content))
            {
                issues.Add(AutomationValidationIssue.Error(
                    "importação",
                    "catalog.empty-file",
                    $"O arquivo da {label} está vazio."));
                return null;
            }

            return content;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (
            exception is IOException
                or UnauthorizedAccessException
                or ArgumentException
                or NotSupportedException
                or DecoderFallbackException)
        {
            issues.Add(AutomationValidationIssue.Error(
                "importação",
                "catalog.file-read-failed",
                $"Não foi possível ler o arquivo da {label}: "
                + exception.Message));
            return null;
        }
    }

    private static string GetSourceFileName(
        string suppliedPath,
        string fallback)
    {
        try
        {
            var name = Path.GetFileName(suppliedPath);
            return string.IsNullOrWhiteSpace(name) ? fallback : name;
        }
        catch (ArgumentException)
        {
            return fallback;
        }
    }
}
