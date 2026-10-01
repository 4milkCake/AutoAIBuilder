using AutoAIBuilder.Application.Automation;
using AutoAIBuilder.Application.Automation.Adapters;
using AutoAIBuilder.Application.Automation.Catalog;
using AutoAIBuilder.Application.Automation.Orchestration;
using AutoAIBuilder.Application.Automation.Supervised;
using AutoAIBuilder.Application.Diagnostics;
using AutoAIBuilder.Application.Automation.Validation;
using AutoAIBuilder.Application.CadVisualization;
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
using AutoAIBuilder.Domain.Recognition;
using AutoAIBuilder.Infrastructure.Dashboard;
using AutoAIBuilder.Infrastructure.Automation;
using AutoAIBuilder.Infrastructure.Automation.Adapters;
using AutoAIBuilder.Infrastructure.Automation.Catalog;
using AutoAIBuilder.Infrastructure.Automation.Pilots;
using AutoAIBuilder.Infrastructure.Automation.Supervised;
using AutoAIBuilder.Infrastructure.CadVisualization;
using AutoAIBuilder.Infrastructure.Persistence;
using AutoAIBuilder.Infrastructure.Recognition;
using AutoAIBuilder.Infrastructure.Semantics;
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
    public void RecognitionOverlaySelection_RevealsCandidateHiddenByFilter()
    {
        var logger = new InMemoryDiagnosticLogger();
        var viewModel = CreateViewModel(
            logger,
            new StubDiagnosticService(logger));
        var highConfidence = CreateRecognitionCandidate(
            Guid.NewGuid(),
            "ALTA",
            92);
        var hiddenCandidate = CreateRecognitionCandidate(
            Guid.NewGuid(),
            "OCULTO",
            0);
        var field = typeof(MainWindowViewModel).GetField(
            "_allRecognitionCandidates",
            System.Reflection.BindingFlags.Instance
            | System.Reflection.BindingFlags.NonPublic);
        Assert.IsNotNull(field);
        var allCandidates =
            (List<RecognitionCandidateItemViewModel>)field.GetValue(viewModel)!;
        allCandidates.AddRange([highConfidence, hiddenCandidate]);
        viewModel.SelectedRecognitionFilter = "Alta confiança";
        Assert.AreEqual(1, viewModel.RecognitionCandidates.Count);

        viewModel.SelectRecognitionOverlayPointCommand.Execute(
            new CadOverlayPointViewModel(
                hiddenCandidate.Id,
                hiddenCandidate.Handle,
                100,
                200,
                "#627087",
                "Candidato oculto"));

        Assert.AreEqual("Todos", viewModel.SelectedRecognitionFilter);
        Assert.AreSame(
            hiddenCandidate,
            viewModel.SelectedRecognitionCandidate);
        CollectionAssert.Contains(
            viewModel.RecognitionCandidates,
            hiddenCandidate);
    }

    [TestMethod]
    public void FullReadinessFlow_CreatesProjectAndKeepsRealAdaptersDisconnected()
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
        Assert.IsTrue(viewModel.Navigation.Single(item => item.Label == "Automação").IsAvailable);
        Assert.IsTrue(viewModel.Navigation.Single(item => item.Label == "Máscaras").IsAvailable);
    }

    [TestMethod]
    public async Task AutomationPilot_FollowsSimulationConfirmationCopyAndAuditFlow()
    {
        var logger = new InMemoryDiagnosticLogger();
        var inputPath = Path.Combine(_directory, "entrada-piloto.dwg");
        var outputRoot = Path.Combine(_directory, "pilot-output");
        File.WriteAllText(inputPath, "arquivo técnico de integração");
        var viewModel = CreateViewModel(
            logger,
            new StubDiagnosticService(logger),
            filePicker: new PilotFilePicker(inputPath, outputRoot),
            dialogService: new AcceptingPilotDialogService());

        viewModel.ImportFileCommand.Execute(null);
        viewModel.OpenAutomationCommand.Execute(null);
        viewModel.ChooseAutomationOutputCommand.Execute(null);

        Assert.AreEqual(
            WorkspaceSection.Automation,
            viewModel.CurrentSection);
        Assert.IsNotNull(viewModel.SelectedAutomationInput);
        Assert.AreEqual(outputRoot, viewModel.AutomationOutputRoot);

        await ((AsyncCommand)viewModel.SimulateAutomationPilotCommand)
            .ExecuteAsync();

        Assert.AreEqual(
            "SIMULAÇÃO APROVADA",
            viewModel.AutomationPilotStage);
        Assert.IsFalse(Directory.Exists(outputRoot));
        Assert.IsTrue(viewModel.AutomationPlanActions.Count >= 7);
        Assert.AreEqual(0, viewModel.AutomationPilotIssues.Count);

        await ((AsyncCommand)viewModel.ExecuteAutomationPilotCommand)
            .ExecuteAsync();

        Assert.AreEqual(
            "EXECUÇÃO CONCLUÍDA",
            viewModel.AutomationPilotStage);
        Assert.IsTrue(Directory.Exists(viewModel.AutomationPublishedPath));
        Assert.IsTrue(viewModel.AutomationOutputFiles.Count >= 2);
        Assert.IsTrue(viewModel.OperationExecutions.Count >= 2);
        Assert.AreEqual(
            "arquivo técnico de integração",
            File.ReadAllText(inputPath));
    }

    [TestMethod]
    public async Task MaskCatalog_AnalyzesImportsInactiveAndActivatesWithoutExecution()
    {
        var logger = new InMemoryDiagnosticLogger();
        var serializer = new AutomationContractJsonSerializer();
        var maskPath = Path.Combine(_directory, "mask.json");
        var catalogPath = Path.Combine(_directory, "rules.json");
        File.WriteAllText(
            maskPath,
            serializer.Serialize(AutomationTestData.CreateMask()));
        File.WriteAllText(
            catalogPath,
            serializer.Serialize(AutomationTestData.CreateCatalog()));
        var viewModel = CreateViewModel(
            logger,
            new StubDiagnosticService(logger),
            filePicker: new MaskPackageFilePicker(
                maskPath,
                catalogPath),
            dialogService: new AcceptingMaskDialogService());

        viewModel.OpenMasksCommand.Execute(null);
        viewModel.ChooseMaskContractCommand.Execute(null);
        viewModel.ChooseRuleCatalogCommand.Execute(null);
        await ((AsyncCommand)viewModel.AnalyzeMaskPackageCommand)
            .ExecuteAsync();

        Assert.AreEqual(WorkspaceSection.Masks, viewModel.CurrentSection);
        Assert.AreEqual(
            System.Windows.Visibility.Visible,
            viewModel.MasksVisibility);
        Assert.AreEqual("ANÁLISE APROVADA", viewModel.MaskCatalogStage);
        Assert.IsTrue(viewModel.ImportMaskPackageCommand.CanExecute(null));

        viewModel.ImportMaskPackageCommand.Execute(null);

        Assert.AreEqual("IMPORTADA — INATIVA", viewModel.MaskCatalogStage);
        Assert.AreEqual(1, viewModel.ImportedMaskCount);
        Assert.AreEqual(0, viewModel.ActiveMaskCount);
        Assert.IsFalse(viewModel.MaskCatalogEntries.Single().IsActive);

        viewModel.ToggleMaskCatalogEntryCommand.Execute(
            viewModel.MaskCatalogEntries.Single());

        Assert.AreEqual("ATIVA PARA INTEGRAÇÃO", viewModel.MaskCatalogStage);
        Assert.AreEqual(1, viewModel.ActiveMaskCount);
        Assert.IsTrue(viewModel.MaskCatalogEntries.Single().IsActive);
        Assert.AreEqual(1, viewModel.RegisteredAdapterCount);
        Assert.AreEqual(
            "SEM ADAPTADOR REGISTRADO",
            viewModel.MaskCatalogEntries.Single().IntegrationStatus);

        viewModel.AssessMaskIntegrationCommand.Execute(
            viewModel.MaskCatalogEntries.Single());

        Assert.AreEqual("SEM ADAPTADOR", viewModel.MaskCatalogStage);
        Assert.AreEqual(1, viewModel.IntegrationAssessments.Count);
        Assert.AreEqual(
            "SEM ADAPTADOR",
            viewModel.IntegrationAssessments.Single().Status);
        Assert.AreEqual(
            serializer.Serialize(AutomationTestData.CreateMask()),
            File.ReadAllText(maskPath));
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
        IDialogService? dialogService = null,
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
        var automationAuditRepository =
            new SqliteAutomationAuditRepository(operationDatabase);
        var serializer = new AutomationContractJsonSerializer();
        var validator = new AutomationContractValidator();
        IAutomationMaskCatalogRepository maskCatalogRepository =
            new SqliteAutomationMaskCatalogRepository(operationDatabase);
        IAutomationIntegrationAssessmentRepository assessmentRepository =
            new SqliteAutomationIntegrationAssessmentRepository(
                operationDatabase);
        IAutomationAdapterRegistry adapterRegistry =
            new AutomationAdapterRegistry(
                [new VerifiedCopyAutomationAdapter(serializer)]);
        IAutomationOrchestrator automationOrchestrator =
            new SafeAutomationOrchestrator(
                maskCatalogRepository,
                adapterRegistry,
                assessmentRepository,
                new AutomationExecutionService(
                    automationAuditRepository,
                    [new VerifiedCopyPilotValidator()]),
                serializer,
                validator);
        var verifiedCopyPilot = new VerifiedCopyPilotService(
            new AutomationPlanService(validator),
            automationOrchestrator,
            automationAuditRepository);
        var maskCatalogService = new AutomationMaskCatalogService(
            maskCatalogRepository,
            serializer,
            validator);

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
            dialogService ?? new RejectingDialogService(),
            dataMaintenanceService ?? new StubDataMaintenanceService(_directory),
            operationCoordinator,
            verifiedCopyPilot,
            maskCatalogService,
            automationOrchestrator,
            new LegacyAutomationBridgeService(),
            new AutomationPreviewService(
                new SqliteAutomationPreviewDecisionRepository(
                    operationDatabase)),
            new SupervisedAutomationService(
                new UnavailableSupervisedCadRunner()),
            new SemanticCsvWorkspaceService(
                new SqliteSemanticDatasetRepository(operationDatabase)),
            new CadVisualizationService(
                new UnavailableCadGeometryExporter(),
                storageRoot: Path.Combine(_directory, "cad-visualization")),
            new CadRecognitionService(
                new UnavailableCadEntityInventoryExporter(),
                storageRoot: Path.Combine(_directory, "recognition")));
    }

    private static RecognitionCandidateItemViewModel CreateRecognitionCandidate(
        Guid id,
        string handle,
        int confidenceScore) =>
        new(
            id,
            handle,
            "INSERT",
            "LAYER",
            $"BLOCO-{handle}",
            "100; 200",
            "0°",
            "A confirmar",
            string.Empty,
            "Símbolo ainda não classificado",
            string.Empty,
            string.Empty,
            confidenceScore >= 85 ? "Alta" : "Desconhecida",
            confidenceScore,
            "Teste",
            "Pendente",
            "#627087",
            string.Empty,
            RecognitionCandidateStatus.Pending);

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

    private sealed class UnavailableCadGeometryExporter : ICadGeometryExporter
    {
        public CadExporterStatus GetStatus() => new(
            false,
            "AutoCAD de teste",
            "Indisponível",
            string.Empty,
            string.Empty,
            "Indisponível durante os testes da interface.");

        public Task ExportAsync(
            string sourceCopyPath,
            string artifactPath,
            string workingDirectory,
            CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Exportação não esperada.");
    }

    private sealed class UnavailableCadEntityInventoryExporter :
        ICadEntityInventoryExporter
    {
        public CadEntityInventoryExporterStatus GetStatus() => new(
            false,
            "AutoCAD de teste",
            "Indisponível",
            string.Empty,
            string.Empty,
            "Indisponível durante os testes da interface.");

        public Task ExportAsync(
            string sourceCopyPath,
            string inventoryPath,
            string workingDirectory,
            CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Inventário não esperado.");
    }

    private sealed class UnavailableSupervisedCadRunner :
        ISupervisedCadRunner
    {
        public CadExporterStatus GetStatus() => new(
            false,
            "AutoCAD de teste",
            "Indisponível",
            string.Empty,
            string.Empty,
            "Indisponível durante os testes da interface.");

        public Task<SupervisedCadRunnerResult> RunAsync(
            SupervisedCadRunnerRequest request,
            CancellationToken cancellationToken) =>
            throw new InvalidOperationException(
                "Execução supervisionada não esperada.");
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

        public string? PickAutomationMaskContract() => null;

        public string? PickAutomationRuleCatalog() => null;

        public string? PickAutomationOutputDirectory(string? currentDirectory) =>
            null;

        public string? PickDataBackupDestination(string suggestedFileName) => null;

        public string? PickDataBackupSource() => null;

        public string? PickDataDirectory(string currentDirectory) => null;
    }

    private sealed class BackupFilePicker(string destination)
        : IFilePickerService
    {
        public IReadOnlyList<string> PickProjectFiles() => [];

        public string? PickAutomationMaskContract() => null;

        public string? PickAutomationRuleCatalog() => null;

        public string? PickAutomationOutputDirectory(string? currentDirectory) =>
            null;

        public string? PickDataBackupDestination(string suggestedFileName) =>
            destination;

        public string? PickDataBackupSource() => null;

        public string? PickDataDirectory(string currentDirectory) => null;
    }

    private sealed class PilotFilePicker(
        string inputPath,
        string outputDirectory) : IFilePickerService
    {
        public IReadOnlyList<string> PickProjectFiles() => [inputPath];

        public string? PickAutomationMaskContract() => null;

        public string? PickAutomationRuleCatalog() => null;

        public string? PickAutomationOutputDirectory(string? currentDirectory) =>
            outputDirectory;

        public string? PickDataBackupDestination(string suggestedFileName) =>
            null;

        public string? PickDataBackupSource() => null;

        public string? PickDataDirectory(string currentDirectory) => null;
    }

    private sealed class MaskPackageFilePicker(
        string maskPath,
        string catalogPath) : IFilePickerService
    {
        public IReadOnlyList<string> PickProjectFiles() => [];

        public string? PickAutomationMaskContract() => maskPath;

        public string? PickAutomationRuleCatalog() => catalogPath;

        public string? PickAutomationOutputDirectory(string? currentDirectory) =>
            null;

        public string? PickDataBackupDestination(string suggestedFileName) =>
            null;

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

        public bool ConfirmVerifiedCopyExecution(
            string fileName,
            string outputRoot,
            string sha256) => false;

        public bool ConfirmMaskCatalogActivation(
            string maskName,
            string maskVersion,
            bool activate) => false;
    }

    private sealed class AcceptingPilotDialogService : IDialogService
    {
        public bool ConfirmRemoveFileReference(string fileName) => false;

        public bool ConfirmRestoreDataBackup(string backupPath) => false;

        public bool ConfirmDataDirectoryChange(
            string currentDirectory,
            string newDirectory) => false;

        public bool ConfirmVerifiedCopyExecution(
            string fileName,
            string outputRoot,
            string sha256) => true;

        public bool ConfirmMaskCatalogActivation(
            string maskName,
            string maskVersion,
            bool activate) => false;
    }

    private sealed class AcceptingMaskDialogService : IDialogService
    {
        public bool ConfirmRemoveFileReference(string fileName) => false;

        public bool ConfirmRestoreDataBackup(string backupPath) => false;

        public bool ConfirmDataDirectoryChange(
            string currentDirectory,
            string newDirectory) => false;

        public bool ConfirmVerifiedCopyExecution(
            string fileName,
            string outputRoot,
            string sha256) => false;

        public bool ConfirmMaskCatalogActivation(
            string maskName,
            string maskVersion,
            bool activate) => true;
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
