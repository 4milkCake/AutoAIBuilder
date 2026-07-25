using System.IO;
using System.Windows;
using AutoAIBuilder.Application.Diagnostics;
using AutoAIBuilder.Application.Operations;

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
            RefreshOperationExecutions();

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

    public Visibility EmptyOperationExecutionVisibility =>
        OperationExecutions.Count == 0
            ? Visibility.Visible
            : Visibility.Collapsed;

    private void RefreshOperationExecutions()
    {
        try
        {
            ReplaceItems(
                OperationExecutions,
                _operationCoordinator
                    .GetRecent(25)
                    .Select(OperationExecutionItemViewModel.From));
        }
        catch (Exception exception) when (
            exception is IOException
                or UnauthorizedAccessException
                or InvalidDataException
                or InvalidOperationException)
        {
            OperationExecutions.Clear();
            TryWriteDiagnostic(
                DiagnosticLevel.Warning,
                "OperationEngine",
                "Não foi possível carregar o histórico operacional.",
                exception);
        }

        OnPropertyChanged(nameof(EmptyOperationExecutionVisibility));
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

public sealed record OperationExecutionItemViewModel(
    string Date,
    string Time,
    string Name,
    string Status,
    int Progress,
    string ProgressText,
    string Detail,
    string Accent)
{
    public static OperationExecutionItemViewModel From(
        OperationExecution execution)
    {
        var status = execution.Status switch
        {
            OperationExecutionStatus.Pending => "Aguardando",
            OperationExecutionStatus.Running => "Em execução",
            OperationExecutionStatus.Succeeded => "Concluída",
            OperationExecutionStatus.Cancelled => "Cancelada",
            OperationExecutionStatus.TimedOut => "Tempo excedido",
            OperationExecutionStatus.Failed => "Falhou",
            OperationExecutionStatus.Interrupted => "Interrompida",
            _ => execution.Status.ToString()
        };
        var accent = execution.Status switch
        {
            OperationExecutionStatus.Succeeded => "#36D17C",
            OperationExecutionStatus.Pending
                or OperationExecutionStatus.Running => "#2C9BFF",
            OperationExecutionStatus.Cancelled
                or OperationExecutionStatus.TimedOut
                or OperationExecutionStatus.Interrupted => "#F8C33A",
            _ => "#FF5D68"
        };
        var detail = string.IsNullOrWhiteSpace(execution.ErrorMessage)
            ? execution.CurrentStep
            : $"{execution.CurrentStep}: {execution.ErrorMessage}";

        return new OperationExecutionItemViewModel(
            execution.CreatedAt.ToLocalTime().ToString("dd/MM/yyyy"),
            execution.CreatedAt.ToLocalTime().ToString("HH:mm:ss"),
            execution.DisplayName,
            status,
            execution.Progress,
            $"{execution.Progress}%",
            detail,
            accent);
    }
}
