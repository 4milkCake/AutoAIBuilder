using System.IO;
using AutoAIBuilder.Application.Diagnostics;

namespace AutoAIBuilder.Desktop.ViewModels;

public sealed partial class MainWindowViewModel
{
    private void TryWriteDiagnostic(
        DiagnosticLevel level,
        string source,
        string message,
        Exception? exception = null,
        IReadOnlyDictionary<string, string>? properties = null)
    {
        try
        {
            _diagnosticLogger.Write(level, source, message, exception, properties);
        }
        catch (Exception loggingException) when (
            loggingException is IOException
                or UnauthorizedAccessException
                or InvalidOperationException)
        {
            // O diagnóstico local não pode interromper a operação principal.
        }
    }

    private void RefreshDiagnostics()
    {
        try
        {
            var snapshot = _diagnosticService.Capture();
            ReplaceItems(
                DiagnosticChecks,
                snapshot.Checks.Select(DiagnosticCheckItemViewModel.From));
            ReplaceItems(
                DiagnosticLogs,
                snapshot.RecentLogs.Select(DiagnosticLogItemViewModel.From));

            DiagnosticGeneratedAt =
                $"Verificado em {snapshot.GeneratedAt:dd/MM/yyyy HH:mm:ss}";
            DiagnosticSummary =
                $"{snapshot.Checks.Count} item(ns): {snapshot.ErrorCount} erro(s) "
                + $"e {snapshot.WarningCount} alerta(s). "
                + $"{snapshot.RecentLogs.Count} evento(s) técnico(s) recente(s).";
            OnPropertyChanged(nameof(EmptyDiagnosticLogVisibility));
        }
        catch (Exception exception) when (
            exception is IOException
                or UnauthorizedAccessException
                or InvalidOperationException)
        {
            DiagnosticChecks.Clear();
            DiagnosticLogs.Clear();
            DiagnosticGeneratedAt = "Falha na verificação";
            DiagnosticSummary = $"Não foi possível gerar o diagnóstico: {exception.Message}";
            OnPropertyChanged(nameof(EmptyDiagnosticLogVisibility));
            StatusMessage = DiagnosticSummary;
        }
    }
}

public sealed record DiagnosticCheckItemViewModel(
    string Name,
    string Value,
    string Detail,
    string Status,
    string Accent)
{
    public static DiagnosticCheckItemViewModel From(DiagnosticCheck check) => new(
        check.Name,
        check.Value,
        check.Detail,
        check.Status switch
        {
            DiagnosticStatus.Healthy => "OK",
            DiagnosticStatus.Warning => "Alerta",
            DiagnosticStatus.Error => "Erro",
            _ => "Informação"
        },
        check.Status switch
        {
            DiagnosticStatus.Healthy => "#36D17C",
            DiagnosticStatus.Warning => "#F8C33A",
            DiagnosticStatus.Error => "#FF5D68",
            _ => "#2C9BFF"
        });
}

public sealed record DiagnosticLogItemViewModel(
    string Date,
    string Time,
    string Level,
    string Source,
    string Message,
    string Detail,
    string Accent)
{
    public static DiagnosticLogItemViewModel From(DiagnosticLogEntry entry) => new(
        entry.OccurredAt.ToString("dd/MM/yyyy"),
        entry.OccurredAt.ToString("HH:mm:ss"),
        entry.Level switch
        {
            DiagnosticLevel.Warning => "Alerta",
            DiagnosticLevel.Error => "Erro",
            DiagnosticLevel.Critical => "Crítico",
            _ => "Informação"
        },
        entry.Source,
        entry.Message,
        entry.ExceptionMessage ?? string.Empty,
        entry.Level switch
        {
            DiagnosticLevel.Warning => "#F8C33A",
            DiagnosticLevel.Error or DiagnosticLevel.Critical => "#FF5D68",
            _ => "#2C9BFF"
        });
}
