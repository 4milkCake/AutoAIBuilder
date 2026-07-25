using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Input;
using AutoAIBuilder.Application.Automation.Execution;
using AutoAIBuilder.Application.Automation.Pilots;
using AutoAIBuilder.Application.Automation.Validation;
using AutoAIBuilder.Application.Diagnostics;
using AutoAIBuilder.Application.History;
using AutoAIBuilder.Application.Navigation;
using AutoAIBuilder.Application.Operations;
using AutoAIBuilder.Application.Validation;

namespace AutoAIBuilder.Desktop.ViewModels;

public sealed partial class MainWindowViewModel
{
    private const string AutomationResourcePrefix = "automation-pilot-project:";
    private static readonly TimeSpan AutomationOperationTimeout =
        TimeSpan.FromMinutes(5);

    private ProjectFileItemViewModel? _selectedAutomationInput;
    private AutomationExecutionPlan? _automationPilotPlan;
    private AutomationAuditStatus? _automationSimulationStatus;
    private DateTimeOffset? _automationPlanProjectUpdatedAt;
    private string _automationOutputRoot = string.Empty;
    private string _automationPilotStage = "AGUARDANDO ENTRADA";
    private string _automationPilotStageAccent = "#627087";
    private string _automationPilotSummary =
        "Selecione um arquivo catalogado e uma pasta de saída para iniciar.";
    private string _automationPlanChecksum = "Ainda não calculado";
    private string _automationPlanIdempotencyKey = "Ainda não calculada";
    private string _automationPublishedPath = string.Empty;
    private string _automationRecoveryPath = string.Empty;
    private string _automationResultStatus = "Nenhuma execução nesta sessão.";
    private bool _isAutomationPilotRunning;
    private int _automationPilotProgress;
    private string _automationPilotStep = "Motor operacional pronto.";
    private Guid? _activeAutomationOperationId;

    public ObservableCollection<ProjectFileItemViewModel> AutomationPilotFiles
    {
        get;
    } = [];

    public ObservableCollection<AutomationPlanActionItemViewModel>
        AutomationPlanActions { get; } = [];

    public ObservableCollection<AutomationValidationIssueItemViewModel>
        AutomationPilotIssues { get; } = [];

    public ObservableCollection<string> AutomationOutputFiles { get; } = [];

    public ICommand ChooseAutomationOutputCommand { get; }

    public ICommand SimulateAutomationPilotCommand { get; }

    public ICommand ExecuteAutomationPilotCommand { get; }

    public ICommand CancelAutomationPilotCommand { get; }

    public ICommand ResetAutomationPilotCommand { get; }

    public ICommand OpenAutomationOutputCommand { get; }

    public Visibility AutomationVisibility =>
        CurrentSection == WorkspaceSection.Automation
            ? Visibility.Visible
            : Visibility.Collapsed;

    public ProjectFileItemViewModel? SelectedAutomationInput
    {
        get => _selectedAutomationInput;
        set
        {
            if (_selectedAutomationInput?.Id == value?.Id)
            {
                _selectedAutomationInput = value;
                OnPropertyChanged();
                return;
            }

            _selectedAutomationInput = value;
            OnPropertyChanged();
            InvalidateAutomationPlan(
                value is null
                    ? "Selecione um arquivo catalogado."
                    : "Entrada selecionada; execute uma nova simulação.");
        }
    }

    public string AutomationOutputRoot
    {
        get => _automationOutputRoot;
        private set
        {
            if (SetField(ref _automationOutputRoot, value))
            {
                OnPropertyChanged(nameof(AutomationOutputRootDisplay));
                InvalidateAutomationPlan(
                    string.IsNullOrWhiteSpace(value)
                        ? "Selecione uma pasta de saída."
                        : "Destino selecionado; execute uma nova simulação.");
            }
        }
    }

    public string AutomationOutputRootDisplay =>
        string.IsNullOrWhiteSpace(AutomationOutputRoot)
            ? "Nenhuma pasta selecionada"
            : AutomationOutputRoot;

    public string AutomationPilotStage
    {
        get => _automationPilotStage;
        private set => SetField(ref _automationPilotStage, value);
    }

    public string AutomationPilotStageAccent
    {
        get => _automationPilotStageAccent;
        private set => SetField(ref _automationPilotStageAccent, value);
    }

    public string AutomationPilotSummary
    {
        get => _automationPilotSummary;
        private set => SetField(ref _automationPilotSummary, value);
    }

    public string AutomationPlanChecksum
    {
        get => _automationPlanChecksum;
        private set => SetField(ref _automationPlanChecksum, value);
    }

    public string AutomationPlanIdempotencyKey
    {
        get => _automationPlanIdempotencyKey;
        private set => SetField(ref _automationPlanIdempotencyKey, value);
    }

    public string AutomationPublishedPath
    {
        get => _automationPublishedPath;
        private set
        {
            if (SetField(ref _automationPublishedPath, value))
            {
                OnPropertyChanged(nameof(AutomationResultVisibility));
                (OpenAutomationOutputCommand as RelayCommand)?
                    .RaiseCanExecuteChanged();
            }
        }
    }

    public string AutomationRecoveryPath
    {
        get => _automationRecoveryPath;
        private set
        {
            if (SetField(ref _automationRecoveryPath, value))
            {
                OnPropertyChanged(nameof(AutomationRecoveryVisibility));
            }
        }
    }

    public string AutomationResultStatus
    {
        get => _automationResultStatus;
        private set => SetField(ref _automationResultStatus, value);
    }

    public bool IsAutomationPilotRunning
    {
        get => _isAutomationPilotRunning;
        private set
        {
            if (SetField(ref _isAutomationPilotRunning, value))
            {
                OnPropertyChanged(nameof(AutomationOperationVisibility));
                RaiseAutomationPilotCommandStates();
            }
        }
    }

    public int AutomationPilotProgress
    {
        get => _automationPilotProgress;
        private set => SetField(ref _automationPilotProgress, value);
    }

    public string AutomationPilotStep
    {
        get => _automationPilotStep;
        private set => SetField(ref _automationPilotStep, value);
    }

    public Visibility EmptyAutomationPilotFilesVisibility =>
        AutomationPilotFiles.Count == 0
            ? Visibility.Visible
            : Visibility.Collapsed;

    public Visibility AutomationPlanVisibility =>
        _automationPilotPlan is null
            ? Visibility.Collapsed
            : Visibility.Visible;

    public Visibility AutomationPlanEmptyVisibility =>
        _automationPilotPlan is null
            ? Visibility.Visible
            : Visibility.Collapsed;

    public Visibility AutomationOperationVisibility =>
        IsAutomationPilotRunning
            ? Visibility.Visible
            : Visibility.Collapsed;

    public Visibility AutomationResultVisibility =>
        string.IsNullOrWhiteSpace(AutomationPublishedPath)
            ? Visibility.Collapsed
            : Visibility.Visible;

    public Visibility AutomationRecoveryVisibility =>
        string.IsNullOrWhiteSpace(AutomationRecoveryPath)
            ? Visibility.Collapsed
            : Visibility.Visible;

    private void RefreshAutomationPilot()
    {
        RefreshAutomationPilotFiles();
        if (_automationPilotPlan is null)
        {
            AutomationPilotSummary = AutomationPilotFiles.Count == 0
                ? "Catalogue ao menos um arquivo acessível antes de simular."
                : "Selecione uma entrada e uma pasta de saída. A simulação "
                  + "não cria cópias nem pastas no destino.";
        }

        RaiseAutomationPilotCommandStates();
    }

    private void RefreshAutomationPilotFiles()
    {
        var selectedId = SelectedAutomationInput?.Id;
        var planNeedsInvalidation = _automationPilotPlan is not null
            && (_automationPilotPlan.ProjectId != SelectedProject?.Id
                || _automationPlanProjectUpdatedAt
                != SelectedProject?.UpdatedAtValue);
        ReplaceItems(
            AutomationPilotFiles,
            _allProjectFiles.Where(file => file.Exists));
        _selectedAutomationInput = selectedId is Guid id
            ? AutomationPilotFiles.FirstOrDefault(file => file.Id == id)
            : AutomationPilotFiles.FirstOrDefault();
        OnPropertyChanged(nameof(SelectedAutomationInput));
        OnPropertyChanged(nameof(EmptyAutomationPilotFilesVisibility));

        if (_selectedAutomationInput is null)
        {
            InvalidateAutomationPlan("Nenhum arquivo acessível foi encontrado.");
        }
        else if (planNeedsInvalidation)
        {
            InvalidateAutomationPlan(
                "O projeto mudou; execute uma nova simulação antes de continuar.");
        }

        RaiseAutomationPilotCommandStates();
    }

    private void ChooseAutomationOutputDirectory()
    {
        var selected = _filePicker.PickAutomationOutputDirectory(
            AutomationOutputRoot);
        if (string.IsNullOrWhiteSpace(selected))
        {
            AutomationPilotSummary =
                "Seleção do destino cancelada; nada foi alterado.";
            return;
        }

        AutomationOutputRoot = Path.GetFullPath(selected);
    }

    private async Task SimulateAutomationPilotAsync(
        CancellationToken cancellationToken)
    {
        if (SelectedProject is null || SelectedAutomationInput is null)
        {
            AutomationPilotSummary =
                "Selecione um projeto e um arquivo catalogado.";
            return;
        }

        if (string.IsNullOrWhiteSpace(AutomationOutputRoot))
        {
            AutomationPilotSummary = "Selecione a pasta de saída.";
            return;
        }

        var project = _workspaceService.GetProject(SelectedProject.Id)
            ?? throw new InvalidOperationException(
                "O projeto ativo não foi encontrado.");
        var inspections = _workspaceService.InspectFiles(project.Id);
        var validation = _validationService.Validate(project, inspections);
        var projectIssues = CreateProjectValidationIssues(validation);
        VerifiedCopySimulation simulation = null!;
        var operation = await RunAutomationPilotOperationAsync(
            "automation-pilot-simulation",
            "Simular cópia técnica verificada",
            "Validando projeto, contrato e arquivo",
            async operationCancellation =>
            {
                simulation = await _verifiedCopyPilotService.SimulateAsync(
                    new VerifiedCopyPilotRequest(
                        project.Id,
                        SelectedAutomationInput.SourcePath,
                        AutomationOutputRoot,
                        projectIssues),
                    operationCancellation);
                return simulation;
            },
            _ => true,
            _ => "Simulação concluída",
            cancellationToken);
        if (operation is null
            || !HandleAutomationOperationState(operation.Value.Execution))
        {
            return;
        }

        ApplyAutomationSimulation(simulation);
        if (simulation.Outcome.Status == AutomationAuditStatus.Simulated)
        {
            AutomationPilotStage = "SIMULAÇÃO APROVADA";
            AutomationPilotStageAccent = "#36D17C";
            AutomationPilotSummary =
                "Plano aprovado. Confira cada etapa antes de confirmar a execução.";
            StatusMessage =
                "Simulação concluída sem criar arquivos. O plano aguarda confirmação.";
        }
        else
        {
            AutomationPilotStage = "SIMULAÇÃO BLOQUEADA";
            AutomationPilotStageAccent = "#FF5D68";
            AutomationPilotSummary =
                "A pré-validação bloqueou o piloto. Corrija os itens indicados.";
            StatusMessage =
                "Simulação bloqueada; nenhuma cópia ou pasta de saída foi criada.";
        }
    }

    private async Task ExecuteAutomationPilotAsync(
        CancellationToken cancellationToken)
    {
        if (_automationPilotPlan is null
            || SelectedAutomationInput is null
            || _automationSimulationStatus != AutomationAuditStatus.Simulated)
        {
            AutomationPilotSummary =
                "Execute e aprove uma nova simulação antes de confirmar.";
            return;
        }

        var checksum = _automationPilotPlan.Inputs.Single().Sha256;
        if (!_dialogService.ConfirmVerifiedCopyExecution(
                SelectedAutomationInput.Name,
                _automationPilotPlan.OutputRoot,
                checksum))
        {
            AutomationPilotSummary =
                "Execução cancelada na confirmação; o plano continua disponível.";
            return;
        }

        AutomationExecutionOutcome outcome = null!;
        var operation = await RunAutomationPilotOperationAsync(
            "automation-pilot-apply",
            "Executar cópia técnica verificada",
            "Conferindo o checksum da simulação",
            async operationCancellation =>
            {
                outcome = await _verifiedCopyPilotService.ExecuteAsync(
                    _automationPilotPlan,
                    operationCancellation);
                return outcome;
            },
            value => value.Succeeded,
            value => value.Summary,
            cancellationToken);
        if (operation is null)
        {
            return;
        }

        if (outcome is not null)
        {
            ApplyAutomationOutcome(outcome);
        }

        if (!HandleAutomationOperationState(operation.Value.Execution))
        {
            return;
        }

        if (outcome is null)
        {
            throw new InvalidOperationException(
                "A execução terminou sem produzir um resultado auditável.");
        }

        AutomationPilotStage = outcome.ReusedPreviousResult
            ? "RESULTADO REUTILIZADO"
            : "EXECUÇÃO CONCLUÍDA";
        AutomationPilotStageAccent = "#36D17C";
        AutomationPilotSummary = outcome.Summary;
        TryRecordActivity(
            "Automação",
            outcome.ReusedPreviousResult
                ? "Resultado seguro reutilizado"
                : "Cópia técnica verificada",
            $"Arquivo “{SelectedAutomationInput.Name}”; auditoria "
            + $"{outcome.AuditId}; saída “{outcome.PublishedPath}”.",
            ActivityLevel.Success,
            SelectedProject);
        StatusMessage = outcome.ReusedPreviousResult
            ? "Resultado idempotente reutilizado; a automação não rodou novamente."
            : "Cópia técnica e manifesto publicados sem alterar o original.";
    }

    private async Task<(OperationExecution Execution, TResult Result)?>
        RunAutomationPilotOperationAsync<TResult>(
            string operationType,
            string displayName,
            string initialStep,
            Func<CancellationToken, Task<TResult>> operation,
            Func<TResult, bool> isSuccessful,
            Func<TResult, string> summary,
            CancellationToken cancellationToken)
    {
        var projectId = SelectedProject?.Id
            ?? throw new InvalidOperationException(
                "Selecione um projeto antes de iniciar o piloto.");
        var request = OperationRequest.Create(
            operationType,
            displayName,
            AutomationResourcePrefix + projectId.ToString("N"),
            projectId,
            AutomationOperationTimeout);
        TResult operationValue = default!;
        _activeAutomationOperationId = request.Id;
        AutomationPilotProgress = 0;
        AutomationPilotStep = initialStep;
        IsAutomationPilotRunning = true;
        var progress = new Progress<OperationProgress>(update =>
        {
            AutomationPilotProgress = update.Percentage;
            AutomationPilotStep = update.Message;
        });

        try
        {
            var execution = await _operationCoordinator.RunAsync(
                request,
                async (context, operationCancellation) =>
                {
                    context.Report(10, initialStep);
                    operationValue = await operation(operationCancellation);
                    operationCancellation.ThrowIfCancellationRequested();
                    context.Report(90, "Validando e registrando o resultado");
                    if (!isSuccessful(operationValue))
                    {
                        throw new InvalidOperationException(
                            summary(operationValue));
                    }
                },
                progress,
                cancellationToken);
            return (execution, operationValue);
        }
        catch (OperationConflictException exception)
        {
            ReportAutomationPilotFailure(exception);
            return null;
        }
        finally
        {
            _activeAutomationOperationId = null;
            IsAutomationPilotRunning = false;
            RefreshOperationExecutions();
        }
    }

    private bool HandleAutomationOperationState(OperationExecution execution)
    {
        switch (execution.Status)
        {
            case OperationExecutionStatus.Succeeded:
                AutomationPilotProgress = 100;
                AutomationPilotStep = "Operação concluída";
                return true;
            case OperationExecutionStatus.Cancelled:
                AutomationPilotStage = "CANCELADA";
                AutomationPilotStageAccent = "#F8C33A";
                AutomationPilotSummary =
                    "Operação cancelada; artefatos parciais foram preservados "
                    + "quando chegaram a ser criados.";
                StatusMessage = AutomationPilotSummary;
                return false;
            case OperationExecutionStatus.TimedOut:
                AutomationPilotStage = "TEMPO EXCEDIDO";
                AutomationPilotStageAccent = "#F8C33A";
                AutomationPilotSummary =
                    "A operação excedeu o limite e foi interrompida com segurança.";
                StatusMessage = AutomationPilotSummary;
                return false;
            default:
                AutomationPilotStage = "FALHA ISOLADA";
                AutomationPilotStageAccent = "#FF5D68";
                AutomationPilotSummary =
                    execution.ErrorMessage ?? "A execução não foi concluída.";
                StatusMessage = AutomationPilotSummary;
                return false;
        }
    }

    private void ApplyAutomationSimulation(VerifiedCopySimulation simulation)
    {
        _automationPilotPlan = simulation.Plan;
        _automationSimulationStatus = simulation.Outcome.Status;
        _automationPlanProjectUpdatedAt = SelectedProject?.UpdatedAtValue;
        AutomationPlanChecksum = simulation.Plan.Inputs.FirstOrDefault()?.Sha256
                                 ?? "Não disponível";
        AutomationPlanIdempotencyKey = simulation.Plan.IdempotencyKey;
        ReplaceItems(
            AutomationPlanActions,
            simulation.Plan.Actions.Select(
                action => new AutomationPlanActionItemViewModel(
                    action.Order,
                    action.Code,
                    action.Description,
                    action.WritesDuringApply
                        ? "Somente após confirmação"
                        : "Somente leitura",
                    action.WritesDuringApply ? "#F8C33A" : "#2C9BFF")));
        ReplaceItems(
            AutomationPilotIssues,
            simulation.Plan.ValidationIssues.Select(
                AutomationValidationIssueItemViewModel.From));
        AutomationPublishedPath = string.Empty;
        AutomationRecoveryPath = string.Empty;
        AutomationOutputFiles.Clear();
        AutomationResultStatus =
            $"Simulação auditada em {simulation.Outcome.AuditId:D}; "
            + "nenhuma execução confirmada nesta sessão.";
        OnPropertyChanged(nameof(AutomationPlanVisibility));
        OnPropertyChanged(nameof(AutomationPlanEmptyVisibility));
        RaiseAutomationPilotCommandStates();
    }

    private void ApplyAutomationOutcome(AutomationExecutionOutcome outcome)
    {
        AutomationPublishedPath = outcome.PublishedPath ?? string.Empty;
        AutomationRecoveryPath = outcome.RecoveryPath ?? string.Empty;
        ReplaceItems(AutomationOutputFiles, outcome.OutputPaths);
        AutomationResultStatus =
            $"{GetAutomationStatusLabel(outcome.Status)} • Auditoria "
            + outcome.AuditId.ToString("D");

        if (!outcome.Succeeded)
        {
            AutomationPilotStage = "FALHA ISOLADA";
            AutomationPilotStageAccent = "#FF5D68";
            AutomationPilotSummary = outcome.Summary;
            TryRecordActivity(
                "Automação",
                "Piloto não concluído",
                $"{outcome.Summary} Auditoria {outcome.AuditId}.",
                ActivityLevel.Error,
                SelectedProject);
        }
    }

    private static IReadOnlyList<AutomationValidationIssue>
        CreateProjectValidationIssues(ProjectValidationReport report) =>
        report.Results
            .Where(result => result.Status != ProjectValidationStatus.Passed)
            .Select(result => new AutomationValidationIssue(
                "projeto",
                result.Code,
                $"{result.Title}: {result.Detail} {result.Recommendation}",
                result.Status == ProjectValidationStatus.Error
                && report.BlockOnErrors
                    ? AutomationValidationSeverity.Error
                    : AutomationValidationSeverity.Warning))
            .ToArray();

    private void CancelAutomationPilot()
    {
        if (_activeAutomationOperationId is not Guid operationId
            || !_operationCoordinator.Cancel(operationId))
        {
            AutomationPilotSummary =
                "Nenhuma operação ativa pôde ser cancelada.";
            return;
        }

        AutomationPilotStep = "Cancelamento solicitado";
        AutomationPilotSummary =
            "Cancelamento solicitado; aguarde a preservação segura dos artefatos.";
    }

    private void ResetAutomationPilot()
    {
        InvalidateAutomationPlan(
            "Plano limpo. As seleções foram preservadas para uma nova simulação.");
        AutomationPublishedPath = string.Empty;
        AutomationRecoveryPath = string.Empty;
        AutomationOutputFiles.Clear();
        AutomationResultStatus = "Nenhuma execução nesta sessão.";
    }

    private void OpenAutomationOutput()
    {
        if (string.IsNullOrWhiteSpace(AutomationPublishedPath))
        {
            return;
        }

        try
        {
            _fileSystemLauncher.OpenFile(AutomationPublishedPath);
            StatusMessage = "Pasta publicada aberta no Explorer.";
        }
        catch (Exception exception) when (
            exception is IOException
                or UnauthorizedAccessException
                or InvalidOperationException
                or System.ComponentModel.Win32Exception)
        {
            ReportAutomationPilotFailure(exception);
        }
    }

    private void InvalidateAutomationPlan(string message)
    {
        _automationPilotPlan = null;
        _automationSimulationStatus = null;
        _automationPlanProjectUpdatedAt = null;
        AutomationPlanActions.Clear();
        AutomationPilotIssues.Clear();
        AutomationPlanChecksum = "Ainda não calculado";
        AutomationPlanIdempotencyKey = "Ainda não calculada";
        AutomationPilotStage = "AGUARDANDO SIMULAÇÃO";
        AutomationPilotStageAccent = "#627087";
        AutomationPilotSummary = message;
        OnPropertyChanged(nameof(AutomationPlanVisibility));
        OnPropertyChanged(nameof(AutomationPlanEmptyVisibility));
        RaiseAutomationPilotCommandStates();
    }

    private bool CanSimulateAutomationPilot() =>
        !IsAutomationPilotRunning
        && SelectedProject is not null
        && SelectedAutomationInput is not null
        && !string.IsNullOrWhiteSpace(AutomationOutputRoot);

    private bool CanExecuteAutomationPilot() =>
        !IsAutomationPilotRunning
        && _automationPilotPlan?.IsValid == true
        && _automationSimulationStatus == AutomationAuditStatus.Simulated;

    private void RaiseAutomationPilotCommandStates()
    {
        (ChooseAutomationOutputCommand as RelayCommand)?
            .RaiseCanExecuteChanged();
        (SimulateAutomationPilotCommand as AsyncCommand)?
            .RaiseCanExecuteChanged();
        (ExecuteAutomationPilotCommand as AsyncCommand)?
            .RaiseCanExecuteChanged();
        (CancelAutomationPilotCommand as RelayCommand)?
            .RaiseCanExecuteChanged();
        (ResetAutomationPilotCommand as RelayCommand)?
            .RaiseCanExecuteChanged();
    }

    private void ReportAutomationPilotFailure(Exception exception)
    {
        AutomationPilotStage = "FALHA ISOLADA";
        AutomationPilotStageAccent = "#FF5D68";
        AutomationPilotSummary =
            $"Não foi possível concluir o piloto: {GetFriendlyMessage(exception)}";
        StatusMessage = AutomationPilotSummary;
        TryWriteDiagnostic(
            DiagnosticLevel.Error,
            "VerifiedCopyPilot",
            "Falha isolada no piloto de cópia técnica.",
            exception);
    }

    private static string GetAutomationStatusLabel(
        AutomationAuditStatus status) => status switch
    {
        AutomationAuditStatus.Succeeded => "Concluída",
        AutomationAuditStatus.Reused => "Resultado reutilizado",
        AutomationAuditStatus.Simulated => "Simulada",
        AutomationAuditStatus.Rejected => "Bloqueada",
        AutomationAuditStatus.CancelledRolledBack => "Cancelada e preservada",
        AutomationAuditStatus.FailedRolledBack => "Falha isolada e preservada",
        AutomationAuditStatus.Interrupted => "Interrompida",
        _ => status.ToString()
    };
}

public sealed record AutomationPlanActionItemViewModel(
    int Order,
    string Code,
    string Description,
    string Safety,
    string Accent);

public sealed record AutomationValidationIssueItemViewModel(
    string Stage,
    string Code,
    string Message,
    string Severity,
    string Accent)
{
    public static AutomationValidationIssueItemViewModel From(
        AutomationValidationIssue issue) =>
        new(
            issue.Stage,
            issue.Code,
            issue.Message,
            issue.Severity switch
            {
                AutomationValidationSeverity.Error => "Erro",
                AutomationValidationSeverity.Warning => "Alerta",
                _ => "Informação"
            },
            issue.Severity switch
            {
                AutomationValidationSeverity.Error => "#FF5D68",
                AutomationValidationSeverity.Warning => "#F8C33A",
                _ => "#2C9BFF"
            });
}
