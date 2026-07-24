using AutoAIBuilder.Application.Diagnostics;
using AutoAIBuilder.Application.History;
using AutoAIBuilder.Application.Navigation;
using AutoAIBuilder.Application.Notifications;
using AutoAIBuilder.Application.Projects;
using AutoAIBuilder.Application.Reports;
using AutoAIBuilder.Application.Settings;
using AutoAIBuilder.Application.Validation;
using AutoAIBuilder.Desktop.Services;
using AutoAIBuilder.Desktop.ViewModels;
using AutoAIBuilder.Infrastructure.Dashboard;
using AutoAIBuilder.Infrastructure.Persistence;

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

    private MainWindowViewModel CreateViewModel(
        IDiagnosticLogger logger,
        IDiagnosticService diagnosticService)
    {
        var workspaceService = new ProjectWorkspaceService(
            new JsonProjectRepository(Path.Combine(_directory, "projects.json")));
        var settingsService = new ApplicationSettingsService(
            new JsonApplicationSettingsRepository(Path.Combine(_directory, "settings.json")));
        var activityService = new ActivityLogService(
            new JsonActivityLogRepository(Path.Combine(_directory, "activity.json")));

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
            new EmptyFilePicker(),
            new CancelledReportExportService(),
            new NoOpFileSystemLauncher(),
            new RejectingDialogService());
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
    }
}
