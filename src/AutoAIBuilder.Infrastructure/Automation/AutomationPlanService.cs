using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AutoAIBuilder.Application.Automation.Catalog;
using AutoAIBuilder.Application.Automation.Contracts;
using AutoAIBuilder.Application.Automation.Execution;
using AutoAIBuilder.Application.Automation.Validation;

namespace AutoAIBuilder.Infrastructure.Automation;

public sealed class AutomationPlanService(
    AutomationContractValidator contractValidator,
    TimeProvider? timeProvider = null) : IAutomationPlanService
{
    private static readonly StringComparer PathComparer =
        OperatingSystem.IsWindows()
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;

    private readonly TimeProvider _timeProvider =
        timeProvider ?? TimeProvider.System;

    public async Task<AutomationExecutionPlan> CreatePlanAsync(
        AutomationPlanRequest request,
        AutomationExecutionMode mode,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.RuleCatalog);
        ArgumentNullException.ThrowIfNull(request.Mask);

        var issues = contractValidator
            .Validate(request.RuleCatalog, request.Mask)
            .Issues
            .ToList();

        if (request.ProjectId == Guid.Empty)
        {
            issues.Add(AutomationValidationIssue.Error(
                "pré-validação",
                "plan.project-required",
                "O plano deve estar associado a um projeto."));
        }

        if (mode == AutomationExecutionMode.Simulation
            && !request.Mask.SupportsSimulation)
        {
            issues.Add(AutomationValidationIssue.Error(
                "pré-validação",
                "plan.simulation-not-supported",
                "A máscara não declara suporte ao modo de simulação."));
        }

        ValidateApplicationVersion(request.Mask, issues);
        var outputRoot = NormalizeOutputRoot(request.OutputRoot, issues);
        var parameters = NormalizeParameters(request, issues);
        ValidateDependencies(request, issues);
        var inputs = await CaptureInputsAsync(
                request.InputPaths ?? [],
                request.Mask,
                issues,
                cancellationToken)
            .ConfigureAwait(false);

        if (inputs.Count == 0)
        {
            issues.Add(AutomationValidationIssue.Error(
                "pré-validação",
                "plan.no-input",
                "Ao menos um arquivo de entrada válido é obrigatório."));
        }

        var idempotencyKey = ComputeIdempotencyKey(
            request,
            inputs,
            outputRoot,
            parameters);
        var serializer = new AutomationContractJsonSerializer();
        var contractSha256 = AutomationMaskPackageFingerprint.Compute(
            serializer.Serialize(request.Mask),
            serializer.Serialize(request.RuleCatalog));

        return new AutomationExecutionPlan(
            Guid.NewGuid(),
            request.ProjectId,
            mode,
            request.Mask,
            inputs,
            outputRoot,
            parameters,
            CreateActions(mode, request.Mask),
            issues,
            idempotencyKey,
            _timeProvider.GetUtcNow(),
            request.RuleCatalog.CatalogId,
            request.RuleCatalog.Version,
            contractSha256,
            RuleCatalog: request.RuleCatalog);
    }

    private static void ValidateApplicationVersion(
        AutomationMaskDefinition mask,
        ICollection<AutomationValidationIssue> issues)
    {
        if (!Version.TryParse(
                AutomationContractVersions.CurrentApplicationVersion,
                out var currentVersion)
            || !Version.TryParse(
                mask.MinimumApplicationVersion,
                out var minimumVersion)
            || currentVersion < minimumVersion)
        {
            issues.Add(AutomationValidationIssue.Error(
                "pré-validação",
                "plan.application-version",
                $"A máscara requer o AutoAIBuilder {mask.MinimumApplicationVersion} "
                + $"ou superior; a versão de contrato atual é "
                + $"{AutomationContractVersions.CurrentApplicationVersion}."));
        }
    }

    private static string NormalizeOutputRoot(
        string outputRoot,
        ICollection<AutomationValidationIssue> issues)
    {
        if (string.IsNullOrWhiteSpace(outputRoot))
        {
            issues.Add(AutomationValidationIssue.Error(
                "pré-validação",
                "plan.output-required",
                "O diretório de saída é obrigatório."));
            return string.Empty;
        }

        try
        {
            var normalized = Path.TrimEndingDirectorySeparator(
                Path.GetFullPath(outputRoot.Trim()));
            if (File.Exists(normalized))
            {
                issues.Add(AutomationValidationIssue.Error(
                    "pré-validação",
                    "plan.output-is-file",
                    $"O destino de saída aponta para um arquivo: {normalized}"));
            }

            return normalized;
        }
        catch (Exception exception) when (
            exception is ArgumentException
                or NotSupportedException
                or PathTooLongException)
        {
            issues.Add(AutomationValidationIssue.Error(
                "pré-validação",
                "plan.invalid-output",
                $"O diretório de saída é inválido: {exception.Message}"));
            return outputRoot.Trim();
        }
    }

    private static IReadOnlyDictionary<string, string> NormalizeParameters(
        AutomationPlanRequest request,
        ICollection<AutomationValidationIssue> issues)
    {
        var supplied = request.Parameters
            ?? new Dictionary<string, string>();
        var declared = request.Mask.Parameters.ToDictionary(
            parameter => parameter.Name,
            StringComparer.OrdinalIgnoreCase);
        var normalized = new Dictionary<string, string>(
            StringComparer.OrdinalIgnoreCase);

        foreach (var key in supplied.Keys)
        {
            if (!declared.ContainsKey(key))
            {
                issues.Add(AutomationValidationIssue.Error(
                    "pré-validação",
                    "plan.unknown-parameter",
                    $"O parâmetro '{key}' não foi declarado pela máscara."));
            }
        }

        foreach (var parameter in request.Mask.Parameters)
        {
            var hasSuppliedValue = supplied.TryGetValue(
                parameter.Name,
                out var suppliedValue);
            var value = hasSuppliedValue
                ? suppliedValue?.Trim()
                : parameter.DefaultValue?.Trim();

            if (parameter.IsRequired && string.IsNullOrWhiteSpace(value))
            {
                issues.Add(AutomationValidationIssue.Error(
                    "pré-validação",
                    "plan.required-parameter",
                    $"O parâmetro obrigatório '{parameter.Name}' não foi informado."));
                continue;
            }

            if (value is null)
            {
                continue;
            }

            if (parameter.AllowedValues is { Count: > 0 }
                && !parameter.AllowedValues.Contains(
                    value,
                    StringComparer.OrdinalIgnoreCase))
            {
                issues.Add(AutomationValidationIssue.Error(
                    "pré-validação",
                    "plan.parameter-not-allowed",
                    $"O valor de '{parameter.Name}' não está entre os valores "
                    + "permitidos."));
                continue;
            }

            normalized[parameter.Name] = value;
        }

        return normalized;
    }

    private static void ValidateDependencies(
        AutomationPlanRequest request,
        ICollection<AutomationValidationIssue> issues)
    {
        var available = request.AvailableDependencies
            ?? new Dictionary<string, string>();
        foreach (var dependency in request.Mask.Dependencies
                     .Where(dependency => dependency.IsRequired))
        {
            if (!available.TryGetValue(dependency.Id, out var version))
            {
                issues.Add(AutomationValidationIssue.Error(
                    "pré-validação",
                    "plan.missing-dependency",
                    $"A dependência obrigatória '{dependency.Id}' não está disponível."));
                continue;
            }

            if (!Version.TryParse(version, out var availableVersion)
                || !Version.TryParse(
                    dependency.MinimumVersion,
                    out var minimumVersion)
                || availableVersion < minimumVersion)
            {
                issues.Add(AutomationValidationIssue.Error(
                    "pré-validação",
                    "plan.dependency-version",
                    $"A dependência '{dependency.Id}' requer a versão "
                    + $"{dependency.MinimumVersion} ou superior; foi encontrada "
                    + $"'{version}'."));
            }
        }
    }

    private static async Task<IReadOnlyList<AutomationInputSnapshot>>
        CaptureInputsAsync(
            IReadOnlyList<string> inputPaths,
            AutomationMaskDefinition mask,
            ICollection<AutomationValidationIssue> issues,
            CancellationToken cancellationToken)
    {
        var snapshots = new List<AutomationInputSnapshot>();
        var seen = new HashSet<string>(PathComparer);

        foreach (var suppliedPath in inputPaths)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(suppliedPath))
            {
                issues.Add(AutomationValidationIssue.Error(
                    "pré-validação",
                    "plan.invalid-input-path",
                    "Um caminho de entrada está vazio."));
                continue;
            }

            string path;
            try
            {
                path = Path.GetFullPath(suppliedPath.Trim());
            }
            catch (Exception exception) when (
                exception is ArgumentException
                    or NotSupportedException
                    or PathTooLongException)
            {
                issues.Add(AutomationValidationIssue.Error(
                    "pré-validação",
                    "plan.invalid-input-path",
                    $"O caminho de entrada é inválido: {exception.Message}"));
                continue;
            }

            if (!seen.Add(path))
            {
                continue;
            }

            if (!File.Exists(path))
            {
                issues.Add(AutomationValidationIssue.Error(
                    "pré-validação",
                    "plan.input-not-found",
                    $"O arquivo de entrada não foi encontrado: {path}"));
                continue;
            }

            var extension = Path.GetExtension(path);
            if (!mask.AcceptedExtensions.Contains(
                    extension,
                    StringComparer.OrdinalIgnoreCase))
            {
                issues.Add(AutomationValidationIssue.Error(
                    "pré-validação",
                    "plan.extension-not-supported",
                    $"O arquivo '{Path.GetFileName(path)}' usa a extensão "
                    + $"'{extension}', não aceita pela máscara."));
                continue;
            }

            try
            {
                var info = new FileInfo(path);
                var checksum = await FileChecksum
                    .ComputeSha256Async(path, cancellationToken)
                    .ConfigureAwait(false);
                info.Refresh();
                snapshots.Add(new AutomationInputSnapshot(
                    path,
                    info.Length,
                    info.LastWriteTimeUtc,
                    checksum));
            }
            catch (Exception exception) when (
                exception is IOException or UnauthorizedAccessException)
            {
                issues.Add(AutomationValidationIssue.Error(
                    "pré-validação",
                    "plan.input-unreadable",
                    $"Não foi possível ler '{path}': {exception.Message}"));
            }
        }

        return snapshots;
    }

    private static string ComputeIdempotencyKey(
        AutomationPlanRequest request,
        IReadOnlyList<AutomationInputSnapshot> inputs,
        string outputRoot,
        IReadOnlyDictionary<string, string> parameters)
    {
        var canonical = new StringBuilder()
            .Append(request.ProjectId.ToString("D")).Append('\n')
            .Append(request.RuleCatalog.CatalogId).Append('@')
            .Append(request.RuleCatalog.Version).Append('\n')
            .Append(request.Mask.Id).Append('@')
            .Append(request.Mask.Version).Append('\n')
            .Append(JsonSerializer.Serialize(request.RuleCatalog)).Append('\n')
            .Append(JsonSerializer.Serialize(request.Mask)).Append('\n')
            .Append(NormalizeForKey(outputRoot)).Append('\n');

        foreach (var input in inputs
                     .OrderBy(input => input.OriginalPath, PathComparer))
        {
            canonical
                .Append(NormalizeForKey(input.OriginalPath)).Append('|')
                .Append(input.SizeBytes).Append('|')
                .Append(input.Sha256).Append('\n');
        }

        foreach (var parameter in parameters
                     .OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase))
        {
            canonical
                .Append(parameter.Key.ToLowerInvariant()).Append('=')
                .Append(parameter.Value).Append('\n');
        }

        return Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(canonical.ToString())));
    }

    private static string NormalizeForKey(string path) =>
        OperatingSystem.IsWindows() ? path.ToUpperInvariant() : path;

    private static IReadOnlyList<AutomationPlanAction> CreateActions(
        AutomationExecutionMode mode,
        AutomationMaskDefinition mask)
    {
        var actions = new List<AutomationPlanAction>
        {
            new(
                1,
                "validate",
                "Validar contrato, dependências, parâmetros e arquivos de entrada.",
                false),
            new(
                2,
                "snapshot",
                "Registrar tamanho, data e checksum SHA-256 dos originais.",
                false),
            new(
                3,
                "copy",
                "Criar área de trabalho isolada e copiar as entradas.",
                true),
            new(
                4,
                "execute",
                $"Aplicar a máscara {mask.Id}@{mask.Version} somente nas cópias.",
                true),
            new(
                5,
                "post-validate",
                "Validar saídas e confirmar novamente os checksums dos originais.",
                false),
            new(
                6,
                "publish",
                "Publicar a saída em pasta exclusiva ou preservar falhas para recuperação.",
                true),
            new(
                7,
                "audit",
                "Registrar resultado, evidências e chave de idempotência.",
                true)
        };

        if (mode == AutomationExecutionMode.Simulation)
        {
            actions.Insert(
                0,
                new AutomationPlanAction(
                    0,
                    "simulation",
                    "Somente exibir o plano; nenhuma pasta, cópia ou saída será criada.",
                    false));
        }

        return actions;
    }
}
