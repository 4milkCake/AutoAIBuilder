namespace AutoAIBuilder.Infrastructure.Persistence;

public static class AppStoragePaths
{
    public static string RootDirectory =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AutoAIBuilder");

    public static string DataDirectory => Path.Combine(RootDirectory, "Data");

    public static string LogDirectory => Path.Combine(RootDirectory, "Logs");

    public static string ProjectsFile => Path.Combine(DataDirectory, "projects.json");

    public static string SettingsFile => Path.Combine(DataDirectory, "settings.json");

    public static string ActivityLogFile => Path.Combine(DataDirectory, "activity-log.json");

    public static string DiagnosticLogFile => Path.Combine(LogDirectory, "diagnostics.jsonl");
}
