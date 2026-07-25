using System.Security.Cryptography;
using System.Text;
using AutoAIBuilder.Application.Automation;
using AutoAIBuilder.Application.Automation.Adapters;
using AutoAIBuilder.Application.Automation.Catalog;
using AutoAIBuilder.Application.Automation.Contracts;
using AutoAIBuilder.Application.Automation.Execution;
using AutoAIBuilder.Application.Automation.Orchestration;
using AutoAIBuilder.Application.Automation.Validation;

namespace AutoAIBuilder.Infrastructure.Automation;

public sealed class SafeAutomationOrchestrator(
    IAutomationMaskCatalogRepository maskCatalogRepository,
    IAutomationAdapterRegistry adapterRegistry,
    IAutomationIntegrationAssessmentRepository assessmentRepository,
    IAutomationExecutionService executionService,
    IAutomationContractSerializer serializer,
    AutomationContractValidator contractValidator,
    TimeProvider? timeProvider = null) : IAutomationOrchestrator
{
    private readonly TimeProvider _timeProvider =
        timeProvider ?? TimeProvider.System;

    public IReadOnlyList<AutomationAdapterDescriptor>
        GetRegisteredAdapters() =>
        adapterRegistry.GetAll();

    public IReadOnlyList<AutomationIntegrationAssessment>
        GetRecentAssessments(int maximumEntries = 50) =>
        assessmentRepository.GetRecent(maximumEntries);

    public AutomationIntegrationAssessment AssessIntegration(
        Guid projectId,
        Guid catalogEntryId)
    {
        if (projectId == Guid.Empty)
        {
            throw new ArgumentException(
                "O projeto é obrigatório para auditar a avaliação.",
                nameof(projectId));
        }

        if (catalogEntryId == Guid.Empty)
        {
            throw new ArgumentException(
                "A versão catalogada é obrigatória para a avaliação.",
                nameof(catalogEntryId));
        }

        var entry = maskCatalogRepository.Get(catalogEntryId)
            ?? throw new InvalidOperationException(
                "A versão selecionada não existe mais no catálogo.");
        var result = Evaluate(entry);
        var assessment = new AutomationIntegrationAssessment(
            Guid.NewGuid(),
            projectId,
            entry.Id,
            entry.MaskId,
            entry.MaskVersion,
            entry.ContentSha256,
            result.Status,
            result.Descriptor?.AdapterId,
            result.Descriptor?.AdapterVersion,
            result.Summary,
            _timeProvider.GetUtcNow());
        assessmentRepository.Add(assessment);
        return assessment;
    }

    public async Task<AutomationExecutionOutcome> ExecutePlanAsync(
        AutomationExecutionPlan plan,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(plan);
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(plan.RuleCatalogId)
            || string.IsNullOrWhiteSpace(plan.RuleCatalogVersion)
            || !IsSha256(plan.ContractSha256)
            || plan.RuleCatalog is null)
        {
            throw new InvalidOperationException(
                "O plano não contém a identidade e a impressão digital do "
                + "pacote de contratos.");
        }

        var contractValidation = contractValidator.Validate(
            plan.RuleCatalog,
            plan.Mask);
        var canonicalFingerprint = AutomationMaskPackageFingerprint.Compute(
            serializer.Serialize(plan.Mask),
            serializer.Serialize(plan.RuleCatalog));
        if (!contractValidation.IsValid
            || !string.Equals(
                plan.RuleCatalog.CatalogId,
                plan.RuleCatalogId,
                StringComparison.Ordinal)
            || !string.Equals(
                plan.RuleCatalog.Version,
                plan.RuleCatalogVersion,
                StringComparison.Ordinal)
            || !string.Equals(
                canonicalFingerprint,
                plan.ContractSha256,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "O orquestrador recusou um plano cujo snapshot de contrato "
                + "não corresponde à identidade e ao SHA-256 declarados.");
        }

        var resolution = adapterRegistry.Resolve(
            plan.Mask.Id,
            plan.Mask.Version,
            plan.ContractSha256!);
        if (!resolution.IsResolved
            || resolution.Descriptor is null
            || resolution.Adapter is null)
        {
            throw new InvalidOperationException(
                "O orquestrador recusou o plano: " + resolution.Summary);
        }

        if (plan.Mode == AutomationExecutionMode.Simulation
            && !resolution.Descriptor.SupportsSimulation)
        {
            throw new InvalidOperationException(
                "O adaptador resolvido não permite simulação.");
        }

        if (plan.Mode == AutomationExecutionMode.Apply
            && !resolution.Descriptor.SupportsApply)
        {
            throw new InvalidOperationException(
                "O adaptador resolvido não permite aplicação.");
        }

        var resolvedPlan = plan with
        {
            Adapter = resolution.Descriptor,
            IdempotencyKey = ComputeAdapterBoundIdempotencyKey(
                plan.IdempotencyKey,
                resolution.Descriptor)
        };
        return await executionService.ExecuteAsync(
                resolvedPlan,
                resolution.Adapter.ExecuteAsync,
                cancellationToken)
            .ConfigureAwait(false);
    }

    public Task<AutomationExecutionResult> ExecuteAsync(
        AutomationRequest request,
        IProgress<int>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        progress?.Report(0);

        var entry = maskCatalogRepository.GetAll().SingleOrDefault(
            candidate =>
                candidate.IsActive
                && string.Equals(
                    candidate.MaskId,
                    request.WorkflowId,
                    StringComparison.Ordinal));
        if (entry is null)
        {
            return Task.FromResult(
                new AutomationExecutionResult(
                    false,
                    "Execução bloqueada: não existe uma versão ativa para o "
                    + $"fluxo '{request.WorkflowId}'.",
                    [
                        "O Marco 11.6B não escolhe versões automaticamente e "
                        + "não executa máscaras catalogadas."
                    ]));
        }

        var assessment = AssessIntegration(request.ProjectId, entry.Id);
        progress?.Report(100);
        return Task.FromResult(
            new AutomationExecutionResult(
                false,
                "Execução de máscara catalogada bloqueada no Marco 11.6B.",
                [
                    assessment.Summary,
                    "A avaliação foi auditada, mas nenhum adaptador foi "
                    + "invocado e nenhum arquivo foi criado."
                ]));
    }

    private EvaluationResult Evaluate(
        AutomationMaskCatalogEntry entry)
    {
        AutomationAdapterDescriptor? descriptor = null;
        try
        {
            var mask = serializer.DeserializeMask(entry.MaskJson);
            var catalog = serializer.DeserializeRuleCatalog(
                entry.RuleCatalogJson);
            var validation = contractValidator.Validate(catalog, mask);
            var canonicalMask = serializer.Serialize(mask);
            var canonicalCatalog = serializer.Serialize(catalog);
            var fingerprint = AutomationMaskPackageFingerprint.Compute(
                canonicalMask,
                canonicalCatalog);
            var identitiesMatch =
                string.Equals(mask.Id, entry.MaskId, StringComparison.Ordinal)
                && string.Equals(
                    mask.Version,
                    entry.MaskVersion,
                    StringComparison.Ordinal)
                && string.Equals(
                    catalog.CatalogId,
                    entry.RuleCatalogId,
                    StringComparison.Ordinal)
                && string.Equals(
                    catalog.Version,
                    entry.RuleCatalogVersion,
                    StringComparison.Ordinal);
            if (!validation.IsValid
                || !identitiesMatch
                || !string.Equals(
                    fingerprint,
                    entry.ContentSha256,
                    StringComparison.OrdinalIgnoreCase))
            {
                return new EvaluationResult(
                    AutomationIntegrationStatus.InvalidCatalogContract,
                    null,
                    "O snapshot catalogado não passou pela revalidação de "
                    + "identidade, conteúdo e SHA-256. A integração foi bloqueada.");
            }
        }
        catch (Exception exception) when (
            exception is InvalidDataException
                or ArgumentException
                or FormatException)
        {
            return new EvaluationResult(
                AutomationIntegrationStatus.InvalidCatalogContract,
                null,
                "O snapshot catalogado está inválido e foi preservado sem "
                + $"uso: {exception.Message}");
        }

        if (!entry.IsActive)
        {
            return new EvaluationResult(
                AutomationIntegrationStatus.InactiveMask,
                null,
                "A versão está inativa. Nenhuma resolução executável foi "
                + "tentada.");
        }

        var resolution = adapterRegistry.Resolve(
            entry.MaskId,
            entry.MaskVersion,
            entry.ContentSha256);
        descriptor = resolution.Descriptor;
        if (resolution.Status
            == AutomationAdapterResolutionStatus.NotRegistered)
        {
            return new EvaluationResult(
                AutomationIntegrationStatus.AdapterNotRegistered,
                null,
                resolution.Summary);
        }

        if (resolution.Status
            == AutomationAdapterResolutionStatus.ContractMismatch)
        {
            return new EvaluationResult(
                AutomationIntegrationStatus.ContractMismatch,
                descriptor,
                resolution.Summary);
        }

        if (resolution.Status
            == AutomationAdapterResolutionStatus.Disabled)
        {
            return new EvaluationResult(
                AutomationIntegrationStatus.AdapterDisabled,
                descriptor,
                resolution.Summary);
        }

        if (!resolution.IsResolved || descriptor is null)
        {
            return new EvaluationResult(
                AutomationIntegrationStatus.AdapterNotRegistered,
                descriptor,
                "O registro não conseguiu resolver um adaptador exato.");
        }

        if (!descriptor.CatalogExecutionEnabled)
        {
            return new EvaluationResult(
                AutomationIntegrationStatus.CatalogExecutionBlocked,
                descriptor,
                "A identidade e o SHA-256 correspondem a um adaptador interno, "
                + "mas sua política não autoriza máscaras catalogadas.");
        }

        return new EvaluationResult(
            AutomationIntegrationStatus.Ready,
            descriptor,
            "Máscara ativa e adaptador interno homologado por identidade, "
            + "versão e SHA-256. A avaliação não executou a máscara.");
    }

    private static bool IsSha256(string? value)
    {
        if (value?.Length != 64)
        {
            return false;
        }

        try
        {
            return Convert.FromHexString(value).Length == 32;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static string ComputeAdapterBoundIdempotencyKey(
        string planKey,
        AutomationAdapterDescriptor descriptor)
    {
        var canonical =
            "AUTOAIBUILDER-ADAPTER-BOUND-EXECUTION-1\n"
            + planKey
            + "\n"
            + descriptor.AdapterId
            + "@"
            + descriptor.AdapterVersion
            + "\n"
            + descriptor.ContractSha256;
        return Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }

    private sealed record EvaluationResult(
        AutomationIntegrationStatus Status,
        AutomationAdapterDescriptor? Descriptor,
        string Summary);
}
