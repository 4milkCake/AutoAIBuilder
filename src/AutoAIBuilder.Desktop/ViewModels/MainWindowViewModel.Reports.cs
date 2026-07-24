using System.IO;
using AutoAIBuilder.Application.History;
using AutoAIBuilder.Application.Reports;

namespace AutoAIBuilder.Desktop.ViewModels;

public sealed partial class MainWindowViewModel
{
    private void GenerateProjectReport()
    {
        if (SelectedProject is null)
        {
            ResetReportState();
            StatusMessage = "Selecione um projeto ativo antes de gerar o relatório.";
            return;
        }

        var project = _workspaceService.GetProject(SelectedProject.Id);
        if (project is null)
        {
            ResetReportState();
            StatusMessage = "O projeto selecionado não foi encontrado.";
            return;
        }

        try
        {
            var inspections = _workspaceService.InspectFiles(project.Id);
            var validation = _validationService.Validate(project, inspections);
            _currentReport = _reportService.Build(project, inspections, validation);

            ReportContent = _currentReport.Content;
            ReportGeneratedAt = $"Gerado em {_currentReport.GeneratedAt:dd/MM/yyyy HH:mm:ss}";
            ReportReadinessStatus = _currentReport.IsAutomationBlocked
                ? "AUTOMAÇÕES BLOQUEADAS"
                : _currentReport.WarningCount > 0
                    ? "APTO COM ALERTAS"
                    : "PROJETO APTO";
            ReportReadinessAccent = _currentReport.IsAutomationBlocked
                ? "#FF5D68"
                : _currentReport.WarningCount > 0
                    ? "#F8C33A"
                    : "#36D17C";
            (ExportProjectReportCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (ExportProjectReportCsvCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (ExportProjectReportPdfCommand as RelayCommand)?.RaiseCanExecuteChanged();

            TryRecordActivity(
                "Relatórios",
                "Relatório gerado",
                $"Relatório de prontidão gerado com {_currentReport.ErrorCount} erro(s) e {_currentReport.WarningCount} alerta(s).",
                _currentReport.ErrorCount > 0 ? ActivityLevel.Warning : ActivityLevel.Success,
                SelectedProject);
            StatusMessage = "Relatório de prontidão atualizado; nenhum arquivo foi criado até a exportação.";
        }
        catch (InvalidOperationException exception)
        {
            ResetReportState();
            StatusMessage = exception.Message;
        }
    }

    private void ExportProjectReport()
    {
        ExportCurrentReport(
            "TXT",
            report => _reportExportService.ExportTextReport(
                report.SuggestedFileName,
                report.Content));
    }

    private void ExportProjectReportCsv()
    {
        ExportCurrentReport(
            "CSV",
            report => _reportExportService.ExportCsvReport(
                report.SuggestedCsvFileName,
                report.CsvContent));
    }

    private void ExportProjectReportPdf()
    {
        ExportCurrentReport(
            "PDF",
            report => _reportExportService.ExportPdfReport(
                report.SuggestedPdfFileName,
                report.Content));
    }

    private void ExportCurrentReport(
        string format,
        Func<ProjectReadinessReport, string?> export)
    {
        if (_currentReport is null)
        {
            StatusMessage = "Gere o relatório antes de exportá-lo.";
            return;
        }

        try
        {
            var exportedPath = export(_currentReport);

            if (string.IsNullOrWhiteSpace(exportedPath))
            {
                StatusMessage = "Exportação cancelada; nenhum arquivo foi criado.";
                return;
            }

            TryRecordActivity(
                "Relatórios",
                "Relatório exportado",
                $"Relatório {format} exportado para {exportedPath}.",
                ActivityLevel.Success,
                SelectedProject);
            StatusMessage = $"Relatório exportado para “{exportedPath}”.";
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException)
        {
            TryRecordActivity(
                "Relatórios",
                "Falha na exportação",
                $"Falha ao exportar {format}: {exception.Message}",
                ActivityLevel.Error,
                SelectedProject);
            StatusMessage = $"Não foi possível exportar o relatório: {exception.Message}";
        }
    }

    private void ResetReportState()
    {
        _currentReport = null;
        ReportContent = SelectedProject is null
            ? "Selecione um projeto ativo para gerar o relatório de prontidão."
            : "Gere o relatório para consolidar as regras, arquivos e verificações do projeto.";
        ReportGeneratedAt = "Ainda não gerado";
        ReportReadinessStatus = "AGUARDANDO RELATÓRIO";
        ReportReadinessAccent = "#627087";
        (ExportProjectReportCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (ExportProjectReportCsvCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (ExportProjectReportPdfCommand as RelayCommand)?.RaiseCanExecuteChanged();
    }
}
