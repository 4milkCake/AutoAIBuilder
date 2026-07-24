using AutoAIBuilder.Application.Dashboard;
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
using AutoAIBuilder.Infrastructure.Diagnostics;
using AutoAIBuilder.Infrastructure.Persistence;
using AutoAIBuilder.Infrastructure.Reports;

namespace AutoAIBuilder.Desktop.Composition;

public static class DesktopCompositionRoot
{
    private static readonly Lazy<IDiagnosticLogger> DiagnosticLoggerFactory =
        new(JsonLinesDiagnosticLogger.CreateDefault);

    public static IDiagnosticLogger DiagnosticLogger => DiagnosticLoggerFactory.Value;

    public static MainWindowViewModel CreateMainWindowViewModel()
    {
        var projectRepository = JsonProjectRepository.CreateDefault();
        var workspaceService = new ProjectWorkspaceService(projectRepository);
        var settingsService = new ApplicationSettingsService(
            JsonApplicationSettingsRepository.CreateDefault());
        var activityLogService = new ActivityLogService(
            JsonActivityLogRepository.CreateDefault());
        var diagnosticService = new EnvironmentDiagnosticService(DiagnosticLogger);

        return new MainWindowViewModel(
            new ProjectDashboardProvider(),
            workspaceService,
            settingsService,
            new ProjectValidationService(),
            new ProjectReportService(),
            activityLogService,
            new ActiveProjectContext(),
            DiagnosticLogger,
            diagnosticService,
            new NavigationService(),
            new NotificationService(),
            new FilePickerService(),
            new ReportExportService(new SimplePdfReportRenderer()),
            new FileSystemLauncher(),
            new DialogService());
    }
}
