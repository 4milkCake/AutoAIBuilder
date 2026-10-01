namespace AutoAIBuilder.Infrastructure.Persistence;

public static class AppStoragePaths
{
    public static string RootDirectory =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AutoAIBuilder");

    public static string DefaultDataDirectory => Path.Combine(RootDirectory, "Data");

    public static string DataDirectory => StorageLocationConfiguration.ResolveDataDirectory();

    public static string LogDirectory => Path.Combine(RootDirectory, "Logs");

    public static string BackupDirectory => Path.Combine(DataDirectory, "Backups");

    public static string CadVisualizationDirectory =>
        Path.Combine(DataDirectory, "CadVisualization");

    public static string RecognitionDirectory =>
        Path.Combine(DataDirectory, "Recognition");

    public static string DatabaseFile => Path.Combine(DataDirectory, "autoaibuilder.db");

    public static string StorageLocationFile =>
        Path.Combine(RootDirectory, "storage-location.json");

    public static string ProjectsFile => Path.Combine(DefaultDataDirectory, "projects.json");

    public static string SettingsFile => Path.Combine(DefaultDataDirectory, "settings.json");

    public static string ActivityLogFile => Path.Combine(DefaultDataDirectory, "activity-log.json");

    public static string DiagnosticLogFile => Path.Combine(LogDirectory, "diagnostics.jsonl");
}
