using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Input;
using AutoAIBuilder.Application.Automation.Adapters;
using AutoAIBuilder.Application.Automation.Catalog;
using AutoAIBuilder.Application.Automation.Orchestration;
using AutoAIBuilder.Application.Automation.Validation;
using AutoAIBuilder.Application.Diagnostics;
using AutoAIBuilder.Application.History;

namespace AutoAIBuilder.Desktop.ViewModels;

public sealed partial class MainWindowViewModel
{
    private AutomationMaskPackagePreview? _maskPackagePreview;
    private string _maskContractPath = string.Empty;
    private string _ruleCatalogPath = string.Empty;
    private string _maskCatalogStage = "AGUARDANDO ARQUIVOS";
    private string _maskCatalogStageAccent = "#627087";
    private string _maskCatalogSummary =
        "Selecione o contrato da máscara e seu catálogo de regras.";
    private string _maskPreviewName = "Nenhuma máscara analisada";
    private string _maskPreviewIdentity = "—";
    private string _maskPreviewCatalogIdentity = "—";
    private string _maskPreviewDescription = string.Empty;
    private string _maskPreviewSha256 = "Ainda não calculado";
    private string _maskPreviewMetrics = "—";
    private string _maskPreviewExtensions = "—";
    private string _maskPreviewDependencies = "—";
    private bool _isMaskCatalogBusy;
    private bool _maskPackageImported;

    public ObservableCollection<AutomationMaskCatalogItemViewModel>
        MaskCatalogEntries
    { get; } = [];

    public ObservableCollection<AutomationValidationIssueItemViewModel>
        MaskImportIssues
    { get; } = [];

    public ObservableCollection<AutomationAdapterItemViewModel>
        RegisteredAutomationAdapters
    { get; } = [];

    public ObservableCollection<AutomationIntegrationAssessmentItemViewModel>
        IntegrationAssessments
    { get; } = [];

    public ICommand ChooseMaskContractCommand { get; }

    public ICommand ChooseRuleCatalogCommand { get; }

    public ICommand AnalyzeMaskPackageCommand { get; }

    public ICommand ImportMaskPackageCommand { get; }

    public ICommand ClearMaskPackageCommand { get; }

    public ICommand ToggleMaskCatalogEntryCommand { get; }

    public ICommand AssessMaskIntegrationCommand { get; }

    public string MaskContractPath
    {
        get => _maskContractPath;
        private set
        {
            if (SetField(ref _maskContractPath, value))
            {
                OnPropertyChanged(nameof(MaskContractPathDisplay));
                InvalidateMaskPackagePreview(
                    "Contrato da máscara selecionado; analise o pacote.");
            }
        }
    }

    public string MaskContractPathDisplay =>
        string.IsNullOrWhiteSpace(MaskContractPath)
            ? "Nenhum contrato de máscara selecionado"
            : MaskContractPath;

    public string RuleCatalogPath
    {
        get => _ruleCatalogPath;
        private set
        {
            if (SetField(ref _ruleCatalogPath, value))
            {
                OnPropertyChanged(nameof(RuleCatalogPathDisplay));
                InvalidateMaskPackagePreview(
                    "Catálogo de regras selecionado; analise o pacote.");
            }
        }
    }

    public string RuleCatalogPathDisplay =>
        string.IsNullOrWhiteSpace(RuleCatalogPath)
            ? "Nenhum catálogo de regras selecionado"
            : RuleCatalogPath;

    public string MaskCatalogStage
    {
        get => _maskCatalogStage;
        private set => SetField(ref _maskCatalogStage, value);
    }

    public string MaskCatalogStageAccent
    {
        get => _maskCatalogStageAccent;
        private set => SetField(ref _maskCatalogStageAccent, value);
    }

    public string MaskCatalogSummary
    {
        get => _maskCatalogSummary;
        private set => SetField(ref _maskCatalogSummary, value);
    }

    public string MaskPreviewName
    {
        get => _maskPreviewName;
        private set => SetField(ref _maskPreviewName, value);
    }

    public string MaskPreviewIdentity
    {
        get => _maskPreviewIdentity;
        private set => SetField(ref _maskPreviewIdentity, value);
    }

    public string MaskPreviewCatalogIdentity
    {
        get => _maskPreviewCatalogIdentity;
        private set => SetField(ref _maskPreviewCatalogIdentity, value);
    }

    public string MaskPreviewDescription
    {
        get => _maskPreviewDescription;
        private set => SetField(ref _maskPreviewDescription, value);
    }

    public string MaskPreviewSha256
    {
        get => _maskPreviewSha256;
        private set => SetField(ref _maskPreviewSha256, value);
    }

    public string MaskPreviewMetrics
    {
        get => _maskPreviewMetrics;
        private set => SetField(ref _maskPreviewMetrics, value);
    }

    public string MaskPreviewExtensions
    {
        get => _maskPreviewExtensions;
        private set => SetField(ref _maskPreviewExtensions, value);
    }

    public string MaskPreviewDependencies
    {
        get => _maskPreviewDependencies;
        private set => SetField(ref _maskPreviewDependencies, value);
    }

    public bool IsMaskCatalogBusy
    {
        get => _isMaskCatalogBusy;
        private set
        {
            if (SetField(ref _isMaskCatalogBusy, value))
            {
                OnPropertyChanged(nameof(MaskCatalogBusyVisibility));
                RaiseMaskCatalogCommandStates();
            }
        }
    }

    public int ImportedMaskCount => MaskCatalogEntries.Count;

    public int ActiveMaskCount =>
        MaskCatalogEntries.Count(entry => entry.IsActive);

    public int RegisteredAdapterCount =>
        RegisteredAutomationAdapters.Count;

    public string MaskCatalogCountSummary =>
        $"{ImportedMaskCount} versão(ões) catalogada(s) • "
        + $"{ActiveMaskCount} ativa(s) • "
        + $"{RegisteredAdapterCount} adaptador(es) interno(s)";

    public string AdapterRegistrySummary =>
        $"{RegisteredAdapterCount} adaptador(es) compilado(s) com o aplicativo; "
        + "nenhum carregamento dinâmico permitido";

    public string IntegrationAuditSummary =>
        IntegrationAssessments.Count == 0
            ? "Nenhuma avaliação de integração registrada."
            : $"{IntegrationAssessments.Count} avaliação(ões) recente(s); "
              + "todas sem execução de máscara catalogada.";

    public Visibility MaskPreviewVisibility =>
        _maskPackagePreview is null
            ? Visibility.Collapsed
            : Visibility.Visible;

    public Visibility MaskPreviewEmptyVisibility =>
        _maskPackagePreview is null
            ? Visibility.Visible
            : Visibility.Collapsed;

    public Visibility EmptyMaskCatalogVisibility =>
        MaskCatalogEntries.Count == 0
            ? Visibility.Visible
            : Visibility.Collapsed;

    public Visibility MaskCatalogBusyVisibility =>
        IsMaskCatalogBusy
            ? Visibility.Visible
            : Visibility.Collapsed;

    private void ChooseMaskContract()
    {
        var path = _filePicker.PickAutomationMaskContract();
        if (string.IsNullOrWhiteSpace(path))
        {
            MaskCatalogSummary =
                "Seleção do contrato cancelada; nada foi alterado.";
            return;
        }

        MaskContractPath = Path.GetFullPath(path);
    }

    private void ChooseRuleCatalog()
    {
        var path = _filePicker.PickAutomationRuleCatalog();
        if (string.IsNullOrWhiteSpace(path))
        {
            MaskCatalogSummary =
                "Seleção do catálogo cancelada; nada foi alterado.";
            return;
        }

        RuleCatalogPath = Path.GetFullPath(path);
    }

    private async Task AnalyzeMaskPackageAsync(
        CancellationToken cancellationToken)
    {
        IsMaskCatalogBusy = true;
        MaskCatalogStage = "ANALISANDO";
        MaskCatalogStageAccent = "#2C9BFF";
        MaskCatalogSummary =
            "Lendo os JSONs e conferindo schemas, versões e conflitos.";
        try
        {
            var preview = await _automationMaskCatalogService.AnalyzeAsync(
                MaskContractPath,
                RuleCatalogPath,
                cancellationToken);
            ApplyMaskPackagePreview(preview);
        }
        catch (OperationCanceledException)
        {
            MaskCatalogStage = "ANÁLISE CANCELADA";
            MaskCatalogStageAccent = "#F8C33A";
            MaskCatalogSummary =
                "A análise foi cancelada sem importar conteúdo.";
            throw;
        }
        finally
        {
            IsMaskCatalogBusy = false;
        }
    }

    private void ImportMaskPackage()
    {
        if (_maskPackagePreview is null
            || !CanImportMaskPackage())
        {
            MaskCatalogSummary =
                "Analise e aprove um pacote antes de importá-lo.";
            return;
        }

        try
        {
            var result = _automationMaskCatalogService.Import(
                _maskPackagePreview);
            if (!result.Succeeded || result.Entry is null)
            {
                MaskCatalogStage = "IMPORTAÇÃO BLOQUEADA";
                MaskCatalogStageAccent = "#FF5D68";
                MaskCatalogSummary = result.Summary;
                return;
            }

            _maskPackageImported = true;
            MaskCatalogStage = "IMPORTADA — INATIVA";
            MaskCatalogStageAccent = "#36D17C";
            MaskCatalogSummary =
                "Contrato armazenado no catálogo local. O registro interno só "
                + "aceitará identidade, versão e SHA-256 exatos; nada foi executado.";
            RefreshMaskCatalog();
            TryRecordActivity(
                "Máscaras",
                "Máscara importada",
                $"“{result.Entry.MaskName}” {result.Entry.MaskVersion}; "
                + $"SHA-256 {result.Entry.ContentSha256}. Permanece inativa.",
                ActivityLevel.Success,
                SelectedProject);
            StatusMessage =
                $"Máscara “{result.Entry.MaskName}” importada de forma inativa.";
            RaiseMaskCatalogCommandStates();
        }
        catch (Exception exception) when (
            exception is InvalidDataException
                or InvalidOperationException
                or IOException
                or UnauthorizedAccessException)
        {
            ReportMaskCatalogFailure(exception);
        }
    }

    private void ToggleMaskCatalogEntry(
        AutomationMaskCatalogItemViewModel? item)
    {
        if (item is null)
        {
            return;
        }

        var activate = !item.IsActive;
        if (!_dialogService.ConfirmMaskCatalogActivation(
                item.Name,
                item.Version,
                activate))
        {
            MaskCatalogSummary =
                "Alteração do catálogo cancelada; nada foi modificado.";
            return;
        }

        try
        {
            var updated = _automationMaskCatalogService.SetActive(
                item.Id,
                activate);
            RefreshMaskCatalog();
            MaskCatalogSummary = activate
                ? "Versão ativada para avaliação de integração. Nenhuma "
                  + "automação foi executada."
                : "Versão desativada e preservada no catálogo.";
            MaskCatalogStage = activate
                ? "ATIVA PARA INTEGRAÇÃO"
                : "VERSÃO DESATIVADA";
            MaskCatalogStageAccent = activate ? "#36D17C" : "#F8C33A";
            TryRecordActivity(
                "Máscaras",
                activate ? "Máscara ativada" : "Máscara desativada",
                $"“{updated.MaskName}” {updated.MaskVersion}; nenhuma execução. "
                + "A resolução exige hash homologado.",
                ActivityLevel.Information,
                SelectedProject);
            StatusMessage = activate
                ? $"Máscara “{updated.MaskName}” selecionada para integração futura."
                : $"Máscara “{updated.MaskName}” desativada.";
        }
        catch (Exception exception) when (
            exception is InvalidDataException
                or InvalidOperationException)
        {
            ReportMaskCatalogFailure(exception);
        }
    }

    private void RefreshMaskCatalog()
    {
        try
        {
            var adapters = _automationOrchestrator.GetRegisteredAdapters();
            ReplaceItems(
                RegisteredAutomationAdapters,
                adapters.Select(AutomationAdapterItemViewModel.From));
            ReplaceItems(
                MaskCatalogEntries,
                _automationMaskCatalogService.GetAll().Select(
                    entry => AutomationMaskCatalogItemViewModel.From(
                        entry,
                        adapters)));
            ReplaceItems(
                IntegrationAssessments,
                _automationOrchestrator.GetRecentAssessments(20).Select(
                    AutomationIntegrationAssessmentItemViewModel.From));
            OnPropertyChanged(nameof(ImportedMaskCount));
            OnPropertyChanged(nameof(ActiveMaskCount));
            OnPropertyChanged(nameof(RegisteredAdapterCount));
            OnPropertyChanged(nameof(MaskCatalogCountSummary));
            OnPropertyChanged(nameof(AdapterRegistrySummary));
            OnPropertyChanged(nameof(IntegrationAuditSummary));
            OnPropertyChanged(nameof(EmptyMaskCatalogVisibility));
            RaiseMaskCatalogCommandStates();
        }
        catch (Exception exception) when (
            exception is InvalidDataException
                or InvalidOperationException)
        {
            ReportMaskCatalogFailure(exception);
        }
    }

    private void AssessMaskIntegration(
        AutomationMaskCatalogItemViewModel? item)
    {
        if (item is null)
        {
            return;
        }

        if (SelectedProject is null)
        {
            MaskCatalogStage = "AVALIAÇÃO BLOQUEADA";
            MaskCatalogStageAccent = "#F8C33A";
            MaskCatalogSummary =
                "Selecione um projeto ativo para registrar a avaliação.";
            return;
        }

        try
        {
            var assessment = _automationOrchestrator.AssessIntegration(
                SelectedProject.Id,
                item.Id);
            RefreshMaskCatalog();
            MaskCatalogStage = GetAssessmentLabel(assessment.Status);
            MaskCatalogStageAccent =
                GetAssessmentAccent(assessment.Status);
            MaskCatalogSummary = assessment.Summary
                + " Nenhum adaptador foi invocado.";
            TryRecordActivity(
                "Máscaras",
                "Integração avaliada",
                $"{item.Identity}: {MaskCatalogStage}. "
                + "Nenhum arquivo foi criado.",
                assessment.IsReady
                    ? ActivityLevel.Success
                    : ActivityLevel.Information,
                SelectedProject);
            StatusMessage =
                $"Integração de “{item.Name}” avaliada sem execução.";
        }
        catch (Exception exception) when (
            exception is InvalidDataException
                or InvalidOperationException
                or ArgumentException)
        {
            ReportMaskCatalogFailure(exception);
        }
    }

    private void ApplyMaskPackagePreview(
        AutomationMaskPackagePreview preview)
    {
        _maskPackagePreview = preview;
        _maskPackageImported = false;
        ReplaceItems(
            MaskImportIssues,
            preview.Issues.Select(AutomationValidationIssueItemViewModel.From));
        MaskPreviewName = preview.Mask?.Name
            ?? "Contrato de máscara inválido";
        MaskPreviewIdentity = preview.Mask is null
            ? "—"
            : $"{preview.Mask.Id}@{preview.Mask.Version}";
        MaskPreviewCatalogIdentity = preview.RuleCatalog is null
            ? "—"
            : $"{preview.RuleCatalog.CatalogId}@"
              + preview.RuleCatalog.Version;
        MaskPreviewDescription = preview.Mask?.Description ?? string.Empty;
        MaskPreviewSha256 = string.IsNullOrWhiteSpace(preview.ContentSha256)
            ? "Não calculado"
            : preview.ContentSha256;
        MaskPreviewMetrics = preview.Mask is null
            || preview.RuleCatalog is null
                ? "—"
                : $"{preview.RuleCatalog.Rules.Count} regras • "
                  + $"{preview.Mask.Parameters.Count} parâmetros • "
                  + $"{preview.Mask.Outputs.Count} saídas";
        MaskPreviewExtensions = preview.Mask is null
            ? "—"
            : string.Join(", ", preview.Mask.AcceptedExtensions);
        MaskPreviewDependencies = preview.Mask is null
            ? "—"
            : preview.Mask.Dependencies.Count == 0
                ? "Nenhuma dependência declarada"
                : string.Join(
                    ", ",
                    preview.Mask.Dependencies.Select(
                        dependency =>
                            $"{dependency.Id} ≥ {dependency.MinimumVersion}"));

        if (preview.Conflict
            == AutomationMaskCatalogConflict.AlreadyImported)
        {
            MaskCatalogStage = "JÁ CATALOGADA";
            MaskCatalogStageAccent = "#2C9BFF";
            MaskCatalogSummary =
                "O pacote é idêntico ao registro existente; nenhuma "
                + "duplicação será criada.";
        }
        else if (preview.IsValid)
        {
            MaskCatalogStage = "ANÁLISE APROVADA";
            MaskCatalogStageAccent = "#36D17C";
            MaskCatalogSummary =
                "Pacote compatível e sem conflito. A importação o manterá "
                + "inativo e não executará código.";
        }
        else
        {
            MaskCatalogStage = "PACOTE BLOQUEADO";
            MaskCatalogStageAccent = "#FF5D68";
            MaskCatalogSummary =
                "Corrija os erros do contrato antes de tentar importá-lo.";
        }

        OnPropertyChanged(nameof(MaskPreviewVisibility));
        OnPropertyChanged(nameof(MaskPreviewEmptyVisibility));
        RaiseMaskCatalogCommandStates();
    }

    private void ClearMaskPackage()
    {
        _maskContractPath = string.Empty;
        _ruleCatalogPath = string.Empty;
        OnPropertyChanged(nameof(MaskContractPath));
        OnPropertyChanged(nameof(MaskContractPathDisplay));
        OnPropertyChanged(nameof(RuleCatalogPath));
        OnPropertyChanged(nameof(RuleCatalogPathDisplay));
        InvalidateMaskPackagePreview(
            "Seleções limpas; o catálogo instalado foi preservado.");
    }

    private void InvalidateMaskPackagePreview(string summary)
    {
        _maskPackagePreview = null;
        _maskPackageImported = false;
        MaskImportIssues.Clear();
        MaskPreviewName = "Nenhuma máscara analisada";
        MaskPreviewIdentity = "—";
        MaskPreviewCatalogIdentity = "—";
        MaskPreviewDescription = string.Empty;
        MaskPreviewSha256 = "Ainda não calculado";
        MaskPreviewMetrics = "—";
        MaskPreviewExtensions = "—";
        MaskPreviewDependencies = "—";
        MaskCatalogStage = "AGUARDANDO ANÁLISE";
        MaskCatalogStageAccent = "#627087";
        MaskCatalogSummary = summary;
        OnPropertyChanged(nameof(MaskPreviewVisibility));
        OnPropertyChanged(nameof(MaskPreviewEmptyVisibility));
        RaiseMaskCatalogCommandStates();
    }

    private bool CanAnalyzeMaskPackage() =>
        !IsMaskCatalogBusy
        && !string.IsNullOrWhiteSpace(MaskContractPath)
        && !string.IsNullOrWhiteSpace(RuleCatalogPath);

    private bool CanImportMaskPackage() =>
        !IsMaskCatalogBusy
        && !_maskPackageImported
        && _maskPackagePreview?.CanImport == true;

    private void RaiseMaskCatalogCommandStates()
    {
        (ChooseMaskContractCommand as RelayCommand)?
            .RaiseCanExecuteChanged();
        (ChooseRuleCatalogCommand as RelayCommand)?
            .RaiseCanExecuteChanged();
        (AnalyzeMaskPackageCommand as AsyncCommand)?
            .RaiseCanExecuteChanged();
        (ImportMaskPackageCommand as RelayCommand)?
            .RaiseCanExecuteChanged();
        (ClearMaskPackageCommand as RelayCommand)?
            .RaiseCanExecuteChanged();
        (ToggleMaskCatalogEntryCommand
            as RelayCommand<AutomationMaskCatalogItemViewModel>)?
            .RaiseCanExecuteChanged();
        (AssessMaskIntegrationCommand
            as RelayCommand<AutomationMaskCatalogItemViewModel>)?
            .RaiseCanExecuteChanged();
    }

    private void ReportMaskCatalogFailure(Exception exception)
    {
        MaskCatalogStage = "FALHA ISOLADA";
        MaskCatalogStageAccent = "#FF5D68";
        MaskCatalogSummary =
            $"Não foi possível atualizar o catálogo: "
            + GetFriendlyMessage(exception);
        StatusMessage = MaskCatalogSummary;
        TryWriteDiagnostic(
            DiagnosticLevel.Error,
            "AutomationMaskCatalog",
            "Falha isolada no catálogo de máscaras.",
            exception);
    }

    private static string GetAssessmentLabel(
        AutomationIntegrationStatus status) =>
        status switch
        {
            AutomationIntegrationStatus.Ready => "INTEGRAÇÃO HOMOLOGADA",
            AutomationIntegrationStatus.InactiveMask => "MÁSCARA INATIVA",
            AutomationIntegrationStatus.InvalidCatalogContract =>
                "SNAPSHOT INVÁLIDO",
            AutomationIntegrationStatus.AdapterNotRegistered =>
                "SEM ADAPTADOR",
            AutomationIntegrationStatus.ContractMismatch =>
                "HASH NÃO HOMOLOGADO",
            AutomationIntegrationStatus.AdapterDisabled =>
                "ADAPTADOR DESABILITADO",
            _ => "EXECUÇÃO DE CATÁLOGO BLOQUEADA"
        };

    private static string GetAssessmentAccent(
        AutomationIntegrationStatus status) =>
        status switch
        {
            AutomationIntegrationStatus.Ready => "#36D17C",
            AutomationIntegrationStatus.InvalidCatalogContract
                or AutomationIntegrationStatus.ContractMismatch => "#FF5D68",
            AutomationIntegrationStatus.InactiveMask => "#627087",
            _ => "#F8C33A"
        };
}

public sealed record AutomationMaskCatalogItemViewModel(
    Guid Id,
    string Name,
    string Version,
    string Identity,
    string Discipline,
    string Description,
    string RuleCatalogIdentity,
    string Metrics,
    string Capabilities,
    string ContentSha256,
    string Sources,
    string ImportedAt,
    bool IsActive,
    string Status,
    string StatusAccent,
    string ToggleAction,
    string IntegrationStatus,
    string IntegrationAccent,
    string IntegrationDetail)
{
    public static AutomationMaskCatalogItemViewModel From(
        AutomationMaskCatalogEntry entry,
        IReadOnlyList<AutomationAdapterDescriptor> adapters)
    {
        var candidates = adapters.Where(
                adapter =>
                    string.Equals(
                        adapter.MaskId,
                        entry.MaskId,
                        StringComparison.Ordinal)
                    && string.Equals(
                        adapter.MaskVersion,
                        entry.MaskVersion,
                        StringComparison.Ordinal))
            .ToArray();
        var exact = candidates.FirstOrDefault(
            adapter => string.Equals(
                adapter.ContractSha256,
                entry.ContentSha256,
                StringComparison.OrdinalIgnoreCase));
        var integration = GetIntegrationDisplay(entry, candidates, exact);

        return new AutomationMaskCatalogItemViewModel(
            entry.Id,
            entry.MaskName,
            entry.MaskVersion,
            $"{entry.MaskId}@{entry.MaskVersion}",
            entry.Discipline,
            entry.Description,
            $"{entry.RuleCatalogId}@{entry.RuleCatalogVersion}",
            $"{entry.RuleCount} regras • {entry.DependencyCount} dependências • "
            + $"{entry.ParameterCount} parâmetros • {entry.OutputCount} saídas",
            $"{(entry.SupportsSimulation ? "Simulação" : "Sem simulação")} • "
            + $"{(entry.IsIdempotent ? "Idempotente" : "Não idempotente")}",
            entry.ContentSha256,
            $"{entry.MaskSourceFileName} + "
            + entry.RuleCatalogSourceFileName,
            entry.ImportedAt.ToLocalTime().ToString("dd/MM/yyyy HH:mm"),
            entry.IsActive,
            entry.IsActive
                ? "ATIVA PARA INTEGRAÇÃO"
                : "INATIVA — SEM EXECUÇÃO",
            entry.IsActive ? "#36D17C" : "#627087",
            entry.IsActive ? "Desativar versão" : "Ativar para integração",
            integration.Status,
            integration.Accent,
            integration.Detail);
    }

    private static (string Status, string Accent, string Detail)
        GetIntegrationDisplay(
            AutomationMaskCatalogEntry entry,
            IReadOnlyList<AutomationAdapterDescriptor> candidates,
            AutomationAdapterDescriptor? exact)
    {
        if (!entry.IsActive)
        {
            return (
                "INATIVA — NÃO RESOLVIDA",
                "#627087",
                "Ative a versão antes de avaliar sua integração.");
        }

        if (candidates.Count == 0)
        {
            return (
                "SEM ADAPTADOR REGISTRADO",
                "#F8C33A",
                "Nenhum componente interno reivindica esta identidade.");
        }

        if (exact is null)
        {
            return (
                "CONTRATO NÃO HOMOLOGADO",
                "#FF5D68",
                "A identidade coincide, mas o SHA-256 difere do contrato "
                + "compilado no adaptador.");
        }

        if (!exact.IsEnabled)
        {
            return (
                "ADAPTADOR DESABILITADO",
                "#F8C33A",
                $"{exact.AdapterId}@{exact.AdapterVersion}");
        }

        return exact.CatalogExecutionEnabled
            ? (
                "PRONTA PARA AVALIAÇÃO",
                "#36D17C",
                $"{exact.AdapterId}@{exact.AdapterVersion}")
            : (
                "CORRESPONDÊNCIA BLOQUEADA",
                "#F8C33A",
                $"{exact.AdapterId}@{exact.AdapterVersion}; a política não "
                + "autoriza execução de catálogo.");
    }
}

public sealed record AutomationAdapterItemViewModel(
    string Name,
    string Identity,
    string Target,
    string Provider,
    string Capabilities,
    string Policy,
    string Status,
    string StatusAccent,
    string ContractSha256)
{
    public static AutomationAdapterItemViewModel From(
        AutomationAdapterDescriptor descriptor) =>
        new(
            descriptor.DisplayName,
            $"{descriptor.AdapterId}@{descriptor.AdapterVersion}",
            $"{descriptor.MaskId}@{descriptor.MaskVersion}",
            descriptor.Provider,
            $"{(descriptor.SupportsSimulation ? "Simulação" : "Sem simulação")} • "
            + $"{(descriptor.SupportsApply ? "Aplicação" : "Sem aplicação")}",
            descriptor.CatalogExecutionEnabled
                ? "Catálogo autorizado por política"
                : "Somente fluxo interno homologado",
            descriptor.IsEnabled ? "REGISTRADO" : "DESABILITADO",
            descriptor.IsEnabled ? "#36D17C" : "#F8C33A",
            descriptor.ContractSha256);
}

public sealed record AutomationIntegrationAssessmentItemViewModel(
    string Identity,
    string Status,
    string StatusAccent,
    string Adapter,
    string Summary,
    string EvaluatedAt)
{
    public static AutomationIntegrationAssessmentItemViewModel From(
        AutomationIntegrationAssessment assessment) =>
        new(
            $"{assessment.MaskId}@{assessment.MaskVersion}",
            GetAssessmentLabel(assessment.Status),
            GetAssessmentAccent(assessment.Status),
            assessment.AdapterId is null
                ? "Nenhum adaptador"
                : $"{assessment.AdapterId}@{assessment.AdapterVersion}",
            assessment.Summary,
            assessment.EvaluatedAt.ToLocalTime()
                .ToString("dd/MM/yyyy HH:mm:ss"));

    private static string GetAssessmentLabel(
        AutomationIntegrationStatus status) =>
        status switch
        {
            AutomationIntegrationStatus.Ready => "PRONTA",
            AutomationIntegrationStatus.InactiveMask => "INATIVA",
            AutomationIntegrationStatus.InvalidCatalogContract =>
                "SNAPSHOT INVÁLIDO",
            AutomationIntegrationStatus.AdapterNotRegistered =>
                "SEM ADAPTADOR",
            AutomationIntegrationStatus.ContractMismatch =>
                "HASH DIVERGENTE",
            AutomationIntegrationStatus.AdapterDisabled =>
                "DESABILITADO",
            _ => "POLÍTICA BLOQUEADA"
        };

    private static string GetAssessmentAccent(
        AutomationIntegrationStatus status) =>
        status switch
        {
            AutomationIntegrationStatus.Ready => "#36D17C",
            AutomationIntegrationStatus.InvalidCatalogContract
                or AutomationIntegrationStatus.ContractMismatch => "#FF5D68",
            AutomationIntegrationStatus.InactiveMask => "#627087",
            _ => "#F8C33A"
        };
}
