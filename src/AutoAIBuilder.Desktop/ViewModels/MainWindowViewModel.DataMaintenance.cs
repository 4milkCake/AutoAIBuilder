using System.IO;
using System.Windows;
using AutoAIBuilder.Application.Diagnostics;
using AutoAIBuilder.Application.History;

namespace AutoAIBuilder.Desktop.ViewModels;

public sealed partial class MainWindowViewModel
{
    private string _dataMaintenanceStatus =
        "Nenhuma operação de manutenção executada nesta sessão.";
    private string _pendingDataDirectory = string.Empty;
    private bool _isDataRestartRequired;

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

    private void CreateDataBackup()
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

        try
        {
            var result = _dataMaintenanceService.CreateBackup(destination);
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
        catch (Exception exception)
        {
            ReportDataMaintenanceFailure(
                "Não foi possível criar o backup",
                exception);
        }
    }

    private void RestoreDataBackup()
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

        try
        {
            var result = _dataMaintenanceService.RestoreBackup(source);

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
        catch (Exception exception)
        {
            ReportDataMaintenanceFailure(
                "Não foi possível restaurar o backup",
                exception);
        }
    }

    private void ChangeDataDirectory()
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

        try
        {
            var result =
                _dataMaintenanceService.RelocateDataDirectory(destination);
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
        catch (Exception exception)
        {
            ReportDataMaintenanceFailure(
                "Não foi possível alterar a pasta de dados",
                exception);
        }
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
