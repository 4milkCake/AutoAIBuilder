using System.IO;
using AutoAIBuilder.Application.Automation;
using AutoAIBuilder.Application.Automation.Adapters;
using AutoAIBuilder.Application.Automation.Catalog;
using AutoAIBuilder.Application.Automation.Execution;
using AutoAIBuilder.Application.Automation.Orchestration;
using AutoAIBuilder.Application.Automation.Pilots;
using AutoAIBuilder.Application.Automation.Preview;
using AutoAIBuilder.Application.Automation.Supervised;
using AutoAIBuilder.Application.Automation.Validation;
using AutoAIBuilder.Application.CadVisualization;
using AutoAIBuilder.Application.Dashboard;
using AutoAIBuilder.Application.Diagnostics;
using AutoAIBuilder.Application.History;
using AutoAIBuilder.Application.Maintenance;
using AutoAIBuilder.Application.Navigation;
using AutoAIBuilder.Application.Notifications;
using AutoAIBuilder.Application.Operations;
using AutoAIBuilder.Application.Projects;
using AutoAIBuilder.Application.Reports;
using AutoAIBuilder.Application.Recognition;
using AutoAIBuilder.Application.Settings;
using AutoAIBuilder.Application.Semantics;
using AutoAIBuilder.Application.Validation;
using AutoAIBuilder.Desktop.Services;
using AutoAIBuilder.Desktop.ViewModels;
using AutoAIBuilder.Infrastructure.Dashboard;
using AutoAIBuilder.Infrastructure.Automation;
using AutoAIBuilder.Infrastructure.Automation.Adapters;
using AutoAIBuilder.Infrastructure.Automation.Catalog;
using AutoAIBuilder.Infrastructure.Automation.Pilots;
using AutoAIBuilder.Infrastructure.Automation.Supervised;
using AutoAIBuilder.Infrastructure.CadVisualization;
using AutoAIBuilder.Infrastructure.Diagnostics;
using AutoAIBuilder.Infrastructure.Persistence;
using AutoAIBuilder.Infrastructure.Reports;
using AutoAIBuilder.Infrastructure.Recognition;
using AutoAIBuilder.Infrastructure.Semantics;

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
        IAutomationAuditRepository automationAuditRepository =
            new SqliteAutomationAuditRepository(database);
        IAutomationMaskCatalogRepository maskCatalogRepository =
            new SqliteAutomationMaskCatalogRepository(database);
        IAutomationIntegrationAssessmentRepository assessmentRepository =
            new SqliteAutomationIntegrationAssessmentRepository(database);
        var contractSerializer = new AutomationContractJsonSerializer();
        var contractValidator = new AutomationContractValidator();
        IAutomationAdapterRegistry adapterRegistry =
            new AutomationAdapterRegistry(
                [new VerifiedCopyAutomationAdapter(contractSerializer)]);
        IAutomationExecutionService automationExecutionService =
            new AutomationExecutionService(
                automationAuditRepository,
                [new VerifiedCopyPilotValidator()]);
        IAutomationOrchestrator automationOrchestrator =
            new SafeAutomationOrchestrator(
                maskCatalogRepository,
                adapterRegistry,
                assessmentRepository,
                automationExecutionService,
                contractSerializer,
                contractValidator);
        var interruptedAutomations =
            automationAuditRepository.MarkIncompleteAsInterrupted(
                DateTimeOffset.UtcNow,
                "A execução foi interrompida pelo encerramento anterior do aplicativo; "
                + "os artefatos existentes foram preservados.");
        var activityLogService = new ActivityLogService(
            new SqliteActivityLogRepository(database));
        var diagnosticService = new EnvironmentDiagnosticService(
            DiagnosticLogger,
            database,
            automationAuditRepository,
            maskCatalogRepository,
            adapterRegistry,
            assessmentRepository);
        IDataMaintenanceService dataMaintenanceService =
            new SqliteDataMaintenanceService(database);
        IOperationCoordinator operationCoordinator =
            new OperationCoordinator(
                new SqliteOperationExecutionRepository(database),
                DiagnosticLogger);
        IVerifiedCopyPilotService verifiedCopyPilotService =
            new VerifiedCopyPilotService(
                new AutomationPlanService(contractValidator),
                automationOrchestrator,
                automationAuditRepository);
        IAutomationMaskCatalogService automationMaskCatalogService =
            new AutomationMaskCatalogService(
                maskCatalogRepository,
                contractSerializer,
                contractValidator);
        ISemanticWorkspaceService semanticWorkspaceService =
            new SemanticCsvWorkspaceService(
                new SqliteSemanticDatasetRepository(database));
        IAutomationPreviewService automationPreviewService =
            new AutomationPreviewService(
                new SqliteAutomationPreviewDecisionRepository(database));
        ISupervisedAutomationService supervisedAutomationService =
            new SupervisedAutomationService(
                new AutoCadSupervisedRunner());
        ICadVisualizationService cadVisualizationService =
            new CadVisualizationService(
                new AutoCadCoreConsoleExporter());
        ICadRecognitionService cadRecognitionService =
            new CadRecognitionService(
                new AutoCadEntityInventoryExporter());
        operationCoordinator.RecoverInterruptedOperations();
        if (interruptedAutomations > 0)
        {
            DiagnosticLogger.Write(
                DiagnosticLevel.Warning,
                "AutomationSafety",
                "Auditorias incompletas foram marcadas como interrompidas.",
                properties: new Dictionary<string, string>
                {
                    ["count"] = interruptedAutomations.ToString()
                });
        }

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
            operationCoordinator,
            verifiedCopyPilotService,
            automationMaskCatalogService,
            automationOrchestrator,
            new LegacyAutomationBridgeService(),
            automationPreviewService,
            supervisedAutomationService,
            semanticWorkspaceService,
            cadVisualizationService,
            cadRecognitionService);
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
