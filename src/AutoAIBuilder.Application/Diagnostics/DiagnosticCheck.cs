namespace AutoAIBuilder.Application.Diagnostics;

public sealed record DiagnosticCheck(
    string Name,
    string Value,
    DiagnosticStatus Status,
    string Detail);
