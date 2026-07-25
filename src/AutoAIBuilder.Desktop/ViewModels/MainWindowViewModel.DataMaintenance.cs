using System.IO;
using System.Windows;
using AutoAIBuilder.Application.Diagnostics;
using AutoAIBuilder.Application.History;
using AutoAIBuilder.Application.Operations;

namespace AutoAIBuilder.Desktop.ViewModels;

public sealed partial class MainWindowViewModel
{
    private string _dataMaintenanceStatus =
        "Nenhuma operação de manutenção executada nesta sessão.";
    private string _pendingDataDirectory = string.Empty;
    private bool _isDataRestartRequired;
    private bool _isDataOperationRunning;
    private int _dataOperationProgress;
    private string _dataOperationStep = "Motor operacional pronto.";
    private Guid? _activeDataOperationId;

    private const string DataStoreResourceKey = "local-data-store";
    private static readonly TimeSpan DataOperationTimeout =
        TimeSpan.FromMinutes(5);

    public string DataStoragePath => _dataMaintenanceService.DataDirectory;

    public string DatabasePath => _dataMaintenanceService.DatabasePath;

    public string BackupDirectory => _dataMaintenanceService.BackupDirectory;

    public string DatabaseSchemaVersion =>
        $"Esquema SQLite {_dataMaintenanceService.SchemaVersion}";

    public string DataMaintenanceStatus
    {
        get => _dataMaintenanceStatus;
        private set => SetField(ref _dataMaintenanceStatus, value);
    }

    public bool IsDataOperationRunning
    {
        get => _isDataOperationRunning;
        private set
        {
            if (SetField(ref _isDataOperationRunning, value))
            {
                OnPropertyChanged(nameof(DataOperationVisibility));
                RaiseDataOperationCommandStates();
            }
        }
    }

    public Visibility DataOperationVisibility =>
        IsDataOperationRunning ? Visibility.Visible : Visibility.Collapsed;

    public int DataOperationProgress
    {
        get => _dataOperationProgress;
        private set => SetField(ref _dataOperationProgress, value);
    }

    public string DataOperationStep
    {
        get => _dataOperationStep;
        private set => SetField(ref _dataOperationStep, value);
    }

    public string PendingDataDirectory
    {
        get => _pendingDataDirectory;
        private set
        {
            if (SetField(ref _pendingDataDirectory, value))
            {
                OnPropertyChanged(nameof(PendingDataDirectoryVisibility));
            }
        }
    }

    public Visibility PendingDataDirectoryVisibility =>
        string.IsNullOrWhiteSpace(PendingDataDirectory)
            ? Visibility.Collapsed
            : Visibility.Visible;

    public bool IsDataRestartRequired
    {
        get => _isDataRestartRequired;
        private set
        {
            if (SetField(ref _isDataRestartRequired, value))
            {
                OnPropertyChanged(nameof(DataRestartNoticeVisibility));
            }
        }
    }

    public Visibility DataRestartNoticeVisibility =>
        IsDataRestartRequired ? Visibility.Visible : Visibility.Collapsed;

    private async Task CreateDataBackupAsync(CancellationToken cancellationToken)
    {
        var suggestedName =
            $"AutoAIBuilder-backup-{DateTime.Now:yyyyMMdd-HHmmss}.aabbackup";
        var destination =
            _filePicker.PickDataBackupDestination(suggestedName);

        if (string.IsNullOrWhiteSpace(destination))
        {
            DataMaintenanceStatus = "Criação de backup cancelada.";
            return;
        }

        var operationResult = await RunDataOperationAsync(
            "data-backup",
            "Criar backup dos dados",
            "Validando destino do backup",
            () => _dataMaintenanceService.CreateBackup(destination),
            cancellationToken);
        if (operationResult is null)
        {
            return;
        }

        var (execution, result) = operationResult.Value;
        if (!HandleTerminalOperationState(execution))
        {
            return;
        }

        DataMaintenanceStatus =
            $"Backup criado em {result.CreatedAt:dd/MM/yyyy HH:mm:ss}: "
            + $"{result.FilePath} ({result.SizeBytes:N0} bytes).";
        TryRecordActivity(
            "Sistema",
            "Backup criado",
            $"Backup consistente dos dados criado em “{result.FilePath}”.",
            ActivityLevel.Success,
            SelectedProject);
        StatusMessage = "Backup dos dados criado e validado com sucesso.";
    }

    private async Task RestoreDataBackupAsync(CancellationToken cancellationToken)
    {
        var source = _filePicker.PickDataBackupSource();
        if (string.IsNullOrWhiteSpace(source))
        {
            DataMaintenanceStatus = "Restauração cancelada.";
            return;
        }

        if (!_dialogService.ConfirmRestoreDataBackup(source))
        {
            DataMaintenanceStatus = "Restauração cancelada; os dados não foram alterados.";
            return;
        }

        var operationResult = await RunDataOperationAsync(
            "data-restore",
            "Restaurar backup dos dados",
            "Validando o arquivo de backup",
            () => _dataMaintenanceService.RestoreBackup(source),
            cancellationToken);
        if (operationResult is null)
        {
            return;
        }

        var (execution, result) = operationResult.Value;
        if (!HandleTerminalOperationState(execution))
        {
            return;
        }

        _operationCoordinator.RecoverInterruptedOperations();
        _activeProjectContext.Reload();
        _applicationSettings = _settingsService.Load();
        LoadSettingsEditor(_applicationSettings);
        ApplyProjectFormDefaults();
        RefreshProjects(_activeProjectContext.ProjectId);
        RefreshHistory();
        RefreshDiagnostics();

        DataMaintenanceStatus =
            $"Backup restaurado em {result.RestoredAt:dd/MM/yyyy HH:mm:ss}. "
            + $"Cópia de segurança anterior: {result.SafetyBackupFilePath}";
        TryRecordActivity(
            "Sistema",
            "Backup restaurado",
            $"Dados restaurados a partir de “{result.SourceFilePath}”.",
            ActivityLevel.Warning,
            SelectedProject);
        StatusMessage =
            "Backup restaurado e dados recarregados. "
            + "O estado anterior foi preservado automaticamente.";
    }

    private async Task ChangeDataDirectoryAsync(
        CancellationToken cancellationToken)
    {
        var destination = _filePicker.PickDataDirectory(DataStoragePath);
        if (string.IsNullOrWhiteSpace(destination))
        {
            DataMaintenanceStatus = "Alteração da pasta de dados cancelada.";
            return;
        }

        if (!_dialogService.ConfirmDataDirectoryChange(
                DataStoragePath,
                destination))
        {
            DataMaintenanceStatus =
                "Alteração cancelada; a pasta atual continua ativa.";
            return;
        }

        var operationResult = await RunDataOperationAsync(
            "data-relocation",
            "Preparar nova pasta de dados",
            "Validando a pasta de destino",
            () => _dataMaintenanceService.RelocateDataDirectory(destination),
            cancellationToken);
        if (operationResult is null)
        {
            return;
        }

        var (execution, result) = operationResult.Value;
        if (!HandleTerminalOperationState(execution))
        {
            return;
        }

        PendingDataDirectory = result.RequiresRestart
            ? result.NewDirectory
            : string.Empty;
        IsDataRestartRequired = result.RequiresRestart;
        DataMaintenanceStatus = result.RequiresRestart
            ? $"Banco copiado e validado em {result.NewDatabasePath}. "
              + "O local atual foi preservado."
            : "A pasta selecionada já é a pasta de dados ativa.";

        TryRecordActivity(
            "Sistema",
            "Pasta de dados configurada",
            result.RequiresRestart
                ? $"Nova pasta preparada: “{result.NewDirectory}”."
                : "A pasta de dados permaneceu inalterada.",
            ActivityLevel.Information,
            SelectedProject);
        StatusMessage = result.RequiresRestart
            ? "Nova pasta preparada. Feche e abra o AutoAIBuilder para ativá-la; "
              + "o aplicativo não será reiniciado automaticamente."
            : "A pasta de dados já estava configurada.";
    }

    private async Task<(OperationExecution Execution, TResult Result)?>
        RunDataOperationAsync<TResult>(
            string operationType,
            string displayName,
            string initialStep,
            Func<TResult> operation,
            CancellationToken cancellationToken)
    {
        var request = OperationRequest.Create(
            operationType,
            displayName,
            DataStoreResourceKey,
            SelectedProject?.Id,
            DataOperationTimeout);
        TResult operationValue = default!;
        _activeDataOperationId = request.Id;
        DataOperationProgress = 0;
        DataOperationStep = initialStep;
        DataMaintenanceStatus = $"{displayName}: iniciando.";
        IsDataOperationRunning = true;

        var progress = new Progress<OperationProgress>(update =>
        {
            DataOperationProgress = update.Percentage;
            DataOperationStep = update.Message;
            DataMaintenanceStatus =
                $"{displayName}: {update.Message} ({update.Percentage}%).";
        });

        try
        {
            var execution = await _operationCoordinator.RunAsync(
                request,
                async (context, operationCancellation) =>
                {
                    context.Report(10, initialStep);
                    operationCancellation.ThrowIfCancellationRequested();
                    context.Report(30, "Executando operação transacional");
                    operationValue = await Task.Run(
                        operation,
                        operationCancellation);
                    operationCancellation.ThrowIfCancellationRequested();
                    context.Report(90, "Validando o resultado");
                },
                progress,
                cancellationToken);

            return (execution, operationValue);
        }
        catch (OperationConflictException exception)
        {
            ReportDataMaintenanceFailure(
                "A operação não pôde ser iniciada",
                exception);
            return null;
        }
        finally
        {
            _activeDataOperationId = null;
            IsDataOperationRunning = false;
            RefreshOperationExecutions();
        }
    }

    private bool HandleTerminalOperationState(OperationExecution execution)
    {
        switch (execution.Status)
        {
            case OperationExecutionStatus.Succeeded:
                DataOperationProgress = 100;
                DataOperationStep = "Operação concluída";
                return true;
            case OperationExecutionStatus.Cancelled:
                DataMaintenanceStatus =
                    "Operação cancelada com segurança antes da conclusão.";
                StatusMessage = DataMaintenanceStatus;
                return false;
            case OperationExecutionStatus.TimedOut:
                DataMaintenanceStatus =
                    "A operação excedeu o tempo limite e foi interrompida.";
                StatusMessage = DataMaintenanceStatus;
                return false;
            default:
                var errorMessage =
                    execution.ErrorMessage ?? "detalhe indisponível";
                DataMaintenanceStatus =
                    $"A operação falhou: {errorMessage}.";
                StatusMessage = DataMaintenanceStatus;
                return false;
        }
    }

    private void CancelDataOperation()
    {
        if (_activeDataOperationId is not Guid executionId
            || !_operationCoordinator.Cancel(executionId))
        {
            DataMaintenanceStatus =
                "Nenhuma operação ativa pôde ser cancelada.";
            return;
        }

        DataOperationStep = "Cancelamento solicitado";
        DataMaintenanceStatus =
            "Cancelamento solicitado. O motor concluirá a interrupção segura.";
    }

    private void RaiseDataOperationCommandStates()
    {
        (CreateDataBackupCommand as AsyncCommand)?.RaiseCanExecuteChanged();
        (RestoreDataBackupCommand as AsyncCommand)?.RaiseCanExecuteChanged();
        (ChangeDataDirectoryCommand as AsyncCommand)?.RaiseCanExecuteChanged();
        (CancelDataOperationCommand as RelayCommand)?.RaiseCanExecuteChanged();
    }

    private void ReportDataMaintenanceFailure(
        string operation,
        Exception exception)
    {
        var message = GetFriendlyMessage(exception);
        DataMaintenanceStatus = $"{operation}: {message}";
        StatusMessage = DataMaintenanceStatus;
        TryWriteDiagnostic(
            DiagnosticLevel.Error,
            "DataMaintenance",
            operation,
            exception);
    }
}
