namespace AutoAIBuilder.Application.Diagnostics;

public sealed record DiagnosticLogEntry(
    DateTimeOffset OccurredAt,
    DiagnosticLevel Level,
    string Source,
    string Message,
    string? ExceptionType,
    string? ExceptionMessage,
    string? StackTrace,
    IReadOnlyDictionary<string, string> Properties);
