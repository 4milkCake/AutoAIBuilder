using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using AutoAIBuilder.Application.Automation;
using AutoAIBuilder.Application.Automation.Catalog;
using AutoAIBuilder.Application.Automation.Pilots;
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
using AutoAIBuilder.Desktop.ViewModels.Modules;
using AutoAIBuilder.Domain.Automation;
using AutoAIBuilder.Domain.Projects;

namespace AutoAIBuilder.Desktop.ViewModels;

public sealed partial class MainWindowViewModel : INotifyPropertyChanged
{
    private readonly IDashboardProvider _dashboardProvider;
    private readonly ProjectWorkspaceService _workspaceService;
    private readonly ApplicationSettingsService _settingsService;
    private readonly ProjectValidationService _validationService;
    private readonly ProjectReportService _reportService;
    private readonly ActivityLogService _activityLogService;
    private readonly ActiveProjectContext _activeProjectContext;
    private readonly IDiagnosticLogger _diagnosticLogger;
    private readonly IDiagnosticService _diagnosticService;
    private readonly INavigationService _navigationService;
    private readonly INotificationService _notificationService;
    private readonly IFilePickerService _filePicker;
    private readonly IReportExportService _reportExportService;
    private readonly IFileSystemLauncher _fileSystemLauncher;
    private readonly IDialogService _dialogService;
    private readonly IDataMaintenanceService _dataMaintenanceService;
    private readonly IOperationCoordinator _operationCoordinator;
    private readonly IVerifiedCopyPilotService _verifiedCopyPilotService;
    private readonly IAutomationMaskCatalogService _automationMaskCatalogService;
    private readonly IAutomationOrchestrator _automationOrchestrator;
    private readonly WorkspaceModuleCatalog _moduleCatalog;
    private readonly List<ProjectFileItemViewModel> _allProjectFiles = [];
    private readonly List<ActivityHistoryItemViewModel> _allHistoryEntries = [];
    private ApplicationSettings _applicationSettings;
    private string _statusMessage = "Aplicativo iniciado.";
    private string _projectFormError = string.Empty;
    private WorkspaceSection _currentSection;
    private ProjectListItemViewModel? _selectedProject;
    private ProjectListItemViewModel? _selectedManagedProject;
    private Guid? _editingProjectId;
    private string _projectSearchText = string.Empty;
    private string _selectedProjectSort = "Atualizados recentemente";
    private string _fileSearchText = string.Empty;
    private string _selectedFileFilter = "Todos";
    private string _newProjectName = string.Empty;
    private string _newProjectType = "Residencial";
    private string _newProjectFloorsText = "1";
    private string _newProjectUnitsText = "1";
    private string _projectName = "Nenhum projeto";
    private string _projectType = "Selecione ou crie um projeto";
    private string _projectDetails = "Sem dados";
    private string _disciplines = "Sem disciplinas";
    private string _updatedAt = "Ainda não atualizado";
    private string _recommendedAction = "Crie um projeto para começar.";
    private string _ruleMeasurementUnit = "Milímetros";
    private string _ruleDrawingScale = "1:50";
    private string _ruleFloorHeightText = "2,80";
    private string _ruleNamingStandard = "DISCIPLINA-TIPO-NÍVEL";
    private bool _ruleRequireLayerStandard = true;
    private bool _ruleRequireFileIntegrity = true;
    private bool _ruleBlockAutomationOnErrors = true;
    private string _ruleFormError = string.Empty;
    private string _settingsDefaultProjectType = "Residencial";
    private string _settingsDefaultFloorsText = "1";
    private string _settingsDefaultUnitsText = "1";
    private bool _settingsConfirmFileReferenceRemoval = true;
    private string _settingsFormError = string.Empty;
    private string _validationSummary = "Execute a verificação para avaliar o projeto ativo.";
    private string _validationEvaluatedAt = "Ainda não verificado";
    private string _validationGateStatus = "Aguardando verificação";
    private string _validationGateAccent = "#627087";
    private int _validationPassedCount;
    private int _validationWarningCount;
    private int _validationErrorCount;
    private ProjectReadinessReport? _currentReport;
    private string _reportContent = "Selecione um projeto para gerar o relatório de prontidão.";
    private string _reportGeneratedAt = "Ainda não gerado";
    private string _reportReadinessStatus = "AGUARDANDO RELATÓRIO";
    private string _reportReadinessAccent = "#627087";
    private string _historySearchText = string.Empty;
    private string _selectedHistoryFilter = "Todas as categorias";
    private string _diagnosticSummary = "Diagnóstico ainda não executado.";
    private string _diagnosticGeneratedAt = "Ainda não verificado";
    private string _notificationTitle = "Informação";
    private string _notificationMessage = string.Empty;
    private string _notificationIcon = "i";
    private string _notificationAccent = "#2C9BFF";
    private string _notificationBackground = "#102641";
    private bool _isNotificationVisible;

    public MainWindowViewModel(
        IDashboardProvider dashboardProvider,
        ProjectWorkspaceService workspaceService,
        ApplicationSettingsService settingsService,
        ProjectValidationService validationService,
        ProjectReportService reportService,
        ActivityLogService activityLogService,
        ActiveProjectContext activeProjectContext,
        IDiagnosticLogger diagnosticLogger,
        IDiagnosticService diagnosticService,
        INavigationService navigationService,
        INotificationService notificationService,
        IFilePickerService filePicker,
        IReportExportService reportExportService,
        IFileSystemLauncher fileSystemLauncher,
        IDialogService dialogService,
        IDataMaintenanceService dataMaintenanceService,
        IOperationCoordinator operationCoordinator,
        IVerifiedCopyPilotService verifiedCopyPilotService,
        IAutomationMaskCatalogService automationMaskCatalogService,
        IAutomationOrchestrator automationOrchestrator)
    {
        _dashboardProvider = dashboardProvider;
        _workspaceService = workspaceService;
        _settingsService = settingsService;
        _validationService = validationService;
        _reportService = reportService;
        _activityLogService = activityLogService;
        _activeProjectContext = activeProjectContext;
        _diagnosticLogger = diagnosticLogger;
        _diagnosticService = diagnosticService;
        _navigationService = navigationService;
        _notificationService = notificationService;
        _filePicker = filePicker;
        _reportExportService = reportExportService;
        _fileSystemLauncher = fileSystemLauncher;
        _dialogService = dialogService;
        _dataMaintenanceService = dataMaintenanceService;
        _operationCoordinator = operationCoordinator;
        _verifiedCopyPilotService = verifiedCopyPilotService;
        _automationMaskCatalogService = automationMaskCatalogService;
        _automationOrchestrator = automationOrchestrator;
        _currentSection = navigationService.CurrentSection;
        _applicationSettings = _settingsService.Load();
        LoadSettingsEditor(_applicationSettings);
        ApplyProjectFormDefaults();

        Navigation =
        [
            new(WorkspaceSection.Dashboard, "▦", "Painel principal", true, null, "Ctrl+1"),
            new(WorkspaceSection.Projects, "▤", "Projetos", false, null, "Ctrl+2"),
            new(WorkspaceSection.Files, "□", "Arquivos", false, null, "Ctrl+3"),
            new(WorkspaceSection.Masks, "◫", "Máscaras", false, "Catálogo"),
            new(WorkspaceSection.Automation, "⌘", "Automação", false, "Piloto", "Ctrl+9"),
            new(null, "✦", "Agentes IA", false, null),
            new(null, "▥", "Bibliotecas", false, null),
            new(WorkspaceSection.ProjectRules, "◇", "Regras de projeto", false, null, "Ctrl+4"),
            new(WorkspaceSection.Validators, "✓", "Validadores", false, null, "Ctrl+5"),
            new(WorkspaceSection.Reports, "▧", "Relatórios", false, null, "Ctrl+6"),
            new(WorkspaceSection.History, "↶", "Histórico", false, null, "Ctrl+7"),
            new(WorkspaceSection.Diagnostics, "⌁", "Diagnóstico", false, null, "Ctrl+8"),
            new(WorkspaceSection.Settings, "⚙", "Configurações", false, null, "Ctrl+,")
        ];

        ProjectSortOptions =
        [
            "Atualizados recentemente",
            "Nome A–Z",
            "Nome Z–A",
            "Mais antigos"
        ];

        FileFilterOptions =
        [
            "Todos",
            "Desenhos",
            "Documentos",
            "Planilhas",
            "Imagens",
            "Outros",
            "Ausentes",
            "Alterados"
        ];

        HistoryFilterOptions =
        [
            "Todas as categorias",
            "Projetos",
            "Arquivos",
            "Regras",
            "Validação",
            "Relatórios",
            "Máscaras",
            "Automação",
            "Configurações",
            "Sistema"
        ];

        Projects = [];
        AvailableProjects = [];
        ArchivedProjects = [];
        ProjectFiles = [];
        Workflow = [];
        Agents = [];
        Metrics = [];
        Activities = [];
        ValidationResults = [];
        HistoryEntries = [];
        DiagnosticChecks = [];
        DiagnosticLogs = [];
        OperationExecutions = [];

        _moduleCatalog = CreateModuleCatalog();
        _navigationService.SectionChanged += OnSectionChanged;
        _notificationService.NotificationPublished += OnNotificationPublished;

        NavigateCommand = new RelayCommand<WorkspaceSection?>(Navigate);
        OpenDashboardCommand = new RelayCommand(() => Navigate(WorkspaceSection.Dashboard));
        OpenProjectsCommand = new RelayCommand(() => Navigate(WorkspaceSection.Projects));
        OpenFilesCommand = new RelayCommand(() => Navigate(WorkspaceSection.Files));
        OpenMasksCommand = new RelayCommand(
            () => Navigate(WorkspaceSection.Masks));
        OpenAutomationCommand = new RelayCommand(
            () => Navigate(WorkspaceSection.Automation));
        OpenProjectRulesCommand = new RelayCommand(() => Navigate(WorkspaceSection.ProjectRules));
        OpenValidatorsCommand = new RelayCommand(() => Navigate(WorkspaceSection.Validators));
        OpenReportsCommand = new RelayCommand(() => Navigate(WorkspaceSection.Reports));
        OpenHistoryCommand = new RelayCommand(() => Navigate(WorkspaceSection.History));
        OpenDiagnosticsCommand = new RelayCommand(() => Navigate(WorkspaceSection.Diagnostics));
        OpenSettingsCommand = new RelayCommand(() => Navigate(WorkspaceSection.Settings));
        RefreshCurrentSectionCommand = new RelayCommand(RefreshCurrentSection);
        DismissNotificationCommand = new RelayCommand(DismissNotification);
        CreateProjectCommand = new RelayCommand(SaveProject);
        CancelProjectEditCommand = new RelayCommand(CancelProjectEdit);
        BeginEditProjectCommand = new RelayCommand<ProjectListItemViewModel>(BeginEditProject);
        DuplicateProjectCommand = new RelayCommand<ProjectListItemViewModel>(DuplicateProject);
        ArchiveProjectCommand = new RelayCommand<ProjectListItemViewModel>(ArchiveProject);
        RestoreProjectCommand = new RelayCommand<ProjectListItemViewModel>(RestoreProject);
        ImportFileCommand = new RelayCommand(ImportFile, () => SelectedProject is not null);
        OpenProjectFileCommand = new RelayCommand<ProjectFileItemViewModel>(OpenProjectFile);
        RevealProjectFileCommand = new RelayCommand<ProjectFileItemViewModel>(RevealProjectFile);
        RefreshProjectFileCommand = new RelayCommand<ProjectFileItemViewModel>(RefreshProjectFile);
        RemoveProjectFileCommand = new RelayCommand<ProjectFileItemViewModel>(RemoveProjectFile);
        ExecuteNextStepCommand = new RelayCommand(ExecuteNextStep);
        SaveProjectRulesCommand = new RelayCommand(SaveProjectRules, () => SelectedProject is not null);
        ResetProjectRulesCommand = new RelayCommand(ResetProjectRules, () => SelectedProject is not null);
        SaveSettingsCommand = new RelayCommand(SaveSettings);
        ResetSettingsCommand = new RelayCommand(ResetSettings);
        CreateDataBackupCommand = new AsyncCommand(
            CreateDataBackupAsync,
            () => !IsDataOperationRunning,
            exception => ReportDataMaintenanceFailure(
                "Não foi possível preparar o backup",
                exception),
            TimeSpan.FromMinutes(6));
        RestoreDataBackupCommand = new AsyncCommand(
            RestoreDataBackupAsync,
            () => !IsDataOperationRunning,
            exception => ReportDataMaintenanceFailure(
                "Não foi possível preparar a restauração",
                exception),
            TimeSpan.FromMinutes(6));
        ChangeDataDirectoryCommand = new AsyncCommand(
            ChangeDataDirectoryAsync,
            () => !IsDataOperationRunning,
            exception => ReportDataMaintenanceFailure(
                "Não foi possível preparar a nova pasta de dados",
                exception),
            TimeSpan.FromMinutes(6));
        CancelDataOperationCommand = new RelayCommand(
            CancelDataOperation,
            () => IsDataOperationRunning);
        RunProjectValidationCommand = new RelayCommand(
            RunProjectValidation,
            () => SelectedProject is not null);
        GenerateProjectReportCommand = new RelayCommand(
            GenerateProjectReport,
            () => SelectedProject is not null);
        ExportProjectReportCommand = new RelayCommand(
            ExportProjectReport,
            () => _currentReport is not null);
        ExportProjectReportCsvCommand = new RelayCommand(
            ExportProjectReportCsv,
            () => _currentReport is not null);
        ExportProjectReportPdfCommand = new RelayCommand(
            ExportProjectReportPdf,
            () => _currentReport is not null);
        RefreshDiagnosticsCommand = new RelayCommand(RefreshDiagnostics);
        ChooseMaskContractCommand = new RelayCommand(
            ChooseMaskContract,
            () => !IsMaskCatalogBusy);
        ChooseRuleCatalogCommand = new RelayCommand(
            ChooseRuleCatalog,
            () => !IsMaskCatalogBusy);
        AnalyzeMaskPackageCommand = new AsyncCommand(
            AnalyzeMaskPackageAsync,
            CanAnalyzeMaskPackage,
            ReportMaskCatalogFailure,
            TimeSpan.FromMinutes(1));
        ImportMaskPackageCommand = new RelayCommand(
            ImportMaskPackage,
            CanImportMaskPackage);
        ClearMaskPackageCommand = new RelayCommand(
            ClearMaskPackage,
            () => !IsMaskCatalogBusy);
        ToggleMaskCatalogEntryCommand =
            new RelayCommand<AutomationMaskCatalogItemViewModel>(
                ToggleMaskCatalogEntry,
                item => item is not null && !IsMaskCatalogBusy);
        AssessMaskIntegrationCommand =
            new RelayCommand<AutomationMaskCatalogItemViewModel>(
                AssessMaskIntegration,
                item => item is not null && !IsMaskCatalogBusy);
        ChooseAutomationOutputCommand = new RelayCommand(
            ChooseAutomationOutputDirectory,
            () => !IsAutomationPilotRunning);
        SimulateAutomationPilotCommand = new AsyncCommand(
            SimulateAutomationPilotAsync,
            CanSimulateAutomationPilot,
            ReportAutomationPilotFailure,
            TimeSpan.FromMinutes(6));
        ExecuteAutomationPilotCommand = new AsyncCommand(
            ExecuteAutomationPilotAsync,
            CanExecuteAutomationPilot,
            ReportAutomationPilotFailure,
            TimeSpan.FromMinutes(6));
        CancelAutomationPilotCommand = new RelayCommand(
            CancelAutomationPilot,
            () => IsAutomationPilotRunning);
        ResetAutomationPilotCommand = new RelayCommand(
            ResetAutomationPilot,
            () => !IsAutomationPilotRunning);
        OpenAutomationOutputCommand = new RelayCommand(
            OpenAutomationOutput,
            () => !string.IsNullOrWhiteSpace(AutomationPublishedPath));

        RefreshProjects(_activeProjectContext.ProjectId);
        RefreshOperationExecutions();
        TryRecordActivity(
            "Sistema",
            "Aplicativo iniciado",
            "AutoAIBuilder iniciado e dados locais carregados.",
            ActivityLevel.Information,
            SelectedProject);
        StatusMessage = SelectedProject is null
            ? "Dados locais carregados. Crie um projeto para começar."
            : "Projeto ativo restaurado. O painel apresenta dados locais reais.";
        TryWriteDiagnostic(
            DiagnosticLevel.Information,
            "Application",
            "AutoAIBuilder iniciado e composição carregada.",
            properties: SelectedProject is null
                ? null
                : new Dictionary<string, string>
                {
                    ["projectId"] = SelectedProject.Id.ToString(),
                    ["projectName"] = SelectedProject.Name
                });
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<NavigationItemViewModel> Navigation { get; }
    public ObservableCollection<WorkflowStepViewModel> Workflow { get; }
    public ObservableCollection<AgentViewModel> Agents { get; }
    public ObservableCollection<MetricViewModel> Metrics { get; }
    public ObservableCollection<ActivityViewModel> Activities { get; }
    public ObservableCollection<ProjectValidationItemViewModel> ValidationResults { get; }
    public ObservableCollection<ActivityHistoryItemViewModel> HistoryEntries { get; }
    public ObservableCollection<DiagnosticCheckItemViewModel> DiagnosticChecks { get; }
    public ObservableCollection<DiagnosticLogItemViewModel> DiagnosticLogs { get; }
    public ObservableCollection<OperationExecutionItemViewModel> OperationExecutions { get; }
    public ObservableCollection<ProjectListItemViewModel> Projects { get; }
    public ObservableCollection<ProjectListItemViewModel> AvailableProjects { get; }
    public ObservableCollection<ProjectListItemViewModel> ArchivedProjects { get; }
    public ObservableCollection<ProjectFileItemViewModel> ProjectFiles { get; }
    public IReadOnlyList<string> ProjectSortOptions { get; }
    public IReadOnlyList<string> FileFilterOptions { get; }
    public IReadOnlyList<string> HistoryFilterOptions { get; }
    public ICommand NavigateCommand { get; }
    public ICommand OpenDashboardCommand { get; }
    public ICommand OpenProjectsCommand { get; }
    public ICommand OpenFilesCommand { get; }
    public ICommand OpenMasksCommand { get; }
    public ICommand OpenAutomationCommand { get; }
    public ICommand OpenProjectRulesCommand { get; }
    public ICommand OpenValidatorsCommand { get; }
    public ICommand OpenReportsCommand { get; }
    public ICommand OpenHistoryCommand { get; }
    public ICommand OpenDiagnosticsCommand { get; }
    public ICommand OpenSettingsCommand { get; }
    public ICommand RefreshCurrentSectionCommand { get; }
    public ICommand DismissNotificationCommand { get; }
    public ICommand CreateProjectCommand { get; }
    public ICommand CancelProjectEditCommand { get; }
    public ICommand BeginEditProjectCommand { get; }
    public ICommand DuplicateProjectCommand { get; }
    public ICommand ArchiveProjectCommand { get; }
    public ICommand RestoreProjectCommand { get; }
    public ICommand ImportFileCommand { get; }
    public ICommand OpenProjectFileCommand { get; }
    public ICommand RevealProjectFileCommand { get; }
    public ICommand RefreshProjectFileCommand { get; }
    public ICommand RemoveProjectFileCommand { get; }
    public ICommand ExecuteNextStepCommand { get; }
    public ICommand SaveProjectRulesCommand { get; }
    public ICommand ResetProjectRulesCommand { get; }
    public ICommand SaveSettingsCommand { get; }
    public ICommand ResetSettingsCommand { get; }
    public ICommand CreateDataBackupCommand { get; }
    public ICommand RestoreDataBackupCommand { get; }
    public ICommand ChangeDataDirectoryCommand { get; }
    public ICommand CancelDataOperationCommand { get; }
    public ICommand RunProjectValidationCommand { get; }
    public ICommand GenerateProjectReportCommand { get; }
    public ICommand ExportProjectReportCommand { get; }
    public ICommand ExportProjectReportCsvCommand { get; }
    public ICommand ExportProjectReportPdfCommand { get; }
    public ICommand RefreshDiagnosticsCommand { get; }

    public string ProjectName
    {
        get => _projectName;
        private set => SetField(ref _projectName, value);
    }

    public string ProjectType
    {
        get => _projectType;
        private set => SetField(ref _projectType, value);
    }

    public string ProjectDetails
    {
        get => _projectDetails;
        private set => SetField(ref _projectDetails, value);
    }

    public string Disciplines
    {
        get => _disciplines;
        private set => SetField(ref _disciplines, value);
    }

    public string UpdatedAt
    {
        get => _updatedAt;
        private set => SetField(ref _updatedAt, value);
    }

    public string RecommendedAction
    {
        get => _recommendedAction;
        private set => SetField(ref _recommendedAction, value);
    }

    public Visibility DashboardVisibility =>
        CurrentSection == WorkspaceSection.Dashboard ? Visibility.Visible : Visibility.Collapsed;

    public Visibility ProjectsVisibility =>
        CurrentSection == WorkspaceSection.Projects ? Visibility.Visible : Visibility.Collapsed;

    public Visibility FilesVisibility =>
        CurrentSection == WorkspaceSection.Files ? Visibility.Visible : Visibility.Collapsed;

    public Visibility MasksVisibility =>
        CurrentSection == WorkspaceSection.Masks
            ? Visibility.Visible
            : Visibility.Collapsed;

    public Visibility ProjectRulesVisibility =>
        CurrentSection == WorkspaceSection.ProjectRules ? Visibility.Visible : Visibility.Collapsed;

    public Visibility SettingsVisibility =>
        CurrentSection == WorkspaceSection.Settings ? Visibility.Visible : Visibility.Collapsed;

    public Visibility ValidatorsVisibility =>
        CurrentSection == WorkspaceSection.Validators ? Visibility.Visible : Visibility.Collapsed;

    public Visibility ReportsVisibility =>
        CurrentSection == WorkspaceSection.Reports ? Visibility.Visible : Visibility.Collapsed;

    public Visibility HistoryVisibility =>
        CurrentSection == WorkspaceSection.History ? Visibility.Visible : Visibility.Collapsed;

    public Visibility DiagnosticsVisibility =>
        CurrentSection == WorkspaceSection.Diagnostics ? Visibility.Visible : Visibility.Collapsed;

    public Visibility WorkspaceStatusVisibility =>
        CurrentSection == WorkspaceSection.Dashboard ? Visibility.Collapsed : Visibility.Visible;

    public Visibility ProjectFormErrorVisibility =>
        string.IsNullOrWhiteSpace(ProjectFormError) ? Visibility.Collapsed : Visibility.Visible;

    public Visibility CancelProjectEditVisibility =>
        _editingProjectId is null ? Visibility.Collapsed : Visibility.Visible;

    public string ProjectFormTitle =>
        _editingProjectId is null ? "Novo projeto" : "Editar projeto";

    public string ProjectSubmitText =>
        _editingProjectId is null ? "Criar projeto" : "Salvar alterações";

    public int ArchivedProjectCount => ArchivedProjects.Count;

    public WorkspaceSection CurrentSection
    {
        get => _currentSection;
        private set
        {
            if (_currentSection == value)
            {
                return;
            }

            _currentSection = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(DashboardVisibility));
            OnPropertyChanged(nameof(ProjectsVisibility));
            OnPropertyChanged(nameof(FilesVisibility));
            OnPropertyChanged(nameof(MasksVisibility));
            OnPropertyChanged(nameof(AutomationVisibility));
            OnPropertyChanged(nameof(ProjectRulesVisibility));
            OnPropertyChanged(nameof(SettingsVisibility));
            OnPropertyChanged(nameof(ValidatorsVisibility));
            OnPropertyChanged(nameof(ReportsVisibility));
            OnPropertyChanged(nameof(HistoryVisibility));
            OnPropertyChanged(nameof(DiagnosticsVisibility));
            OnPropertyChanged(nameof(WorkspaceStatusVisibility));
        }
    }

    public ProjectListItemViewModel? SelectedProject
    {
        get => _selectedProject;
        set
        {
            if (ReferenceEquals(_selectedProject, value))
            {
                return;
            }

            _selectedProject = value;

            if (value is null)
            {
                _activeProjectContext.Clear();
            }
            else
            {
                _activeProjectContext.Select(value.Id);
            }

            OnPropertyChanged();
            OnPropertyChanged(nameof(SelectedProjectTitle));
            OnPropertyChanged(nameof(HasSelectedProject));
            RefreshProjectFiles();
            RefreshAutomationPilotFiles();
            LoadProjectRules();
            RefreshActiveProject();
            SetManagedProject(
                value is null
                    ? null
                    : Projects.FirstOrDefault(project => project.Id == value.Id),
                announce: false);
            (ImportFileCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (SaveProjectRulesCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (ResetProjectRulesCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (RunProjectValidationCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (GenerateProjectReportCommand as RelayCommand)?.RaiseCanExecuteChanged();

            if (CurrentSection == WorkspaceSection.Validators)
            {
                ResetValidationState();
            }

            ResetReportState();
        }
    }

    public ProjectListItemViewModel? SelectedManagedProject
    {
        get => _selectedManagedProject;
        set => SetManagedProject(value, announce: true);
    }

    public string SelectedProjectTitle =>
        SelectedProject?.Name ?? "Nenhum projeto selecionado";

    public bool HasSelectedProject => SelectedProject is not null;

    public string FileSummary =>
        SelectedProject is null
            ? "Selecione um projeto para consultar os arquivos."
            : $"{_allProjectFiles.Count} arquivo(s) catalogado(s), {MissingFileCount} ausente(s) e {ChangedFileCount} alterado(s).";

    public int MissingFileCount => _allProjectFiles.Count(file => !file.Exists);

    public int ChangedFileCount => _allProjectFiles.Count(file => file.HasChanged);

    public string FileSearchText
    {
        get => _fileSearchText;
        set
        {
            if (SetField(ref _fileSearchText, value))
            {
                RefreshFileFilters();
            }
        }
    }

    public string SelectedFileFilter
    {
        get => _selectedFileFilter;
        set
        {
            if (SetField(ref _selectedFileFilter, value))
            {
                RefreshFileFilters();
            }
        }
    }

    public string ProjectSearchText
    {
        get => _projectSearchText;
        set
        {
            if (SetField(ref _projectSearchText, value))
            {
                RefreshProjectFilters();
            }
        }
    }

    public string SelectedProjectSort
    {
        get => _selectedProjectSort;
        set
        {
            if (SetField(ref _selectedProjectSort, value))
            {
                RefreshProjectFilters();
            }
        }
    }

    public string NewProjectName
    {
        get => _newProjectName;
        set
        {
            if (SetField(ref _newProjectName, value))
            {
                ClearProjectFormError();
            }
        }
    }

    public string NewProjectType
    {
        get => _newProjectType;
        set
        {
            if (SetField(ref _newProjectType, value))
            {
                ClearProjectFormError();
            }
        }
    }

    public string NewProjectFloorsText
    {
        get => _newProjectFloorsText;
        set
        {
            if (SetField(ref _newProjectFloorsText, value))
            {
                ClearProjectFormError();
            }
        }
    }

    public string NewProjectUnitsText
    {
        get => _newProjectUnitsText;
        set
        {
            if (SetField(ref _newProjectUnitsText, value))
            {
                ClearProjectFormError();
            }
        }
    }

    public string ProjectFormError
    {
        get => _projectFormError;
        private set
        {
            if (SetField(ref _projectFormError, value))
            {
                OnPropertyChanged(nameof(ProjectFormErrorVisibility));
            }
        }
    }

    public IReadOnlyList<string> MeasurementUnitOptions { get; } =
        ["Milímetros", "Centímetros", "Metros"];

    public string RuleMeasurementUnit
    {
        get => _ruleMeasurementUnit;
        set
        {
            if (SetField(ref _ruleMeasurementUnit, value))
            {
                ClearRuleFormError();
            }
        }
    }

    public string RuleDrawingScale
    {
        get => _ruleDrawingScale;
        set
        {
            if (SetField(ref _ruleDrawingScale, value))
            {
                ClearRuleFormError();
            }
        }
    }

    public string RuleFloorHeightText
    {
        get => _ruleFloorHeightText;
        set
        {
            if (SetField(ref _ruleFloorHeightText, value))
            {
                ClearRuleFormError();
            }
        }
    }

    public string RuleNamingStandard
    {
        get => _ruleNamingStandard;
        set
        {
            if (SetField(ref _ruleNamingStandard, value))
            {
                ClearRuleFormError();
            }
        }
    }

    public bool RuleRequireLayerStandard
    {
        get => _ruleRequireLayerStandard;
        set => SetField(ref _ruleRequireLayerStandard, value);
    }

    public bool RuleRequireFileIntegrity
    {
        get => _ruleRequireFileIntegrity;
        set => SetField(ref _ruleRequireFileIntegrity, value);
    }

    public bool RuleBlockAutomationOnErrors
    {
        get => _ruleBlockAutomationOnErrors;
        set => SetField(ref _ruleBlockAutomationOnErrors, value);
    }

    public string RuleFormError
    {
        get => _ruleFormError;
        private set
        {
            if (SetField(ref _ruleFormError, value))
            {
                OnPropertyChanged(nameof(RuleFormErrorVisibility));
            }
        }
    }

    public Visibility RuleFormErrorVisibility =>
        string.IsNullOrWhiteSpace(RuleFormError) ? Visibility.Collapsed : Visibility.Visible;

    public string SettingsDefaultProjectType
    {
        get => _settingsDefaultProjectType;
        set
        {
            if (SetField(ref _settingsDefaultProjectType, value))
            {
                ClearSettingsFormError();
            }
        }
    }

    public string SettingsDefaultFloorsText
    {
        get => _settingsDefaultFloorsText;
        set
        {
            if (SetField(ref _settingsDefaultFloorsText, value))
            {
                ClearSettingsFormError();
            }
        }
    }

    public string SettingsDefaultUnitsText
    {
        get => _settingsDefaultUnitsText;
        set
        {
            if (SetField(ref _settingsDefaultUnitsText, value))
            {
                ClearSettingsFormError();
            }
        }
    }

    public bool SettingsConfirmFileReferenceRemoval
    {
        get => _settingsConfirmFileReferenceRemoval;
        set => SetField(ref _settingsConfirmFileReferenceRemoval, value);
    }

    public string SettingsFormError
    {
        get => _settingsFormError;
        private set
        {
            if (SetField(ref _settingsFormError, value))
            {
                OnPropertyChanged(nameof(SettingsFormErrorVisibility));
            }
        }
    }

    public Visibility SettingsFormErrorVisibility =>
        string.IsNullOrWhiteSpace(SettingsFormError) ? Visibility.Collapsed : Visibility.Visible;

    public string ValidationSummary
    {
        get => _validationSummary;
        private set => SetField(ref _validationSummary, value);
    }

    public string ValidationEvaluatedAt
    {
        get => _validationEvaluatedAt;
        private set => SetField(ref _validationEvaluatedAt, value);
    }

    public string ValidationGateStatus
    {
        get => _validationGateStatus;
        private set => SetField(ref _validationGateStatus, value);
    }

    public string ValidationGateAccent
    {
        get => _validationGateAccent;
        private set => SetField(ref _validationGateAccent, value);
    }

    public int ValidationPassedCount
    {
        get => _validationPassedCount;
        private set => SetField(ref _validationPassedCount, value);
    }

    public int ValidationWarningCount
    {
        get => _validationWarningCount;
        private set => SetField(ref _validationWarningCount, value);
    }

    public int ValidationErrorCount
    {
        get => _validationErrorCount;
        private set => SetField(ref _validationErrorCount, value);
    }

    public Visibility EmptyValidationVisibility =>
        ValidationResults.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

    public string ReportContent
    {
        get => _reportContent;
        private set => SetField(ref _reportContent, value);
    }

    public string ReportGeneratedAt
    {
        get => _reportGeneratedAt;
        private set => SetField(ref _reportGeneratedAt, value);
    }

    public string ReportReadinessStatus
    {
        get => _reportReadinessStatus;
        private set => SetField(ref _reportReadinessStatus, value);
    }

    public string ReportReadinessAccent
    {
        get => _reportReadinessAccent;
        private set => SetField(ref _reportReadinessAccent, value);
    }

    public string HistorySearchText
    {
        get => _historySearchText;
        set
        {
            if (SetField(ref _historySearchText, value))
            {
                RefreshHistoryFilters();
            }
        }
    }

    public string SelectedHistoryFilter
    {
        get => _selectedHistoryFilter;
        set
        {
            if (SetField(ref _selectedHistoryFilter, value))
            {
                RefreshHistoryFilters();
            }
        }
    }

    public string HistorySummary =>
        $"{HistoryEntries.Count} de {_allHistoryEntries.Count} evento(s) exibido(s). O histórico mantém no máximo 500 registros.";

    public Visibility EmptyHistoryVisibility =>
        HistoryEntries.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

    public string DiagnosticSummary
    {
        get => _diagnosticSummary;
        private set => SetField(ref _diagnosticSummary, value);
    }

    public string DiagnosticGeneratedAt
    {
        get => _diagnosticGeneratedAt;
        private set => SetField(ref _diagnosticGeneratedAt, value);
    }

    public Visibility EmptyDiagnosticLogVisibility =>
        DiagnosticLogs.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

    public string NotificationTitle
    {
        get => _notificationTitle;
        private set => SetField(ref _notificationTitle, value);
    }

    public string NotificationMessage
    {
        get => _notificationMessage;
        private set => SetField(ref _notificationMessage, value);
    }

    public string NotificationIcon
    {
        get => _notificationIcon;
        private set => SetField(ref _notificationIcon, value);
    }

    public string NotificationAccent
    {
        get => _notificationAccent;
        private set => SetField(ref _notificationAccent, value);
    }

    public string NotificationBackground
    {
        get => _notificationBackground;
        private set => SetField(ref _notificationBackground, value);
    }

    public Visibility NotificationVisibility =>
        _isNotificationVisible ? Visibility.Visible : Visibility.Collapsed;

    public string StatusMessage
    {
        get => _statusMessage;
        private set
        {
            if (SetField(ref _statusMessage, value))
            {
                _notificationService.Publish(value);
            }
        }
    }

    private void SaveProject()
    {
        ProjectFormError = string.Empty;

        if (!TryReadProjectForm(out var floors, out var units))
        {
            return;
        }

        try
        {
            if (_editingProjectId is Guid projectId)
            {
                var updated = _workspaceService.UpdateProject(new UpdateProjectRequest(
                    projectId,
                    NewProjectName,
                    NewProjectType,
                    floors,
                    units));

                ResetProjectForm();
                RefreshProjects(updated.Id);
                TryRecordActivity(
                    "Projetos",
                    "Projeto atualizado",
                    $"Dados cadastrais do projeto “{updated.Name}” atualizados.",
                    ActivityLevel.Success,
                    ProjectListItemViewModel.From(updated));
                StatusMessage = $"Projeto “{updated.Name}” atualizado.";
                return;
            }

            var project = _workspaceService.CreateProject(new CreateProjectRequest(
                NewProjectName,
                NewProjectType,
                floors,
                units));

            ResetProjectForm();
            RefreshProjects(project.Id);
            TryRecordActivity(
                "Projetos",
                "Projeto criado",
                $"Projeto “{project.Name}” criado e definido como ativo.",
                ActivityLevel.Success,
                ProjectListItemViewModel.From(project));
            StatusMessage = $"Projeto “{project.Name}” criado e definido como projeto ativo.";
        }
        catch (Exception exception) when (
            exception is ArgumentException or InvalidOperationException)
        {
            SetProjectFormError(GetFriendlyMessage(exception));
        }
    }

    private bool TryReadProjectForm(out int floors, out int units)
    {
        floors = 0;
        units = 0;

        if (string.IsNullOrWhiteSpace(NewProjectName))
        {
            SetProjectFormError("Informe o nome do projeto.");
            return false;
        }

        if (string.IsNullOrWhiteSpace(NewProjectType))
        {
            SetProjectFormError("Informe o tipo do projeto.");
            return false;
        }

        if (!int.TryParse(NewProjectFloorsText, out floors) || floors < 1)
        {
            SetProjectFormError("Informe uma quantidade válida de pavimentos, maior ou igual a 1.");
            return false;
        }

        if (!int.TryParse(NewProjectUnitsText, out units) || units < 1)
        {
            SetProjectFormError("Informe uma quantidade válida de unidades, maior ou igual a 1.");
            return false;
        }

        return true;
    }

    private void BeginEditProject(ProjectListItemViewModel? item)
    {
        if (item is null)
        {
            return;
        }

        var project = _workspaceService.GetProject(item.Id);
        if (project is null || project.IsArchived)
        {
            StatusMessage = "O projeto precisa estar ativo para ser editado.";
            return;
        }

        _editingProjectId = project.Id;
        NewProjectName = project.Name;
        NewProjectType = project.Type;
        NewProjectFloorsText = project.Floors.ToString();
        NewProjectUnitsText = project.Units.ToString();
        ProjectFormError = string.Empty;
        OnProjectFormModeChanged();

        SelectedProject = AvailableProjects.FirstOrDefault(value => value.Id == project.Id);
        StatusMessage = $"Editando o projeto “{project.Name}”.";
    }

    private void CancelProjectEdit()
    {
        ResetProjectForm();
        StatusMessage = "Edição cancelada; nenhuma alteração foi salva.";
    }

    private void DuplicateProject(ProjectListItemViewModel? item)
    {
        if (item is null)
        {
            return;
        }

        try
        {
            var duplicate = _workspaceService.DuplicateProject(item.Id);
            ResetProjectForm();
            RefreshProjects(duplicate.Id);
            TryRecordActivity(
                "Projetos",
                "Projeto duplicado",
                $"Projeto duplicado como “{duplicate.Name}” sem copiar arquivos originais.",
                ActivityLevel.Success,
                ProjectListItemViewModel.From(duplicate));
            StatusMessage = $"Projeto duplicado como “{duplicate.Name}”; os arquivos originais não foram copiados.";
        }
        catch (InvalidOperationException exception)
        {
            StatusMessage = exception.Message;
        }
    }

    private void ArchiveProject(ProjectListItemViewModel? item)
    {
        if (item is null)
        {
            return;
        }

        try
        {
            var archived = _workspaceService.ArchiveProject(item.Id);

            if (_editingProjectId == archived.Id)
            {
                ResetProjectForm();
            }

            if (_activeProjectContext.ProjectId == archived.Id)
            {
                _activeProjectContext.Clear();
            }

            RefreshProjects();
            TryRecordActivity(
                "Projetos",
                "Projeto arquivado",
                $"Projeto “{archived.Name}” arquivado de forma reversível.",
                ActivityLevel.Warning,
                ProjectListItemViewModel.From(archived));
            StatusMessage = $"Projeto “{archived.Name}” arquivado. Ele pode ser restaurado a qualquer momento.";
        }
        catch (InvalidOperationException exception)
        {
            StatusMessage = exception.Message;
        }
    }

    private void RestoreProject(ProjectListItemViewModel? item)
    {
        if (item is null)
        {
            return;
        }

        try
        {
            var restored = _workspaceService.RestoreProject(item.Id);
            RefreshProjects(restored.Id);
            TryRecordActivity(
                "Projetos",
                "Projeto restaurado",
                $"Projeto “{restored.Name}” restaurado e definido como ativo.",
                ActivityLevel.Success,
                ProjectListItemViewModel.From(restored));
            StatusMessage = $"Projeto “{restored.Name}” restaurado e definido como projeto ativo.";
        }
        catch (InvalidOperationException exception)
        {
            StatusMessage = exception.Message;
        }
    }

    private void ImportFile()
    {
        if (SelectedProject is null)
        {
            StatusMessage = "Selecione um projeto antes de catalogar um arquivo.";
            return;
        }

        var selectedPaths = _filePicker.PickProjectFiles();
        if (selectedPaths.Count == 0)
        {
            StatusMessage = "Catalogação cancelada; nenhum arquivo foi alterado.";
            return;
        }

        try
        {
            var previousCount = _allProjectFiles.Count;
            var updatedProject = _workspaceService.RegisterFiles(
                SelectedProject.Id,
                selectedPaths);

            RefreshProjects(updatedProject.Id);
            Navigate(WorkspaceSection.Files);
            var addedCount = Math.Max(0, _allProjectFiles.Count - previousCount);
            StatusMessage = addedCount == 0
                ? "Os arquivos selecionados já estavam catalogados; nenhuma duplicidade foi criada."
                : $"{addedCount} arquivo(s) catalogado(s) sem copiar ou alterar os originais.";
            TryRecordActivity(
                "Arquivos",
                addedCount == 0 ? "Catalogação sem alterações" : "Arquivos catalogados",
                addedCount == 0
                    ? "Os arquivos selecionados já estavam presentes no catálogo."
                    : $"{addedCount} referência(s) adicionada(s) sem copiar os arquivos originais.",
                addedCount == 0 ? ActivityLevel.Information : ActivityLevel.Success,
                SelectedProject);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            StatusMessage = exception.Message;
        }
    }

    private void OpenProjectFile(ProjectFileItemViewModel? item)
    {
        if (!CanAccessFile(item))
        {
            return;
        }

        try
        {
            _fileSystemLauncher.OpenFile(item!.SourcePath);
            StatusMessage = $"Arquivo “{item.Name}” aberto no aplicativo associado.";
        }
        catch (Exception exception) when (
            exception is IOException
                or UnauthorizedAccessException
                or InvalidOperationException
                or System.ComponentModel.Win32Exception)
        {
            StatusMessage = $"Não foi possível abrir o arquivo: {exception.Message}";
        }
    }

    private void RevealProjectFile(ProjectFileItemViewModel? item)
    {
        if (!CanAccessFile(item))
        {
            return;
        }

        try
        {
            _fileSystemLauncher.RevealFile(item!.SourcePath);
            StatusMessage = $"Arquivo “{item.Name}” localizado no Explorer.";
        }
        catch (Exception exception) when (
            exception is IOException
                or UnauthorizedAccessException
                or InvalidOperationException
                or System.ComponentModel.Win32Exception)
        {
            StatusMessage = $"Não foi possível localizar o arquivo: {exception.Message}";
        }
    }

    private void RefreshProjectFile(ProjectFileItemViewModel? item)
    {
        if (SelectedProject is null || item is null)
        {
            return;
        }

        try
        {
            var updated = _workspaceService.RefreshFileMetadata(
                SelectedProject.Id,
                item.Id);

            RefreshProjects(updated.Id);
            TryRecordActivity(
                "Arquivos",
                "Metadados atualizados",
                $"Metadados do arquivo “{item.Name}” atualizados.",
                ActivityLevel.Success,
                SelectedProject);
            StatusMessage = $"Metadados de “{item.Name}” atualizados.";
        }
        catch (Exception exception) when (
            exception is IOException
                or UnauthorizedAccessException
                or InvalidOperationException)
        {
            StatusMessage = exception.Message;
        }
    }

    private void RemoveProjectFile(ProjectFileItemViewModel? item)
    {
        if (SelectedProject is null || item is null)
        {
            return;
        }

        if (_applicationSettings.ConfirmFileReferenceRemoval
            && !_dialogService.ConfirmRemoveFileReference(item.Name))
        {
            StatusMessage = "Remoção cancelada; o catálogo não foi alterado.";
            return;
        }

        try
        {
            var updated = _workspaceService.RemoveFileReference(
                SelectedProject.Id,
                item.Id);

            RefreshProjects(updated.Id);
            TryRecordActivity(
                "Arquivos",
                "Referência removida",
                $"Referência de “{item.Name}” removida; o arquivo original foi preservado.",
                ActivityLevel.Warning,
                SelectedProject);
            StatusMessage = $"Referência de “{item.Name}” removida. O arquivo original permanece intacto.";
        }
        catch (InvalidOperationException exception)
        {
            StatusMessage = exception.Message;
        }
    }

    private bool CanAccessFile(ProjectFileItemViewModel? item)
    {
        if (item is null)
        {
            return false;
        }

        if (item.Exists)
        {
            return true;
        }

        StatusMessage = $"O arquivo “{item.Name}” não está disponível no caminho catalogado.";
        return false;
    }

    private void RefreshProjects(Guid? selectedId = null)
    {
        var currentId = selectedId ?? _activeProjectContext.ProjectId ?? SelectedProject?.Id;
        var projectItems = _workspaceService.GetProjects()
            .Select(ProjectListItemViewModel.From)
            .ToArray();

        ReplaceItems(
            AvailableProjects,
            projectItems
                .Where(project => !project.IsArchived)
                .OrderByDescending(project => project.UpdatedAtValue));

        ReplaceItems(
            ArchivedProjects,
            projectItems
                .Where(project => project.IsArchived)
                .OrderByDescending(project => project.ArchivedAtValue));

        RefreshProjectFilters();

        SelectedProject = AvailableProjects.FirstOrDefault(project => project.Id == currentId)
            ?? AvailableProjects.FirstOrDefault();

        OnPropertyChanged(nameof(ArchivedProjectCount));
        OnPropertyChanged(nameof(FileSummary));
    }

    private void RefreshProjectFilters()
    {
        IEnumerable<ProjectListItemViewModel> query = AvailableProjects;

        if (!string.IsNullOrWhiteSpace(ProjectSearchText))
        {
            var search = ProjectSearchText.Trim();
            query = query.Where(project =>
                project.Name.Contains(search, StringComparison.OrdinalIgnoreCase)
                || project.Type.Contains(search, StringComparison.OrdinalIgnoreCase)
                || project.Disciplines.Contains(search, StringComparison.OrdinalIgnoreCase));
        }

        query = SelectedProjectSort switch
        {
            "Nome A–Z" => query.OrderBy(project => project.Name, StringComparer.CurrentCultureIgnoreCase),
            "Nome Z–A" => query.OrderByDescending(project => project.Name, StringComparer.CurrentCultureIgnoreCase),
            "Mais antigos" => query.OrderBy(project => project.UpdatedAtValue),
            _ => query.OrderByDescending(project => project.UpdatedAtValue)
        };

        ReplaceItems(Projects, query);
        SetManagedProject(
            SelectedProject is null
                ? null
                : Projects.FirstOrDefault(project => project.Id == SelectedProject.Id),
            announce: false);
    }

    private void SetManagedProject(
        ProjectListItemViewModel? value,
        bool announce)
    {
        if (ReferenceEquals(_selectedManagedProject, value))
        {
            return;
        }

        _selectedManagedProject = value;
        OnPropertyChanged(nameof(SelectedManagedProject));

        if (value is null)
        {
            return;
        }

        var activeItem = AvailableProjects.FirstOrDefault(project => project.Id == value.Id);
        if (activeItem is not null && !ReferenceEquals(SelectedProject, activeItem))
        {
            SelectedProject = activeItem;
        }

        if (announce)
        {
            StatusMessage = $"Projeto “{value.Name}” definido como projeto ativo.";
        }
    }

    private void RefreshProjectFiles()
    {
        _allProjectFiles.Clear();
        ProjectFiles.Clear();

        if (SelectedProject is null)
        {
            OnPropertyChanged(nameof(MissingFileCount));
            OnPropertyChanged(nameof(ChangedFileCount));
            OnPropertyChanged(nameof(FileSummary));
            return;
        }

        try
        {
            _allProjectFiles.AddRange(
                _workspaceService.InspectFiles(SelectedProject.Id)
                    .OrderByDescending(item => item.File.ImportedAt)
                    .Select(ProjectFileItemViewModel.From));
        }
        catch (InvalidOperationException exception)
        {
            StatusMessage = exception.Message;
        }

        RefreshFileFilters();
        OnPropertyChanged(nameof(MissingFileCount));
        OnPropertyChanged(nameof(ChangedFileCount));
        OnPropertyChanged(nameof(FileSummary));
    }

    private void RefreshFileFilters()
    {
        IEnumerable<ProjectFileItemViewModel> query = _allProjectFiles;

        if (!string.IsNullOrWhiteSpace(FileSearchText))
        {
            var search = FileSearchText.Trim();
            query = query.Where(file =>
                file.Name.Contains(search, StringComparison.OrdinalIgnoreCase)
                || file.SourcePath.Contains(search, StringComparison.OrdinalIgnoreCase)
                || file.Extension.Contains(search, StringComparison.OrdinalIgnoreCase)
                || file.Kind.Contains(search, StringComparison.OrdinalIgnoreCase));
        }

        query = SelectedFileFilter switch
        {
            "Desenhos" => query.Where(file => file.KindKey == ProjectFileKind.Drawing),
            "Documentos" => query.Where(file => file.KindKey == ProjectFileKind.Document),
            "Planilhas" => query.Where(file => file.KindKey == ProjectFileKind.Spreadsheet),
            "Imagens" => query.Where(file => file.KindKey == ProjectFileKind.Image),
            "Outros" => query.Where(file => file.KindKey == ProjectFileKind.Other),
            "Ausentes" => query.Where(file => !file.Exists),
            "Alterados" => query.Where(file => file.HasChanged),
            _ => query
        };

        ReplaceItems(ProjectFiles, query);
    }

    private void RefreshActiveProject()
    {
        var project = _activeProjectContext.ProjectId is Guid projectId
            ? _workspaceService.GetProject(projectId)
            : null;

        if (project is null || project.IsArchived)
        {
            ProjectName = "Nenhum projeto";
            ProjectType = "Selecione ou crie um projeto";
            ProjectDetails = "Sem dados";
            Disciplines = "Sem disciplinas";
            UpdatedAt = "Ainda não atualizado";
            RecommendedAction = "Crie ou restaure um projeto para começar.";
            Workflow.Clear();
            Agents.Clear();
            Metrics.Clear();
            Activities.Clear();
            return;
        }

        var snapshot = _dashboardProvider.GetFor(project);

        ProjectName = snapshot.Project.Name;
        ProjectType = snapshot.Project.Type;
        ProjectDetails = $"{snapshot.Project.Floors} pavimentos • {snapshot.Project.Units} unidades";
        Disciplines = string.Join("  •  ", snapshot.Project.Disciplines);
        UpdatedAt = $"Atualizado em {snapshot.Project.UpdatedAt:dd/MM/yyyy HH:mm}";
        RecommendedAction = snapshot.RecommendedAction;

        ReplaceItems(
            Workflow,
            snapshot.Workflow.Select(step => new WorkflowStepViewModel(
                step.Order,
                step.Name,
                GetStateLabel(step.State),
                GetStateAccent(step.State),
                step.State == WorkflowState.Running)));

        ReplaceItems(
            Agents,
            snapshot.Agents.Select(agent => new AgentViewModel(
                agent.Name,
                agent.Role,
                GetStateLabel(agent.State),
                GetStateAccent(agent.State),
                agent.Progress ?? 0)));

        ReplaceItems(
            Metrics,
            snapshot.Metrics.Select(metric => new MetricViewModel(
                metric.Label,
                metric.Value,
                metric.Progress,
                metric.Accent)));

        ReplaceItems(
            Activities,
            snapshot.Activities
                .OrderByDescending(activity => activity.OccurredAt)
                .Select(activity => new ActivityViewModel(
                    activity.OccurredAt.ToString("HH:mm"),
                    activity.Description,
                    activity.Accent)));
    }

    private void ExecuteNextStep()
    {
        if (SelectedProject is null)
        {
            Navigate(WorkspaceSection.Projects);
            StatusMessage = "Crie, restaure ou selecione um projeto antes de continuar.";
            return;
        }

        if (_allProjectFiles.Count == 0)
        {
            Navigate(WorkspaceSection.Files);
            StatusMessage = "Catálogo vazio: selecione o primeiro arquivo do projeto.";
            return;
        }

        Navigate(WorkspaceSection.ProjectRules);
        StatusMessage = "Revise e salve as regras do projeto antes de configurar máscaras ou automações.";
    }

    private void ResetProjectForm()
    {
        _editingProjectId = null;
        NewProjectName = string.Empty;
        ApplyProjectFormDefaults();
        ProjectFormError = string.Empty;
        OnProjectFormModeChanged();
    }

    private void OnProjectFormModeChanged()
    {
        OnPropertyChanged(nameof(ProjectFormTitle));
        OnPropertyChanged(nameof(ProjectSubmitText));
        OnPropertyChanged(nameof(CancelProjectEditVisibility));
    }

    private void SetProjectFormError(string message)
    {
        ProjectFormError = message;
        StatusMessage = message;
    }

    private void ClearProjectFormError()
    {
        if (!string.IsNullOrEmpty(ProjectFormError))
        {
            ProjectFormError = string.Empty;
        }
    }

    private void SetRuleFormError(string message)
    {
        RuleFormError = message;
        StatusMessage = message;
    }

    private void ClearRuleFormError()
    {
        if (!string.IsNullOrEmpty(RuleFormError))
        {
            RuleFormError = string.Empty;
        }
    }

    private void SetSettingsFormError(string message)
    {
        SettingsFormError = message;
        StatusMessage = message;
    }

    private void ClearSettingsFormError()
    {
        if (!string.IsNullOrEmpty(SettingsFormError))
        {
            SettingsFormError = string.Empty;
        }
    }

    private static string GetFriendlyMessage(Exception exception) =>
        exception is ArgumentException argumentException
            ? argumentException.Message.Split(" (Parameter", StringSplitOptions.None)[0]
            : exception.Message;

    private static void ReplaceItems<T>(
        ObservableCollection<T> target,
        IEnumerable<T> items)
    {
        target.Clear();

        foreach (var item in items)
        {
            target.Add(item);
        }
    }

    private static string GetStateLabel(WorkflowState state) => state switch
    {
        WorkflowState.Completed => "Concluído",
        WorkflowState.Running => "Em execução",
        WorkflowState.Blocked => "Bloqueado",
        _ => "Pendente"
    };

    private static string GetStateAccent(WorkflowState state) => state switch
    {
        WorkflowState.Completed => "#36D17C",
        WorkflowState.Running => "#2C9BFF",
        WorkflowState.Blocked => "#FF5D68",
        _ => "#627087"
    };

    private bool SetField<T>(
        ref T field,
        T value,
        [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public sealed class NavigationItemViewModel : INotifyPropertyChanged
{
    private bool _isActive;

    public NavigationItemViewModel(
        WorkspaceSection? section,
        string icon,
        string label,
        bool isActive,
        string? badge,
        string? shortcut = null)
    {
        Section = section;
        Icon = icon;
        Label = label;
        _isActive = isActive;
        Badge = badge;
        Shortcut = shortcut;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public WorkspaceSection? Section { get; }
    public string Icon { get; }
    public string Label { get; }
    public string? Badge { get; }
    public string? Shortcut { get; }
    public string? DisplayHint => Shortcut ?? Badge;
    public bool IsAvailable => Section is not null;
    public string ToolTip => IsAvailable
        ? string.IsNullOrWhiteSpace(Shortcut)
            ? Label
            : $"{Label} ({Shortcut})"
        : $"{Label}: será disponibilizado na fase de integração das automações.";

    public bool IsActive
    {
        get => _isActive;
        set
        {
            if (_isActive == value)
            {
                return;
            }

            _isActive = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsActive)));
        }
    }
}

public sealed record WorkflowStepViewModel(
    int Number,
    string Name,
    string Status,
    string Accent,
    bool IsRunning);

public sealed record AgentViewModel(
    string Name,
    string Role,
    string Status,
    string Accent,
    int Progress);

public sealed record MetricViewModel(
    string Label,
    string Value,
    int Progress,
    string Accent);

public sealed record ActivityViewModel(
    string Time,
    string Description,
    string Accent);

public sealed record ProjectValidationItemViewModel(
    string Code,
    string Area,
    string Title,
    string Detail,
    string Recommendation,
    string Status,
    string Accent)
{
    public static ProjectValidationItemViewModel From(ProjectValidationResult result) => new(
        result.Code,
        result.Area,
        result.Title,
        result.Detail,
        result.Recommendation,
        result.Status switch
        {
            ProjectValidationStatus.Passed => "Aprovado",
            ProjectValidationStatus.Warning => "Alerta",
            _ => "Erro"
        },
        result.Status switch
        {
            ProjectValidationStatus.Passed => "#36D17C",
            ProjectValidationStatus.Warning => "#F8C33A",
            _ => "#FF5D68"
        });
}

public sealed record ActivityHistoryItemViewModel(
    Guid Id,
    string Date,
    string Time,
    string Category,
    string Action,
    string Description,
    string ProjectName,
    string Level,
    string Accent)
{
    public static ActivityHistoryItemViewModel From(ActivityLogEntry entry) => new(
        entry.Id,
        entry.OccurredAt.ToString("dd/MM/yyyy"),
        entry.OccurredAt.ToString("HH:mm:ss"),
        entry.Category,
        entry.Action,
        entry.Description,
        entry.ProjectName ?? "Aplicativo",
        entry.Level switch
        {
            ActivityLevel.Success => "Sucesso",
            ActivityLevel.Warning => "Atenção",
            ActivityLevel.Error => "Erro",
            _ => "Informação"
        },
        entry.Level switch
        {
            ActivityLevel.Success => "#36D17C",
            ActivityLevel.Warning => "#F8C33A",
            ActivityLevel.Error => "#FF5D68",
            _ => "#2C9BFF"
        });
}

public sealed record ProjectListItemViewModel(
    Guid Id,
    string Name,
    string Type,
    string Scale,
    string Disciplines,
    int FileCount,
    int LayerCount,
    string UpdatedAt,
    DateTimeOffset UpdatedAtValue,
    bool IsArchived,
    string ArchivedAt,
    DateTimeOffset? ArchivedAtValue)
{
    public static ProjectListItemViewModel From(ProjectWorkspace project) => new(
        project.Id,
        project.Name,
        project.Type,
        $"{project.Floors} pavimento(s) • {project.Units} unidade(s)",
        string.Join(" • ", project.Disciplines),
        project.Files.Count,
        project.Layers.Count,
        project.UpdatedAt.ToString("dd/MM/yyyy HH:mm"),
        project.UpdatedAt,
        project.IsArchived,
        project.ArchivedAt?.ToString("dd/MM/yyyy HH:mm") ?? string.Empty,
        project.ArchivedAt);
}

public sealed record ProjectFileItemViewModel(
    Guid Id,
    string Name,
    string SourcePath,
    string Extension,
    string Size,
    string Kind,
    ProjectFileKind KindKey,
    string ImportedAt,
    bool Exists,
    bool HasChanged,
    string Status,
    string StatusAccent)
{
    public static ProjectFileItemViewModel From(ProjectFileInspection inspection)
    {
        var file = inspection.File;
        var status = !inspection.Exists
            ? "Ausente"
            : inspection.HasChanged
                ? "Alterado"
                : "Disponível";
        var statusAccent = !inspection.Exists
            ? "#FF5D68"
            : inspection.HasChanged
                ? "#F8C33A"
                : "#36D17C";

        return new ProjectFileItemViewModel(
            file.Id,
            file.Name,
            file.SourcePath,
            file.Extension.TrimStart('.').ToUpperInvariant(),
            FormatSize(inspection.CurrentSizeBytes ?? file.SizeBytes),
            GetKindLabel(file.Kind),
            file.Kind,
            file.ImportedAt.ToString("dd/MM/yyyy HH:mm"),
            inspection.Exists,
            inspection.HasChanged,
            status,
            statusAccent);
    }

    private static string FormatSize(long bytes)
    {
        const double kiloByte = 1024;
        const double megaByte = kiloByte * 1024;

        return bytes >= megaByte
            ? $"{bytes / megaByte:0.0} MB"
            : $"{bytes / kiloByte:0.0} KB";
    }

    private static string GetKindLabel(ProjectFileKind kind) => kind switch
    {
        ProjectFileKind.Drawing => "Desenho",
        ProjectFileKind.Document => "Documento",
        ProjectFileKind.Spreadsheet => "Planilha",
        ProjectFileKind.Image => "Imagem",
        _ => "Outro"
    };
}
