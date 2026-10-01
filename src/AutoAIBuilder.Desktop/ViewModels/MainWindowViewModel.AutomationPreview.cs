using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Input;
using AutoAIBuilder.Application.Automation.Preview;
using AutoAIBuilder.Domain.Semantics;

namespace AutoAIBuilder.Desktop.ViewModels;

public sealed partial class MainWindowViewModel
{
    private AutomationPreviewPlan? _automationPreviewPlan;
    private AutomationPreviewGroupItemViewModel? _selectedAutomationPreviewGroup;
    private string _automationPreviewDecisionNote = string.Empty;
    private string _automationPreviewStatus = "AGUARDANDO BASE SEMÂNTICA";
    private string _automationPreviewStatusAccent = "#627087";
    private string _automationPreviewSummary =
        "Selecione um projeto com dados semânticos e planta verificada.";

    public ObservableCollection<AutomationPreviewGroupItemViewModel>
        AutomationPreviewGroups { get; } = [];

    public ObservableCollection<CadOverlayPointViewModel>
        AutomationPreviewOverlayPoints { get; } = [];

    public ObservableCollection<AutomationPreviewAuditItemViewModel>
        AutomationPreviewAudit { get; } = [];

    public ICommand RefreshAutomationPreviewCommand { get; }

    public ICommand ApproveAutomationPreviewGroupCommand { get; }

    public ICommand RejectAutomationPreviewGroupCommand { get; }

    public AutomationPreviewGroupItemViewModel?
        SelectedAutomationPreviewGroup
    {
        get => _selectedAutomationPreviewGroup;
        set
        {
            if (SetField(ref _selectedAutomationPreviewGroup, value))
            {
                AutomationPreviewDecisionNote = value?.DecisionNote ?? string.Empty;
                RefreshAutomationPreviewOverlays();
                OnPropertyChanged(nameof(AutomationPreviewSelectionSummary));
                (ApproveAutomationPreviewGroupCommand
                    as RelayCommand<AutomationPreviewGroupItemViewModel>)?
                    .RaiseCanExecuteChanged();
                (RejectAutomationPreviewGroupCommand
                    as RelayCommand<AutomationPreviewGroupItemViewModel>)?
                    .RaiseCanExecuteChanged();
            }
        }
    }

    public string AutomationPreviewDecisionNote
    {
        get => _automationPreviewDecisionNote;
        set => SetField(ref _automationPreviewDecisionNote, value);
    }

    public string AutomationPreviewStatus
    {
        get => _automationPreviewStatus;
        private set => SetField(ref _automationPreviewStatus, value);
    }

    public string AutomationPreviewStatusAccent
    {
        get => _automationPreviewStatusAccent;
        private set => SetField(ref _automationPreviewStatusAccent, value);
    }

    public string AutomationPreviewSummary
    {
        get => _automationPreviewSummary;
        private set => SetField(ref _automationPreviewSummary, value);
    }

    public string AutomationPreviewPlanId =>
        _automationPreviewPlan is null
            ? "Plano ainda não gerado"
            : $"{_automationPreviewPlan.Id[..16]}…";

    public string AutomationPreviewBeforeSummary =>
        _automationPreviewPlan?.BeforeSummary
        ?? "Planta arquitetônica ainda não carregada.";

    public string AutomationPreviewAfterSummary =>
        _automationPreviewPlan?.AfterSummary
        ?? "Proposta ainda não gerada.";

    public string AutomationPreviewSafety =>
        _automationPreviewPlan?.SafetyStatement
        ?? "Nenhum DWG será alterado nesta etapa.";

    public string AutomationPreviewIntegrity =>
        _automationPreviewPlan?.SourceIntegrityConfirmed == true
            ? "DWG ORIGINAL ÍNTEGRO"
            : "INTEGRIDADE DO DWG NÃO CONFIRMADA";

    public string AutomationPreviewIntegrityAccent =>
        _automationPreviewPlan?.SourceIntegrityConfirmed == true
            ? "#36D17C"
            : "#F8C33A";

    public int AutomationPreviewApprovedCount =>
        _automationPreviewPlan?.ApprovedCount ?? 0;

    public int AutomationPreviewRejectedCount =>
        _automationPreviewPlan?.RejectedCount ?? 0;

    public int AutomationPreviewPendingCount =>
        _automationPreviewPlan?.PendingCount ?? 0;

    public string AutomationPreviewReadiness =>
        _automationPreviewPlan?.IsReadyForSupervisedExecution == true
            ? "PLANO APROVADO PARA O 11.6H"
            : _automationPreviewPlan?.RejectedCount > 0
                ? "PLANO COM GRUPOS REJEITADOS"
                : "AGUARDANDO DECISÕES";

    public string AutomationPreviewReadinessAccent =>
        _automationPreviewPlan?.IsReadyForSupervisedExecution == true
            ? "#36D17C"
            : _automationPreviewPlan?.RejectedCount > 0
                ? "#FF5D68"
                : "#F8C33A";

    public string AutomationPreviewSelectionSummary =>
        SelectedAutomationPreviewGroup is null
            ? "Visão combinada: detectados, corrigidos e pendências."
            : $"{SelectedAutomationPreviewGroup.Name} • "
              + $"{SelectedAutomationPreviewGroup.ItemCount} item(ns) • "
              + SelectedAutomationPreviewGroup.IntendedEffect;

    private void RefreshAutomationPreview()
    {
        if (SelectedProject is null || _semanticSnapshot.Dataset is null)
        {
            ClearAutomationPreview(
                "Selecione um projeto com uma base semântica importada.");
            return;
        }

        try
        {
            var selectedGroupId = SelectedAutomationPreviewGroup?.Id;
            _automationPreviewPlan = _automationPreviewService.Build(
                SelectedProject.Id,
                _semanticSnapshot,
                CadVisualization);
            ReplaceItems(
                AutomationPreviewGroups,
                _automationPreviewPlan.Groups.Select(
                    AutomationPreviewGroupItemViewModel.From));
            ReplaceItems(
                AutomationPreviewAudit,
                _automationPreviewPlan.Groups
                    .Where(group => group.DecidedAt is not null)
                    .OrderByDescending(group => group.DecidedAt)
                    .Select(AutomationPreviewAuditItemViewModel.From));
            SelectedAutomationPreviewGroup =
                AutomationPreviewGroups.FirstOrDefault(
                    group => group.Id.Equals(
                        selectedGroupId,
                        StringComparison.OrdinalIgnoreCase));
            AutomationPreviewStatus = "PRÉ-VISUALIZAÇÃO GERADA";
            AutomationPreviewStatusAccent = "#36D17C";
            AutomationPreviewSummary = _automationPreviewPlan.AfterSummary;
            NotifyAutomationPreviewChanged();
            RefreshAutomationPreviewOverlays();
            StatusMessage =
                "Prévia 11.6G atualizada; nenhuma alteração CAD foi executada.";
        }
        catch (Exception exception) when (
            exception is ArgumentException
                or InvalidDataException
                or InvalidOperationException
                or IOException
                or UnauthorizedAccessException)
        {
            ClearAutomationPreview(exception.Message);
            TryWriteDiagnostic(
                AutoAIBuilder.Application.Diagnostics.DiagnosticLevel.Error,
                "AutomationPreview",
                "Não foi possível gerar a pré-visualização 11.6G.",
                exception);
        }
    }

    private void DecideAutomationPreviewGroup(
        AutomationPreviewGroupItemViewModel? group,
        AutomationPreviewDecisionStatus status)
    {
        if (_automationPreviewPlan is null || group?.CanDecide != true)
        {
            return;
        }

        try
        {
            var groupId = group.Id;
            _automationPreviewPlan = _automationPreviewService.Decide(
                _automationPreviewPlan,
                groupId,
                status,
                AutomationPreviewDecisionNote);
            ReplaceItems(
                AutomationPreviewGroups,
                _automationPreviewPlan.Groups.Select(
                    AutomationPreviewGroupItemViewModel.From));
            ReplaceItems(
                AutomationPreviewAudit,
                _automationPreviewPlan.Groups
                    .Where(item => item.DecidedAt is not null)
                    .OrderByDescending(item => item.DecidedAt)
                    .Select(AutomationPreviewAuditItemViewModel.From));
            SelectedAutomationPreviewGroup =
                AutomationPreviewGroups.FirstOrDefault(
                    item => item.Id.Equals(
                        groupId,
                        StringComparison.OrdinalIgnoreCase));
            AutomationPreviewSummary = _automationPreviewPlan.AfterSummary;
            NotifyAutomationPreviewChanged();
            RefreshAutomationPreviewOverlays();
            StatusMessage = status == AutomationPreviewDecisionStatus.Approved
                ? $"Grupo “{group.Name}” aprovado e auditado."
                : $"Grupo “{group.Name}” rejeitado e auditado.";
        }
        catch (Exception exception) when (
            exception is ArgumentException
                or InvalidDataException
                or InvalidOperationException)
        {
            StatusMessage =
                $"Não foi possível registrar a decisão: {exception.Message}";
        }
    }

    private void RefreshAutomationPreviewOverlays()
    {
        if (_automationPreviewPlan is null)
        {
            ReplaceItems(AutomationPreviewOverlayPoints, []);
            return;
        }

        var selected = SelectedAutomationPreviewGroup;
        if (selected is not null)
        {
            var pointIds = selected.PointIds.ToHashSet();
            var componentIds = selected.ComponentIds.ToHashSet();
            var accent = selected.DecisionKey
                == AutomationPreviewDecisionStatus.Rejected
                    ? "#FF5D68"
                    : selected.Accent;
            var points = _semanticSnapshot.Points
                .Where(point => pointIds.Contains(point.Id))
                .Select(point => new CadOverlayPointViewModel(
                    point.Id,
                    point.ExternalId,
                    point.CenterX,
                    point.CenterY,
                    accent,
                    $"{selected.Category} • {point.ExternalId} • "
                    + selected.Decision));
            var components = _semanticSnapshot.Components
                .Where(component => componentIds.Contains(component.Id))
                .Select(component => new CadOverlayPointViewModel(
                    component.Id,
                    component.ExternalId,
                    component.CenterX,
                    component.CenterY,
                    accent,
                    $"{selected.Category} • {component.ExternalId} • "
                    + selected.Decision));
            ReplaceItems(
                AutomationPreviewOverlayPoints,
                points.Concat(components));
            return;
        }

        ReplaceItems(
            AutomationPreviewOverlayPoints,
            _semanticSnapshot.Points.Select(point =>
            {
                var (accent, category) = point.ReviewStatus switch
                {
                    SemanticReviewStatus.Corrected => ("#A78BFA", "CORRIGIDO"),
                    SemanticReviewStatus.NeedsReview => ("#F8C33A", "IGNORADO"),
                    _ when point.Discipline == SemanticDiscipline.Hydraulic =>
                        ("#28D7C0", "DETECTADO"),
                    _ => ("#2C9BFF", "DETECTADO")
                };
                return new CadOverlayPointViewModel(
                    point.Id,
                    point.ExternalId,
                    point.CenterX,
                    point.CenterY,
                    accent,
                    $"{category} • {point.ExternalId} • {point.Description}");
            }));
    }

    private void ClearAutomationPreview(string message)
    {
        _automationPreviewPlan = null;
        SelectedAutomationPreviewGroup = null;
        ReplaceItems(AutomationPreviewGroups, []);
        ReplaceItems(AutomationPreviewOverlayPoints, []);
        ReplaceItems(AutomationPreviewAudit, []);
        AutomationPreviewStatus = "PRÉVIA INDISPONÍVEL";
        AutomationPreviewStatusAccent = "#F8C33A";
        AutomationPreviewSummary = message;
        NotifyAutomationPreviewChanged();
    }

    private void NotifyAutomationPreviewChanged()
    {
        OnPropertyChanged(nameof(AutomationPreviewPlanId));
        OnPropertyChanged(nameof(AutomationPreviewBeforeSummary));
        OnPropertyChanged(nameof(AutomationPreviewAfterSummary));
        OnPropertyChanged(nameof(AutomationPreviewSafety));
        OnPropertyChanged(nameof(AutomationPreviewIntegrity));
        OnPropertyChanged(nameof(AutomationPreviewIntegrityAccent));
        OnPropertyChanged(nameof(AutomationPreviewApprovedCount));
        OnPropertyChanged(nameof(AutomationPreviewRejectedCount));
        OnPropertyChanged(nameof(AutomationPreviewPendingCount));
        OnPropertyChanged(nameof(AutomationPreviewReadiness));
        OnPropertyChanged(nameof(AutomationPreviewReadinessAccent));
        OnPropertyChanged(nameof(AutomationPreviewSelectionSummary));
    }
}

public sealed record AutomationPreviewGroupItemViewModel(
    string Id,
    string Category,
    string Name,
    string Description,
    string Source,
    string IntendedEffect,
    int ItemCount,
    string Decision,
    AutomationPreviewDecisionStatus DecisionKey,
    string DecisionNote,
    string Accent,
    string DecisionAccent,
    string FutureEffect,
    bool CanDecide,
    IReadOnlyList<Guid> PointIds,
    IReadOnlyList<Guid> ComponentIds)
{
    public static AutomationPreviewGroupItemViewModel From(
        AutomationPreviewGroup group) =>
        new(
            group.Id,
            group.Category switch
            {
                AutomationPreviewCategory.Original => "ORIGINAL",
                AutomationPreviewCategory.Detected => "DETECTADO",
                AutomationPreviewCategory.Proposed => "PROPOSTO",
                AutomationPreviewCategory.Corrected => "CORRIGIDO",
                _ => "IGNORADO"
            },
            group.Name,
            group.Description,
            group.Source,
            group.IntendedEffect,
            group.ItemCount,
            group.Decision switch
            {
                AutomationPreviewDecisionStatus.Approved => "APROVADO",
                AutomationPreviewDecisionStatus.Rejected => "REJEITADO",
                AutomationPreviewDecisionStatus.Protected => "PROTEGIDO",
                _ => "PENDENTE"
            },
            group.Decision,
            group.DecisionNote ?? string.Empty,
            GetCategoryAccent(group.Category),
            group.Decision switch
            {
                AutomationPreviewDecisionStatus.Approved => "#36D17C",
                AutomationPreviewDecisionStatus.Rejected => "#FF5D68",
                AutomationPreviewDecisionStatus.Protected => "#8FA2B8",
                _ => "#F8C33A"
            },
            group.WritesDuringFutureApply
                ? "ALTERARIA A CÓPIA TÉCNICA NO 11.6H"
                : "NÃO ESCREVE NO DESENHO",
            group.RequiresDecision,
            group.PointIds,
            group.ComponentIds);

    private static string GetCategoryAccent(
        AutomationPreviewCategory category) => category switch
        {
            AutomationPreviewCategory.Original => "#8FA2B8",
            AutomationPreviewCategory.Detected => "#2C9BFF",
            AutomationPreviewCategory.Proposed => "#36D17C",
            AutomationPreviewCategory.Corrected => "#A78BFA",
            _ => "#F8C33A"
        };
}

public sealed record AutomationPreviewAuditItemViewModel(
    string Group,
    string Decision,
    string Accent,
    string Note,
    string DecidedAt)
{
    public static AutomationPreviewAuditItemViewModel From(
        AutomationPreviewGroup group) =>
        new(
            group.Name,
            group.Decision == AutomationPreviewDecisionStatus.Approved
                ? "APROVADO"
                : "REJEITADO",
            group.Decision == AutomationPreviewDecisionStatus.Approved
                ? "#36D17C"
                : "#FF5D68",
            group.DecisionNote ?? "Sem observação",
            group.DecidedAt?.ToLocalTime().ToString("dd/MM/yyyy HH:mm:ss")
            ?? "—");
}
