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
using AutoAIBuilder.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;

namespace AutoAIBuilder.Tests;

[TestClass]
public sealed class MainWindowViewModelIntegrationTests
{
    private string _directory = null!;

    [TestInitialize]
    public void Initialize()
    {
        _directory = Path.Combine(
            Path.GetTempPath(),
            "AutoAIBuilder.Tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_directory);
    }

    [TestCleanup]
    public void Cleanup()
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [TestMethod]
    public void Shell_NavigatesToDiagnosticsAndLoadsSnapshot()
    {
        var logger = new InMemoryDiagnosticLogger();
        var viewModel = CreateViewModel(
            logger,
            new StubDiagnosticService(logger));

        viewModel.OpenDiagnosticsCommand.Execute(null);

        Assert.AreEqual(WorkspaceSection.Diagnostics, viewModel.CurrentSection);
        Assert.AreEqual(1, viewModel.DiagnosticChecks.Count);
        Assert.IsTrue(viewModel.DiagnosticLogs.Count >= 1);
        StringAssert.Contains(viewModel.DiagnosticSummary, "1 item");
        Assert.AreEqual(System.Windows.Visibility.Visible, viewModel.DiagnosticsVisibility);
    }

    [TestMethod]
    public void FullReadinessFlow_CreatesProjectValidatesReportsAndKeepsAutomationDisconnected()
    {
        var logger = new InMemoryDiagnosticLogger();
        var viewModel = CreateViewModel(
            logger,
            new StubDiagnosticService(logger));

        Assert.IsNotNull(viewModel.SelectedProject);
        viewModel.RunProjectValidationCommand.Execute(null);
        viewModel.GenerateProjectReportCommand.Execute(null);
        viewModel.OpenReportsCommand.Execute(null);

        Assert.AreEqual(WorkspaceSection.Reports, viewModel.CurrentSection);
        Assert.IsTrue(viewModel.ValidationResults.Count > 0);
        StringAssert.Contains(viewModel.ReportContent, "RELATÓRIO DE PRONTIDÃO");
        Assert.IsFalse(viewModel.Navigation.Single(item => item.Label == "Automação").IsAvailable);
        Assert.IsFalse(viewModel.Navigation.Single(item => item.Label == "Máscaras").IsAvailable);
    }

    [TestMethod]
    public void EmptyStorage_DoesNotCreateDemonstrationProject()
    {
        var logger = new InMemoryDiagnosticLogger();
        var viewModel = CreateViewModel(
            logger,
            new StubDiagnosticService(logger),
            seedProject: false);

        Assert.IsNull(viewModel.SelectedProject);
        Assert.AreEqual(0, viewModel.Projects.Count);
        Assert.AreEqual(0, viewModel.AvailableProjects.Count);
        StringAssert.Contains(viewModel.StatusMessage, "Crie um projeto");
    }

    [TestMethod]
    public async Task DataBackupCommand_UsesOperationalEngineAndPersistsResult()
    {
        var logger = new InMemoryDiagnosticLogger();
        var destination = Path.Combine(_directory, "backup.aabbackup");
        var viewModel = CreateViewModel(
            logger,
            new StubDiagnosticService(logger),
            filePicker: new BackupFilePicker(destination));

        var command = (AsyncCommand)viewModel.CreateDataBackupCommand;
        await command.ExecuteAsync();

        Assert.IsFalse(viewModel.IsDataOperationRunning);
        Assert.AreEqual(100, viewModel.DataOperationProgress);
        Assert.AreEqual(1, viewModel.OperationExecutions.Count);
        Assert.AreEqual(
            "Concluída",
            viewModel.OperationExecutions.Single().Status);
        StringAssert.Contains(viewModel.DataMaintenanceStatus, destination);
        Assert.IsTrue(
            logger.GetRecent().Any(entry =>
                entry.Source == "OperationEngine"));
    }

    private MainWindowViewModel CreateViewModel(
        IDiagnosticLogger logger,
        IDiagnosticService diagnosticService,
        bool seedProject = true,
        IFilePickerService? filePicker = null,
        IDataMaintenanceService? dataMaintenanceService = null,
        IOperationCoordinator? operationCoordinator = null)
    {
        var workspaceService = new ProjectWorkspaceService(
            new JsonProjectRepository(Path.Combine(_directory, "projects.json")));
        if (seedProject)
        {
            workspaceService.CreateProject(new CreateProjectRequest(
                "Projeto integrado",
                "Residencial",
                2,
                4));
        }
        var settingsService = new ApplicationSettingsService(
            new JsonApplicationSettingsRepository(Path.Combine(_directory, "settings.json")));
        var activityService = new ActivityLogService(
            new JsonActivityLogRepository(Path.Combine(_directory, "activity.json")));
        var operationDatabase = new SqliteDatabase(
            Path.Combine(_directory, "operations.db"));
        operationDatabase.Initialize();
        operationCoordinator ??= new OperationCoordinator(
            new SqliteOperationExecutionRepository(operationDatabase),
            logger);

        return new MainWindowViewModel(
            new ProjectDashboardProvider(),
            workspaceService,
            settingsService,
            new ProjectValidationService(),
            new ProjectReportService(),
            activityService,
            new ActiveProjectContext(),
            logger,
            diagnosticService,
            new NavigationService(),
            new NotificationService(),
            filePicker ?? new EmptyFilePicker(),
            new CancelledReportExportService(),
            new NoOpFileSystemLauncher(),
            new RejectingDialogService(),
            dataMaintenanceService ?? new StubDataMaintenanceService(_directory),
            operationCoordinator);
    }

    private sealed class InMemoryDiagnosticLogger : IDiagnosticLogger
    {
        private readonly List<DiagnosticLogEntry> _entries = [];

        public string StoragePath => Path.Combine(Path.GetTempPath(), "in-memory.jsonl");

        public IReadOnlyList<DiagnosticLogEntry> GetRecent(int maximumEntries = 100) =>
            _entries.TakeLast(maximumEntries).Reverse().ToArray();

        public void Write(
            DiagnosticLevel level,
            string source,
            string message,
            Exception? exception = null,
            IReadOnlyDictionary<string, string>? properties = null) =>
            _entries.Add(new DiagnosticLogEntry(
                DateTimeOffset.Now,
                level,
                source,
                message,
                exception?.GetType().FullName,
                exception?.Message,
                exception?.StackTrace,
                properties ?? new Dictionary<string, string>()));
    }

    private sealed class StubDiagnosticService(IDiagnosticLogger logger) : IDiagnosticService
    {
        public DiagnosticSnapshot Capture() => new(
            DateTimeOffset.Now,
            [
                new DiagnosticCheck(
                    "Teste integrado",
                    "Ativo",
                    DiagnosticStatus.Healthy,
                    "Composição carregada.")
            ],
            logger.GetRecent());
    }

    private sealed class EmptyFilePicker : IFilePickerService
    {
        public IReadOnlyList<string> PickProjectFiles() => [];

        public string? PickDataBackupDestination(string suggestedFileName) => null;

        public string? PickDataBackupSource() => null;

        public string? PickDataDirectory(string currentDirectory) => null;
    }

    private sealed class BackupFilePicker(string destination)
        : IFilePickerService
    {
        public IReadOnlyList<string> PickProjectFiles() => [];

        public string? PickDataBackupDestination(string suggestedFileName) =>
            destination;

        public string? PickDataBackupSource() => null;

        public string? PickDataDirectory(string currentDirectory) => null;
    }

    private sealed class CancelledReportExportService : IReportExportService
    {
        public string? ExportTextReport(string suggestedFileName, string content) => null;

        public string? ExportCsvReport(string suggestedFileName, string content) => null;

        public string? ExportPdfReport(string suggestedFileName, string content) => null;
    }

    private sealed class NoOpFileSystemLauncher : IFileSystemLauncher
    {
        public void OpenFile(string filePath)
        {
        }

        public void RevealFile(string filePath)
        {
        }
    }

    private sealed class RejectingDialogService : IDialogService
    {
        public bool ConfirmRemoveFileReference(string fileName) => false;

        public bool ConfirmRestoreDataBackup(string backupPath) => false;

        public bool ConfirmDataDirectoryChange(
            string currentDirectory,
            string newDirectory) => false;
    }

    private sealed class StubDataMaintenanceService(string directory)
        : IDataMaintenanceService
    {
        public string DataDirectory => directory;

        public string DatabasePath => Path.Combine(directory, "test.db");

        public string BackupDirectory => Path.Combine(directory, "Backups");

        public int SchemaVersion => SqliteDatabase.CurrentSchemaVersion;

        public DataBackupResult CreateBackup(string destinationPath) =>
            new(destinationPath, DateTimeOffset.Now, 0);

        public DataRestoreResult RestoreBackup(string sourcePath) =>
            new(sourcePath, Path.Combine(directory, "safety.aabbackup"), DateTimeOffset.Now);

        public DataRelocationResult RelocateDataDirectory(string destinationDirectory) =>
            new(
                directory,
                destinationDirectory,
                Path.Combine(destinationDirectory, "test.db"),
                RequiresRestart: true);
    }
}
