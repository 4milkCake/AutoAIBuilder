using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Input;
using AutoAIBuilder.Application.Automation.Supervised;
using AutoAIBuilder.Application.History;

namespace AutoAIBuilder.Desktop.ViewModels;

public sealed partial class MainWindowViewModel
{
    private SupervisedAutomationPreflight? _supervisedPreflight;
    private SupervisedAutomationRunResult? _supervisedResult;
    private string _supervisedSourceDwgPath = string.Empty;
    private string _supervisedHistoricalMaskPath = string.Empty;
    private string _supervisedHistoricalPointsPath = string.Empty;
    private string _supervisedLegendObjectsPath = string.Empty;
    private string _supervisedScriptsPath = string.Empty;
    private string _supervisedOutputRoot = string.Empty;
    private string _supervisedStage = "AGUARDANDO PRÉ-VERIFICAÇÃO";
    private string _supervisedStageAccent = "#627087";
    private string _supervisedSummary =
        "Revise os insumos e execute a pré-verificação.";
    private bool _isSupervisedAutomationRunning;

    public ObservableCollection<SupervisedGateItemViewModel>
        SupervisedAutomationGates { get; } = [];

    public ObservableCollection<SupervisedPointIssueItemViewModel>
        SupervisedPointIssues { get; } = [];

    public ICommand InspectSupervisedAutomationCommand { get; }

    public ICommand ChooseSupervisedHistoricalMaskCommand { get; }

    public ICommand ChooseSupervisedSourceDwgCommand { get; }

    public ICommand ChooseSupervisedHistoricalPointsCommand { get; }

    public ICommand ChooseSupervisedLegendObjectsCommand { get; }

    public ICommand ChooseSupervisedScriptsCommand { get; }

    public ICommand ChooseSupervisedOutputCommand { get; }

    public ICommand ExecuteSupervisedAutomationCommand { get; }

    public ICommand OpenSupervisedRunCommand { get; }

    public string SupervisedHistoricalMaskPath
    {
        get => _supervisedHistoricalMaskPath;
        private set => SetField(ref _supervisedHistoricalMaskPath, value);
    }

    public string SupervisedSourceDwgPath
    {
        get => _supervisedSourceDwgPath;
        private set => SetField(ref _supervisedSourceDwgPath, value);
    }

    public string SupervisedHistoricalPointsPath
    {
        get => _supervisedHistoricalPointsPath;
        private set => SetField(ref _supervisedHistoricalPointsPath, value);
    }

    public string SupervisedLegendObjectsPath
    {
        get => _supervisedLegendObjectsPath;
        private set => SetField(ref _supervisedLegendObjectsPath, value);
    }

    public string SupervisedScriptsPath
    {
        get => _supervisedScriptsPath;
        private set => SetField(ref _supervisedScriptsPath, value);
    }

    public string SupervisedOutputRoot
    {
        get => _supervisedOutputRoot;
        private set => SetField(ref _supervisedOutputRoot, value);
    }

    public string SupervisedStage
    {
        get => _supervisedStage;
        private set => SetField(ref _supervisedStage, value);
    }

    public string SupervisedStageAccent
    {
        get => _supervisedStageAccent;
        private set => SetField(ref _supervisedStageAccent, value);
    }

    public string SupervisedSummary
    {
        get => _supervisedSummary;
        private set => SetField(ref _supervisedSummary, value);
    }

    public bool IsSupervisedAutomationRunning
    {
        get => _isSupervisedAutomationRunning;
        private set
        {
            if (SetField(ref _isSupervisedAutomationRunning, value))
            {
                RaiseSupervisedCommandStates();
                OnPropertyChanged(nameof(SupervisedExecutionButtonText));
            }
        }
    }

    public string SupervisedExecutionButtonText =>
        IsSupervisedAutomationRunning
            ? "Executando no AutoCAD…"
            : "Executar na cópia técnica";

    public string SupervisedSourceIntegrity =>
        _supervisedResult is null
            ? _supervisedPreflight?.Gates.FirstOrDefault(
                gate => gate.Code == "SOURCE_INTEGRITY")?.Evidence
              ?? "Ainda não verificado"
            : _supervisedResult.SourceIntegrityConfirmed
                ? "ORIGINAL ÍNTEGRO APÓS A EXECUÇÃO"
                : "ALERTA: ORIGINAL DIVERGIU";

    public string SupervisedHistoricalIntegrity =>
        _supervisedResult is null
            ? "Referência ainda não executada"
            : _supervisedResult.HistoricalReferenceIntegrityConfirmed
                ? "REFERÊNCIA HISTÓRICA ÍNTEGRA"
                : "ALERTA: REFERÊNCIA DIVERGIU";

    public string SupervisedResultPath =>
        _supervisedResult?.ResultDwgPath
        ?? "Nenhum DWG de resultado nesta sessão.";

    public string SupervisedRunPath =>
        _supervisedResult?.RunDirectory
        ?? "A pasta será criada somente após confirmação.";

    public string SupervisedAccuracy =>
        _supervisedResult is null
            ? "—"
            : $"{_supervisedResult.Metrics.AccuracyPercentage:0.##}%";

    public string SupervisedCorrectPoints =>
        _supervisedResult is null
            ? "—"
            : $"{_supervisedResult.Metrics.CorrectPoints}/"
              + $"{_supervisedResult.Metrics.ExpectedPoints}";

    public string SupervisedHistoricalMatches =>
        _supervisedResult is null
            ? "—"
            : $"{_supervisedResult.Metrics.HistoricalMatches}/"
              + $"{_supervisedResult.Metrics.HistoricalPoints}";

    public string SupervisedPreservation =>
        _supervisedResult is null
            ? "Preservação ainda não medida"
            : _supervisedResult.Metrics.MissingEntityHandles == 0
              && _supervisedResult.Metrics.EntitiesBefore
                 == _supervisedResult.Metrics.EntitiesAfter
                ? "PRESERVAÇÃO CONFIRMADA — "
                  + $"{_supervisedResult.Metrics.EntitiesAfter}/"
                  + $"{_supervisedResult.Metrics.EntitiesBefore} entidades; "
                  + "zero handles ausentes; CINZA PONTOS "
                  + $"{_supervisedResult.Metrics.ProtectedBlocksAfter}/"
                  + $"{_supervisedResult.Metrics.ProtectedBlocksBefore}"
                : "ALERTA DE PRESERVAÇÃO — "
                  + $"{_supervisedResult.Metrics.MissingEntityHandles} "
                  + "handle(s) ausente(s)";

    public string SupervisedPlanId =>
        _supervisedPreflight is null
            ? "Plano ainda não inspecionado"
            : $"{_supervisedPreflight.PlanId[..16]}…";

    private void RefreshSupervisedAutomation()
    {
        if (SelectedProject is null || _semanticSnapshot.Dataset is null)
        {
            ClearSupervisedAutomation(
                "Selecione um projeto com base semântica importada.");
            return;
        }

        DiscoverSupervisedInputs();
        try
        {
            _automationPreviewPlan = _automationPreviewService.Build(
                SelectedProject.Id,
                _semanticSnapshot,
                CadVisualization);
            _supervisedPreflight = _supervisedAutomationService.Inspect(
                CreateSupervisedRequest());
            if (_supervisedResult?.ProjectId != SelectedProject.Id)
            {
                _supervisedResult = null;
            }

            _supervisedResult ??= _supervisedAutomationService.LoadLatest(
                SelectedProject.Id,
                SupervisedOutputRoot);
            ReplaceItems(
                SupervisedAutomationGates,
                _supervisedPreflight.Gates.Select(
                    SupervisedGateItemViewModel.From));
            ReplaceItems(
                SupervisedPointIssues,
                _supervisedResult?.PointComparisons
                    .Where(item => !item.LayerMatches)
                    .Select(SupervisedPointIssueItemViewModel.From)
                ?? []);
            var blockers = _supervisedPreflight.Gates.Count(
                gate => gate.Status
                        == SupervisedAutomationGateStatus.Blocked);
            if (_supervisedResult is not null)
            {
                SupervisedStage = _supervisedResult.AcceptancePassed
                    ? "ÚLTIMA EXECUÇÃO APROVADA E ISOLADA"
                    : "ÚLTIMO RESULTADO REPROVADO E ISOLADO";
                SupervisedStageAccent = _supervisedResult.AcceptancePassed
                    ? "#36D17C"
                    : "#FF5D68";
                SupervisedSummary = _supervisedResult.Summary;
            }
            else
            {
                SupervisedStage = _supervisedPreflight.CanExecute
                    ? "PRONTO PARA CONFIRMAÇÃO"
                    : "EXECUÇÃO BLOQUEADA";
                SupervisedStageAccent = _supervisedPreflight.CanExecute
                    ? "#36D17C"
                    : "#F8C33A";
                SupervisedSummary = _supervisedPreflight.CanExecute
                    ? "Todos os bloqueios de segurança foram liberados. "
                      + "A execução ainda exige sua confirmação explícita."
                    : $"{blockers} bloqueio(s) impedem a execução. "
                      + "Nenhum DWG foi criado ou alterado.";
            }

            NotifySupervisedChanged();
            RaiseSupervisedCommandStates();
            StatusMessage = _supervisedResult is null
                ? "Pré-verificação 11.6H.1 concluída; nenhuma rotina foi executada."
                : "Última execução 11.6H.1 restaurada a partir do manifesto.";
        }
        catch (Exception exception) when (
            exception is ArgumentException
                or InvalidDataException
                or InvalidOperationException
                or IOException
                or UnauthorizedAccessException)
        {
            ClearSupervisedAutomation(exception.Message);
            ReportSupervisedAutomationFailure(exception);
        }
    }

    private void DiscoverSupervisedInputs()
    {
        var drawingPath = _semanticSnapshot.Dataset?.DrawingPath;
        if (string.IsNullOrWhiteSpace(drawingPath))
        {
            return;
        }

        var workDirectory = Path.GetDirectoryName(drawingPath);
        var root = workDirectory is null
            ? null
            : Directory.GetParent(workDirectory)?.FullName;
        if (string.IsNullOrWhiteSpace(SupervisedHistoricalMaskPath))
        {
            SupervisedHistoricalMaskPath = FindFirst(
                workDirectory,
                "*MASCARA_AUTOMATICA_V06.dwg");
        }

        if (string.IsNullOrWhiteSpace(SupervisedSourceDwgPath))
        {
            SupervisedSourceDwgPath = FindFirst(
                workDirectory,
                "*ARQUITETURA_ORIGINAL.dwg");
        }

        if (string.IsNullOrWhiteSpace(SupervisedHistoricalPointsPath))
        {
            SupervisedHistoricalPointsPath = FindFirst(
                workDirectory,
                "*MASCARA_AUTOMATICA_V06_pontos_semanticos_v07.csv");
        }

        if (string.IsNullOrWhiteSpace(SupervisedLegendObjectsPath))
        {
            SupervisedLegendObjectsPath = FindFirst(
                workDirectory,
                "*legenda_objetos_v033.csv");
        }

        if (string.IsNullOrWhiteSpace(SupervisedScriptsPath)
            && root is not null)
        {
            SupervisedScriptsPath = Path.Combine(root, "04_Scripts");
        }

        if (string.IsNullOrWhiteSpace(SupervisedOutputRoot)
            && root is not null)
        {
            SupervisedOutputRoot = Path.Combine(
                root,
                "03_Saidas",
                "Execucoes_11.6H");
        }
    }

    private static string FindFirst(string? directory, string pattern)
    {
        if (string.IsNullOrWhiteSpace(directory)
            || !Directory.Exists(directory))
        {
            return string.Empty;
        }

        return Directory
            .EnumerateFiles(directory, pattern, SearchOption.TopDirectoryOnly)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault() ?? string.Empty;
    }

    private SupervisedAutomationRequest CreateSupervisedRequest()
    {
        if (SelectedProject is null || _automationPreviewPlan is null)
        {
            throw new InvalidOperationException(
                "O projeto e o plano 11.6G são obrigatórios.");
        }

        return new SupervisedAutomationRequest(
            SelectedProject.Id,
            _automationPreviewPlan,
            _semanticSnapshot.Points,
            _semanticSnapshot.Components,
            SupervisedSourceDwgPath,
            SupervisedHistoricalMaskPath,
            SupervisedHistoricalPointsPath,
            SupervisedLegendObjectsPath,
            SupervisedScriptsPath,
            SupervisedOutputRoot);
    }

    private async Task ExecuteSupervisedAutomationAsync(
        CancellationToken cancellationToken)
    {
        RefreshSupervisedAutomation();
        if (_supervisedPreflight?.CanExecute != true)
        {
            return;
        }

        if (!_dialogService.ConfirmSupervisedAutomationExecution(
                _supervisedPreflight.SourceDwgPath,
                _supervisedPreflight.HistoricalMaskPath,
                _supervisedPreflight.OutputRoot,
                _supervisedPreflight.ExpectedPointCount))
        {
            StatusMessage =
                "Execução 11.6H.1 cancelada; nenhuma cópia técnica foi criada.";
            return;
        }

        IsSupervisedAutomationRunning = true;
        SupervisedStage = "AUTOCAD EM EXECUÇÃO";
        SupervisedStageAccent = "#2C9BFF";
        SupervisedSummary =
            "A rotina trabalha somente na cópia técnica isolada e não exclui entidades.";
        try
        {
            _supervisedResult = await _supervisedAutomationService.ExecuteAsync(
                CreateSupervisedRequest(),
                cancellationToken);
            ReplaceItems(
                SupervisedPointIssues,
                _supervisedResult.PointComparisons
                    .Where(item => !item.LayerMatches)
                    .Select(SupervisedPointIssueItemViewModel.From));
            SupervisedStage =
                _supervisedResult.SourceIntegrityConfirmed
                && _supervisedResult.HistoricalReferenceIntegrityConfirmed
                && _supervisedResult.AcceptancePassed
                    ? "EXECUÇÃO CONCLUÍDA E ISOLADA"
                    : "PRESERVAÇÃO REPROVADA E ISOLADA";
            SupervisedStageAccent =
                _supervisedResult.SourceIntegrityConfirmed
                && _supervisedResult.HistoricalReferenceIntegrityConfirmed
                && _supervisedResult.AcceptancePassed
                    ? "#36D17C"
                    : "#FF5D68";
            SupervisedSummary = _supervisedResult.Summary;
            NotifySupervisedChanged();
            TryRecordActivity(
                "Automação",
                "Execução supervisionada 11.6H.1",
                _supervisedResult.Summary,
                _supervisedResult.AcceptancePassed
                    ? ActivityLevel.Information
                    : ActivityLevel.Warning,
                SelectedProject);
            StatusMessage =
                _supervisedResult.AcceptancePassed
                    ? "11.6H.1 aprovado: pontos organizados e arquitetura preservada."
                    : "11.6H.1 executado, mas a preservação foi reprovada.";
        }
        finally
        {
            IsSupervisedAutomationRunning = false;
        }
    }

    private bool CanExecuteSupervisedAutomation() =>
        !IsSupervisedAutomationRunning
        && _supervisedPreflight?.CanExecute == true;

    private void ChooseSupervisedHistoricalMask()
    {
        var selected = _filePicker.PickSupervisedHistoricalMask(
            SupervisedHistoricalMaskPath);
        if (!string.IsNullOrWhiteSpace(selected))
        {
            SupervisedHistoricalMaskPath = selected;
            RefreshSupervisedAutomation();
        }
    }

    private void ChooseSupervisedSourceDwg()
    {
        var selected = _filePicker.PickSupervisedSourceDwg(
            SupervisedSourceDwgPath);
        if (!string.IsNullOrWhiteSpace(selected))
        {
            SupervisedSourceDwgPath = selected;
            RefreshSupervisedAutomation();
        }
    }

    private void ChooseSupervisedHistoricalPoints()
    {
        var selected = _filePicker.PickSupervisedCsv(
            "Selecionar pontos da máscara histórica (v07)",
            SupervisedHistoricalPointsPath);
        if (!string.IsNullOrWhiteSpace(selected))
        {
            SupervisedHistoricalPointsPath = selected;
            RefreshSupervisedAutomation();
        }
    }

    private void ChooseSupervisedLegendObjects()
    {
        var selected = _filePicker.PickSupervisedCsv(
            "Selecionar catálogo de objetos da legenda",
            SupervisedLegendObjectsPath);
        if (!string.IsNullOrWhiteSpace(selected))
        {
            SupervisedLegendObjectsPath = selected;
            RefreshSupervisedAutomation();
        }
    }

    private void ChooseSupervisedScripts()
    {
        var selected = _filePicker.PickAutoLispDirectory(
            SupervisedScriptsPath);
        if (!string.IsNullOrWhiteSpace(selected))
        {
            SupervisedScriptsPath = selected;
            RefreshSupervisedAutomation();
        }
    }

    private void ChooseSupervisedOutput()
    {
        var selected = _filePicker.PickAutomationOutputDirectory(
            SupervisedOutputRoot);
        if (!string.IsNullOrWhiteSpace(selected))
        {
            SupervisedOutputRoot = selected;
            RefreshSupervisedAutomation();
        }
    }

    private void OpenSupervisedRun()
    {
        if (_supervisedResult is null)
        {
            return;
        }

        _fileSystemLauncher.OpenFile(_supervisedResult.RunDirectory);
    }

    private void ClearSupervisedAutomation(string message)
    {
        _supervisedPreflight = null;
        ReplaceItems(SupervisedAutomationGates, []);
        SupervisedStage = "EXECUÇÃO INDISPONÍVEL";
        SupervisedStageAccent = "#F8C33A";
        SupervisedSummary = message;
        NotifySupervisedChanged();
        RaiseSupervisedCommandStates();
    }

    private void RaiseSupervisedCommandStates()
    {
        (ExecuteSupervisedAutomationCommand as AsyncCommand)?
            .RaiseCanExecuteChanged();
        (OpenSupervisedRunCommand as RelayCommand)?
            .RaiseCanExecuteChanged();
    }

    private void NotifySupervisedChanged()
    {
        OnPropertyChanged(nameof(SupervisedSourceIntegrity));
        OnPropertyChanged(nameof(SupervisedHistoricalIntegrity));
        OnPropertyChanged(nameof(SupervisedResultPath));
        OnPropertyChanged(nameof(SupervisedRunPath));
        OnPropertyChanged(nameof(SupervisedAccuracy));
        OnPropertyChanged(nameof(SupervisedCorrectPoints));
        OnPropertyChanged(nameof(SupervisedHistoricalMatches));
        OnPropertyChanged(nameof(SupervisedPreservation));
        OnPropertyChanged(nameof(SupervisedPlanId));
    }

    private void ReportSupervisedAutomationFailure(Exception exception)
    {
        SupervisedStage = "FALHA ISOLADA";
        SupervisedStageAccent = "#FF5D68";
        SupervisedSummary = exception.Message;
        StatusMessage =
            $"A execução supervisionada não foi concluída: {exception.Message}";
        TryWriteDiagnostic(
            Application.Diagnostics.DiagnosticLevel.Error,
            "SupervisedAutomation",
            "Falha isolada no Marco 11.6H.1.",
            exception);
    }
}

public sealed record SupervisedGateItemViewModel(
    string Name,
    string Status,
    string Accent,
    string Evidence)
{
    public static SupervisedGateItemViewModel From(
        SupervisedAutomationGate gate) =>
        new(
            gate.Name,
            gate.Status switch
            {
                SupervisedAutomationGateStatus.Passed => "APROVADO",
                SupervisedAutomationGateStatus.Warning => "ATENÇÃO",
                _ => "BLOQUEADO"
            },
            gate.Status switch
            {
                SupervisedAutomationGateStatus.Passed => "#36D17C",
                SupervisedAutomationGateStatus.Warning => "#F8C33A",
                _ => "#FF5D68"
            },
            gate.Evidence);
}

public sealed record SupervisedPointIssueItemViewModel(
    string Handle,
    string ExpectedLayer,
    string ActualLayer,
    string Status)
{
    public static SupervisedPointIssueItemViewModel From(
        SupervisedPointComparison comparison) =>
        new(
            comparison.Handle,
            comparison.ExpectedLayer,
            comparison.Found ? comparison.ActualLayer : "NÃO ENCONTRADO",
            comparison.Found ? "LAYER DIVERGENTE" : "AUSENTE");
}
