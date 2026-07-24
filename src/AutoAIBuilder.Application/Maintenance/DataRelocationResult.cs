namespace AutoAIBuilder.Application.Maintenance;

public sealed record DataRelocationResult(
    string CurrentDirectory,
    string NewDirectory,
    string NewDatabasePath,
    bool RequiresRestart);
