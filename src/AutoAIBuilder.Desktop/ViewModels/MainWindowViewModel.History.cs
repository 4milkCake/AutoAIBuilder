using System.IO;
using AutoAIBuilder.Application.Diagnostics;
using AutoAIBuilder.Application.History;

namespace AutoAIBuilder.Desktop.ViewModels;

public sealed partial class MainWindowViewModel
{
    private void RefreshHistory()
    {
        try
        {
            _allHistoryEntries.Clear();
            _allHistoryEntries.AddRange(
                _activityLogService.GetRecent()
                    .Select(ActivityHistoryItemViewModel.From));
            RefreshHistoryFilters();
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException)
        {
            _allHistoryEntries.Clear();
            HistoryEntries.Clear();
            OnPropertyChanged(nameof(HistorySummary));
            OnPropertyChanged(nameof(EmptyHistoryVisibility));
            StatusMessage = $"Não foi possível carregar o histórico: {exception.Message}";
        }
    }

    private void RefreshHistoryFilters()
    {
        IEnumerable<ActivityHistoryItemViewModel> query = _allHistoryEntries;

        if (!string.Equals(
                SelectedHistoryFilter,
                "Todas as categorias",
                StringComparison.Ordinal))
        {
            query = query.Where(entry =>
                string.Equals(
                    entry.Category,
                    SelectedHistoryFilter,
                    StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(HistorySearchText))
        {
            var search = HistorySearchText.Trim();
            query = query.Where(entry =>
                entry.Action.Contains(search, StringComparison.OrdinalIgnoreCase)
                || entry.Description.Contains(search, StringComparison.OrdinalIgnoreCase)
                || entry.ProjectName.Contains(search, StringComparison.OrdinalIgnoreCase)
                || entry.Category.Contains(search, StringComparison.OrdinalIgnoreCase));
        }

        ReplaceItems(HistoryEntries, query);
        OnPropertyChanged(nameof(HistorySummary));
        OnPropertyChanged(nameof(EmptyHistoryVisibility));
    }

    private void TryRecordActivity(
        string category,
        string action,
        string description,
        ActivityLevel level,
        ProjectListItemViewModel? project)
    {
        try
        {
            _activityLogService.Record(
                category,
                action,
                description,
                level,
                project?.Id,
                project?.Name);
        }
        catch (Exception exception) when (
            exception is IOException
                or UnauthorizedAccessException
                or System.Text.Json.JsonException)
        {
            // O histórico não deve interromper a ação principal do usuário.
            TryWriteDiagnostic(
                DiagnosticLevel.Warning,
                "ActivityLog",
                "O histórico funcional não pôde registrar uma ação.",
                exception,
                new Dictionary<string, string>
                {
                    ["category"] = category,
                    ["action"] = action
                });
        }
    }
}
