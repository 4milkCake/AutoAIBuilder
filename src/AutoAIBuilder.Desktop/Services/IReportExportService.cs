namespace AutoAIBuilder.Desktop.Services;

public interface IReportExportService
{
    string? ExportTextReport(string suggestedFileName, string content);

    string? ExportCsvReport(string suggestedFileName, string content);

    string? ExportPdfReport(string suggestedFileName, string content);
}
