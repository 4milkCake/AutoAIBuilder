namespace AutoAIBuilder.Application.Diagnostics;

public interface IDiagnosticLogger
{
    string StoragePath { get; }

    void Write(
        DiagnosticLevel level,
        string source,
        string message,
        Exception? exception = null,
        IReadOnlyDictionary<string, string>? properties = null);

    IReadOnlyList<DiagnosticLogEntry> GetRecent(int maximumEntries = 100);
}
