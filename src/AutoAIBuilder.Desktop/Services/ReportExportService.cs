using System.IO;
using System.Text;
using AutoAIBuilder.Infrastructure.Reports;
using Microsoft.Win32;

namespace AutoAIBuilder.Desktop.Services;

public sealed class ReportExportService(SimplePdfReportRenderer pdfRenderer) : IReportExportService
{
    public string? ExportTextReport(string suggestedFileName, string content)
    {
        var path = PickDestination(
            "Exportar relatório de prontidão",
            suggestedFileName,
            ".txt",
            "Relatório de texto|*.txt");
        if (path is null)
        {
            return null;
        }

        File.WriteAllText(path, content, new UTF8Encoding(false));
        return path;
    }

    public string? ExportCsvReport(string suggestedFileName, string content)
    {
        var path = PickDestination(
            "Exportar verificações em CSV",
            suggestedFileName,
            ".csv",
            "Arquivo CSV|*.csv");
        if (path is null)
        {
            return null;
        }

        File.WriteAllText(path, content, new UTF8Encoding(true));
        return path;
    }

    public string? ExportPdfReport(string suggestedFileName, string content)
    {
        var path = PickDestination(
            "Exportar relatório em PDF",
            suggestedFileName,
            ".pdf",
            "Documento PDF|*.pdf");
        if (path is null)
        {
            return null;
        }

        File.WriteAllBytes(path, pdfRenderer.Render(content));
        return path;
    }

    private static string? PickDestination(
        string title,
        string suggestedFileName,
        string defaultExtension,
        string filter)
    {
        var dialog = new SaveFileDialog
        {
            Title = title,
            FileName = suggestedFileName,
            DefaultExt = defaultExtension,
            AddExtension = true,
            Filter = filter,
            OverwritePrompt = true
        };

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }
}
