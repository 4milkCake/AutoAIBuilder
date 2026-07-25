using System.IO;
using AutoAIBuilder.Application.Dashboard;
using AutoAIBuilder.Application.Diagnostics;
using AutoAIBuilder.Application.History;
using AutoAIBuilder.Application.Maintenance;
using AutoAIBuilder.Application.Navigation;
using AutoAIBuilder.Application.Notifications;
using AutoAIBuilder.Application.Operations;
using AutoAIBuilder.Application.Projects;
using AutoAIBuilder.Application.Reports;
using AutoAIBuilder.Application.Settings;
using AutoAIBuilder.Application.Validation;
using AutoAIBuilder.Desktop.Services;
using AutoAIBuilder.Desktop.ViewModels;
using AutoAIBuilder.Infrastructure.Dashboard;
using AutoAIBuilder.Infrastructure.Diagnostics;
using AutoAIBuilder.Infrastructure.Persistence;
using AutoAIBuilder.Infrastructure.Reports;

namespace AutoAIBuilder.Desktop.Composition;

public static class DesktopCompositionRoot
{
    private static readonly Lazy<IDiagnosticLogger> DiagnosticLoggerFactory =
        new(JsonLinesDiagnosticLogger.CreateDefault);

    public static IDiagnosticLogger DiagnosticLogger => DiagnosticLoggerFactory.Value;

    public static SqliteDatabase InitializeDataStore()
    {
        ApplyPendingDataDirectoryChange();

        var database = new SqliteDatabase(AppStoragePaths.DatabaseFile);
        database.Initialize();

        var migrationResult = LegacyJsonDataMigrator
            .CreateDefault(database)
            .Run();
        WriteMigrationDiagnostics(migrationResult);
        return database;
    }

    private static void ApplyPendingDataDirectoryChange()
    {
        var pendingDirectory =
            StorageLocationConfiguration.GetPendingDataDirectory();
        if (pendingDirectory is null)
        {
            return;
        }

        var currentDatabasePath = AppStoragePaths.DatabaseFile;
        var pendingDatabasePath = Path.Combine(
            pendingDirectory,
            Path.GetFileName(currentDatabasePath));

        if (File.Exists(currentDatabasePath))
        {
            var currentDatabase = new SqliteDatabase(currentDatabasePath);
            currentDatabase.Initialize();
            new SqliteDataMaintenanceService(
                    currentDatabase,
                    _ => { })
                .CreateBackup(pendingDatabasePath);
        }
        else
        {
            new SqliteDatabase(pendingDatabasePath).Initialize();
        }

        StorageLocationConfiguration.ActivatePendingDataDirectory();
    }

    public static MainWindowViewModel CreateMainWindowViewModel()
    {
        var database = InitializeDataStore();
        var projectRepository = new SqliteProjectRepository(database);
        var workspaceService = new ProjectWorkspaceService(projectRepository);
        var settingsService = new ApplicationSettingsService(
            new SqliteApplicationSettingsRepository(database));
        var activityLogService = new ActivityLogService(
            new SqliteActivityLogRepository(database));
        var diagnosticService = new EnvironmentDiagnosticService(
            DiagnosticLogger,
            database);
        IDataMaintenanceService dataMaintenanceService =
            new SqliteDataMaintenanceService(database);
        IOperationCoordinator operationCoordinator =
            new OperationCoordinator(
                new SqliteOperationExecutionRepository(database),
                DiagnosticLogger);
        operationCoordinator.RecoverInterruptedOperations();

        return new MainWindowViewModel(
            new ProjectDashboardProvider(),
            workspaceService,
            settingsService,
            new ProjectValidationService(),
            new ProjectReportService(),
            activityLogService,
            new ActiveProjectContext(
                new SqliteActiveProjectStateRepository(database)),
            DiagnosticLogger,
            diagnosticService,
            new NavigationService(),
            new NotificationService(),
            new FilePickerService(),
            new ReportExportService(new SimplePdfReportRenderer()),
            new FileSystemLauncher(),
            new DialogService(),
            dataMaintenanceService,
            operationCoordinator);
    }

    private static void WriteMigrationDiagnostics(
        LegacyJsonMigrationResult result)
    {
        if (result.ImportedAnything)
        {
            DiagnosticLogger.Write(
                DiagnosticLevel.Information,
                "DataMigration",
                "Dados JSON legados importados para o banco SQLite.",
                properties: new Dictionary<string, string>
                {
                    ["projects"] = result.ImportedProjects.ToString(),
                    ["settings"] = result.ImportedSettings.ToString(),
                    ["activityEntries"] = result.ImportedActivityEntries.ToString()
                });
        }

        foreach (var warning in result.Warnings)
        {
            DiagnosticLogger.Write(
                DiagnosticLevel.Warning,
                "DataMigration",
                warning);
        }
    }
}
