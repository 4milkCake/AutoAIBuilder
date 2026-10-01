using System.Security.Cryptography;
using System.Text;
using AutoAIBuilder.Application.Automation.Preview;
using AutoAIBuilder.Application.CadVisualization;
using AutoAIBuilder.Application.Semantics;
using AutoAIBuilder.Domain.Semantics;

namespace AutoAIBuilder.Infrastructure.Automation;

public sealed class AutomationPreviewService(
    IAutomationPreviewDecisionRepository decisions) :
    IAutomationPreviewService
{
    public AutomationPreviewPlan Build(
        Guid projectId,
        SemanticWorkspaceSnapshot semanticSnapshot,
        CadVisualizationSnapshot? cadSnapshot)
    {
        ArgumentNullException.ThrowIfNull(semanticSnapshot);
        if (projectId == Guid.Empty)
        {
            throw new ArgumentException(
                "O projeto da pré-visualização é obrigatório.",
                nameof(projectId));
        }

        var dataset = semanticSnapshot.Dataset
            ?? throw new InvalidOperationException(
                "Importe a base semântica antes de gerar a pré-visualização.");
        if (dataset.ProjectId != projectId)
        {
            throw new InvalidDataException(
                "A base semântica não pertence ao projeto selecionado.");
        }

        var sourceHash = cadSnapshot?.SourceSha256
                         ?? dataset.SourceFingerprint;
        var planId = CalculatePlanId(
            projectId,
            dataset.Id,
            dataset.SourceFingerprint,
            sourceHash);
        var stored = decisions.GetForPlan(planId)
            .ToDictionary(
                decision => decision.GroupId,
                StringComparer.OrdinalIgnoreCase);
        var groups = BuildGroups(semanticSnapshot, cadSnapshot, stored);
        return CreatePlan(
            planId,
            projectId,
            dataset.Id,
            dataset.DrawingPath,
            sourceHash,
            dataset.SourceFingerprint,
            groups,
            ConfirmSourceIntegrity(dataset.DrawingPath, sourceHash),
            cadSnapshot);
    }

    public AutomationPreviewPlan Decide(
        AutomationPreviewPlan currentPlan,
        string groupId,
        AutomationPreviewDecisionStatus status,
        string? note)
    {
        ArgumentNullException.ThrowIfNull(currentPlan);
        ArgumentException.ThrowIfNullOrWhiteSpace(groupId);
        if (status is not (
                AutomationPreviewDecisionStatus.Approved
                or AutomationPreviewDecisionStatus.Rejected))
        {
            throw new ArgumentOutOfRangeException(
                nameof(status),
                "A decisão deve aprovar ou rejeitar o grupo.");
        }

        var group = currentPlan.Groups.FirstOrDefault(
            item => item.Id.Equals(groupId, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException(
                "O grupo selecionado não pertence ao plano atual.");
        if (!group.RequiresDecision)
        {
            throw new InvalidOperationException(
                "Este grupo é apenas uma referência protegida.");
        }

        var trimmedNote = string.IsNullOrWhiteSpace(note)
            ? null
            : note.Trim();
        if (trimmedNote?.Length > 1_000)
        {
            throw new ArgumentException(
                "A observação deve ter no máximo 1.000 caracteres.",
                nameof(note));
        }

        var decision = new AutomationPreviewDecision(
            Guid.NewGuid(),
            currentPlan.Id,
            currentPlan.ProjectId,
            currentPlan.DatasetId,
            group.Id,
            status,
            trimmedNote,
            DateTimeOffset.UtcNow);
        decisions.Save(decision);

        var groups = currentPlan.Groups
            .Select(item => item.Id.Equals(
                    group.Id,
                    StringComparison.OrdinalIgnoreCase)
                ? item with
                {
                    Decision = status,
                    DecisionNote = trimmedNote,
                    DecidedAt = decision.DecidedAt
                }
                : item)
            .ToArray();
        var updated = CreatePlan(
            currentPlan.Id,
            currentPlan.ProjectId,
            currentPlan.DatasetId,
            currentPlan.SourceDwgPath,
            currentPlan.SourceSha256,
            currentPlan.DatasetFingerprint,
            groups,
            ConfirmSourceIntegrity(
                currentPlan.SourceDwgPath,
                currentPlan.SourceSha256),
            null);
        return updated with { BeforeSummary = currentPlan.BeforeSummary };
    }

    private static IReadOnlyList<AutomationPreviewGroup> BuildGroups(
        SemanticWorkspaceSnapshot snapshot,
        CadVisualizationSnapshot? cad,
        IReadOnlyDictionary<string, AutomationPreviewDecision> stored)
    {
        var electrical = snapshot.Points
            .Where(point => point.Discipline == SemanticDiscipline.Electrical)
            .Select(point => point.Id)
            .ToArray();
        var hydraulic = snapshot.Points
            .Where(point => point.Discipline == SemanticDiscipline.Hydraulic)
            .Select(point => point.Id)
            .ToArray();
        var corrected = snapshot.Points
            .Where(point => point.ReviewStatus == SemanticReviewStatus.Corrected)
            .Select(point => point.Id)
            .ToArray();
        var pending = snapshot.Points
            .Where(point => point.ReviewStatus == SemanticReviewStatus.NeedsReview)
            .Select(point => point.Id)
            .ToArray();
        var layerCount = snapshot.Points
            .Select(point => point.SemanticLayer)
            .Concat(snapshot.Components.Select(component => component.SemanticLayer))
            .Where(layer => !string.IsNullOrWhiteSpace(layer))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();

        return
        [
            CreateGroup(
                "original-architecture",
                AutomationPreviewCategory.Original,
                "Arquitetura original",
                "Entidades gráficas preservadas como fundo da comparação.",
                "DWG verificado pelo visualizador CAD",
                "Preservar integralmente",
                cad?.Primitives.Count ?? 0,
                false,
                false,
                [],
                [],
                stored),
            CreateGroup(
                "detected-electrical",
                AutomationPreviewCategory.Detected,
                "Pontos elétricos detectados",
                "Símbolos reconhecidos pela cadeia v01–v04 e confirmados no v07.",
                "mascara_previsualizar_v04.lsp + mascara_exportar_v07.lsp",
                "Incluir na identificação elétrica",
                electrical.Length,
                false,
                electrical.Length > 0,
                electrical,
                [],
                stored),
            CreateGroup(
                "detected-hydraulic",
                AutomationPreviewCategory.Detected,
                "Pontos hidráulicos detectados",
                "Símbolos hidráulicos reconhecidos e exportados pela automação histórica.",
                "mascara_previsualizar_v04.lsp + mascara_exportar_v07.lsp",
                "Incluir na identificação hidráulica",
                hydraulic.Length,
                false,
                hydraulic.Length > 0,
                hydraulic,
                [],
                stored),
            CreateGroup(
                "detected-components",
                AutomationPreviewCategory.Detected,
                "Componentes associados",
                "Textos e elementos gráficos vinculados aos pontos encontrados.",
                "mascara_previsualizar_v04.lsp",
                "Manter a associação semântica",
                snapshot.Components.Count,
                false,
                snapshot.Components.Count > 0,
                [],
                snapshot.Components.Select(component => component.Id).ToArray(),
                stored),
            CreateGroup(
                "corrected-review",
                AutomationPreviewCategory.Corrected,
                "Correções humanas",
                "Pontos corrigidos no editor detalhado e preservados na proposta.",
                "Central de revisão 11.6D",
                "Substituir a classificação detectada pela correção aprovada",
                corrected.Length,
                false,
                corrected.Length > 0,
                corrected,
                [],
                stored),
            CreateGroup(
                "proposed-layers",
                AutomationPreviewCategory.Proposed,
                "Camadas semânticas propostas",
                "Organização que a rotina v05 aplicaria somente numa cópia técnica.",
                "mascara_camadas_v05.lsp",
                "Criar e organizar camadas na futura aplicação",
                layerCount,
                true,
                layerCount > 0,
                snapshot.Points.Select(point => point.Id).ToArray(),
                [],
                stored),
            CreateGroup(
                "proposed-mask",
                AutomationPreviewCategory.Proposed,
                "Máscara automática proposta",
                "Conjunto completo de pontos que formará a máscara supervisionada.",
                "mascara_camadas_v05.lsp + mascara_limpeza_v06.lsp",
                "Gerar máscara somente sobre cópia técnica",
                snapshot.Points.Count,
                true,
                snapshot.Points.Count > 0,
                snapshot.Points.Select(point => point.Id).ToArray(),
                [],
                stored),
            CreateGroup(
                "ignored-pending",
                AutomationPreviewCategory.Ignored,
                "Pendências temporariamente ignoradas",
                "Itens ainda ambíguos permanecem fora de qualquer aplicação automática.",
                "Fila de pendências da revisão semântica",
                "Não alterar até revisão humana",
                pending.Length,
                false,
                pending.Length > 0,
                pending,
                [],
                stored)
        ];
    }

    private static AutomationPreviewGroup CreateGroup(
        string id,
        AutomationPreviewCategory category,
        string name,
        string description,
        string source,
        string effect,
        int count,
        bool writes,
        bool requiresDecision,
        IReadOnlyList<Guid> pointIds,
        IReadOnlyList<Guid> componentIds,
        IReadOnlyDictionary<string, AutomationPreviewDecision> stored)
    {
        stored.TryGetValue(id, out var decision);
        return new AutomationPreviewGroup(
            id,
            category,
            name,
            description,
            source,
            effect,
            count,
            writes,
            requiresDecision,
            requiresDecision
                ? decision?.Status ?? AutomationPreviewDecisionStatus.Pending
                : AutomationPreviewDecisionStatus.Protected,
            decision?.Note,
            decision?.DecidedAt,
            pointIds,
            componentIds);
    }

    private static AutomationPreviewPlan CreatePlan(
        string id,
        Guid projectId,
        Guid datasetId,
        string sourcePath,
        string sourceSha256,
        string datasetFingerprint,
        IReadOnlyList<AutomationPreviewGroup> groups,
        bool integrity,
        CadVisualizationSnapshot? cad)
    {
        var required = groups.Where(group => group.RequiresDecision).ToArray();
        var approved = required.Count(
            group => group.Decision == AutomationPreviewDecisionStatus.Approved);
        var rejected = required.Count(
            group => group.Decision == AutomationPreviewDecisionStatus.Rejected);
        var pending = required.Count(
            group => group.Decision == AutomationPreviewDecisionStatus.Pending);
        var points = groups.First(group => group.Id == "proposed-mask").ItemCount;
        var layers = groups.First(group => group.Id == "proposed-layers").ItemCount;
        return new AutomationPreviewPlan(
            id,
            projectId,
            datasetId,
            sourcePath,
            sourceSha256,
            datasetFingerprint,
            DateTimeOffset.Now,
            groups,
            integrity,
            required.Length,
            approved,
            rejected,
            pending,
            integrity && pending == 0 && rejected == 0,
            $"{cad?.Primitives.Count ?? 0} primitivas arquitetônicas preservadas; "
            + "nenhum elemento original alterado.",
            $"{points} pontos e {layers} camadas no plano visual; "
            + $"{approved} grupo(s) aprovado(s), {rejected} rejeitado(s) "
            + $"e {pending} pendente(s).",
            "Pré-visualização e decisões somente; AutoLISP e escrita no DWG "
            + "continuam bloqueados.");
    }

    private static string CalculatePlanId(
        Guid projectId,
        Guid datasetId,
        string datasetFingerprint,
        string sourceHash)
    {
        var payload = Encoding.UTF8.GetBytes(
            $"11.6G|{projectId:D}|{datasetId:D}|{datasetFingerprint}|{sourceHash}");
        return Convert.ToHexString(SHA256.HashData(payload));
    }

    private static bool ConfirmSourceIntegrity(string path, string expectedHash)
    {
        if (!File.Exists(path) || expectedHash.Length != 64)
        {
            return false;
        }

        using var stream = File.OpenRead(path);
        var current = Convert.ToHexString(SHA256.HashData(stream));
        return current.Equals(expectedHash, StringComparison.OrdinalIgnoreCase);
    }
}
