using System.Text.Json;
using AutoAIBuilder.Application.History;
using AutoAIBuilder.Application.Settings;
using AutoAIBuilder.Domain.Projects;

namespace AutoAIBuilder.Infrastructure.Persistence;

public sealed class LegacyJsonDataMigrator
{
    private const string ProjectsMigration = "legacy-projects-json-v1";
    private const string SettingsMigration = "legacy-settings-json-v1";
    private const string ActivityMigration = "legacy-activity-json-v1";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private readonly SqliteDatabase _database;
    private readonly string _projectsFile;
    private readonly string _settingsFile;
    private readonly string _activityFile;

    public LegacyJsonDataMigrator(
        SqliteDatabase database,
        string projectsFile,
        string settingsFile,
        string activityFile)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
        _projectsFile = Path.GetFullPath(projectsFile);
        _settingsFile = Path.GetFullPath(settingsFile);
        _activityFile = Path.GetFullPath(activityFile);
    }

    public static LegacyJsonDataMigrator CreateDefault(SqliteDatabase database) =>
        new(
            database,
            AppStoragePaths.ProjectsFile,
            AppStoragePaths.SettingsFile,
            AppStoragePaths.ActivityLogFile);

    public LegacyJsonMigrationResult Run()
    {
        _database.Initialize();

        var warnings = new List<string>();
        var projectCount = MigrateProjects(warnings);
        var importedSettings = MigrateSettings(warnings);
        var activityCount = MigrateActivity(warnings);

        return new LegacyJsonMigrationResult(
            projectCount,
            importedSettings,
            activityCount,
            warnings);
    }

    private int MigrateProjects(List<string> warnings)
    {
        if (_database.HasDataMigration(ProjectsMigration))
        {
            return 0;
        }

        if (!File.Exists(_projectsFile))
        {
            _database.MarkDataMigration(
                ProjectsMigration,
                "Arquivo legado não existia; nenhuma importação necessária.");
            return 0;
        }

        try
        {
            var projects =
                DeserializeFile<List<ProjectWorkspace>>(_projectsFile) ?? [];

            lock (_database.SyncRoot)
            {
                using var connection = _database.OpenConnection();
                using var transaction = connection.BeginTransaction();

                foreach (var project in projects)
                {
                    using var command = connection.CreateCommand();
                    command.Transaction = transaction;
                    command.CommandText =
                        """
                        INSERT OR IGNORE INTO Projects (Id, PayloadJson, UpdatedAt)
                        VALUES ($id, $payload, $updatedAt);
                        """;
                    command.Parameters.AddWithValue(
                        "$id",
                        project.Id.ToString("D"));
                    command.Parameters.AddWithValue(
                        "$payload",
                        JsonSerializer.Serialize(project, SerializerOptions));
                    command.Parameters.AddWithValue(
                        "$updatedAt",
                        project.UpdatedAt.UtcDateTime.ToString("O"));
                    command.ExecuteNonQuery();
                }

                transaction.Commit();
            }

            _database.MarkDataMigration(
                ProjectsMigration,
                $"{projects.Count} projeto(s) importado(s); JSON original preservado.");
            return projects.Count;
        }
        catch (Exception exception) when (IsLegacyReadFailure(exception))
        {
            warnings.Add(
                $"Projetos legados não foram importados; o arquivo foi preservado: "
                + exception.Message);
            return 0;
        }
    }

    private bool MigrateSettings(List<string> warnings)
    {
        if (_database.HasDataMigration(SettingsMigration))
        {
            return false;
        }

        if (!File.Exists(_settingsFile))
        {
            _database.MarkDataMigration(
                SettingsMigration,
                "Arquivo legado não existia; nenhuma importação necessária.");
            return false;
        }

        try
        {
            var settings =
                DeserializeFile<ApplicationSettings>(_settingsFile)
                ?? ApplicationSettings.CreateDefault();

            lock (_database.SyncRoot)
            {
                using var connection = _database.OpenConnection();
                using var transaction = connection.BeginTransaction();
                using var command = connection.CreateCommand();
                command.Transaction = transaction;
                command.CommandText =
                    """
                    INSERT OR IGNORE INTO ApplicationSettings (
                        Id, PayloadJson, UpdatedAt)
                    VALUES (1, $payload, $updatedAt);
                    """;
                command.Parameters.AddWithValue(
                    "$payload",
                    JsonSerializer.Serialize(settings, SerializerOptions));
                command.Parameters.AddWithValue(
                    "$updatedAt",
                    DateTimeOffset.UtcNow.ToString("O"));
                command.ExecuteNonQuery();
                transaction.Commit();
            }

            _database.MarkDataMigration(
                SettingsMigration,
                "Configurações importadas; JSON original preservado.");
            return true;
        }
        catch (Exception exception) when (IsLegacyReadFailure(exception))
        {
            warnings.Add(
                $"Configurações legadas não foram importadas; o arquivo foi preservado: "
                + exception.Message);
            return false;
        }
    }

    private int MigrateActivity(List<string> warnings)
    {
        if (_database.HasDataMigration(ActivityMigration))
        {
            return 0;
        }

        if (!File.Exists(_activityFile))
        {
            _database.MarkDataMigration(
                ActivityMigration,
                "Arquivo legado não existia; nenhuma importação necessária.");
            return 0;
        }

        try
        {
            var entries =
                DeserializeFile<List<ActivityLogEntry>>(_activityFile) ?? [];
            var entriesToImport = entries
                .OrderBy(entry => entry.OccurredAt)
                .TakeLast(SqliteActivityLogRepository.MaximumEntries)
                .ToArray();

            var repository = new SqliteActivityLogRepository(_database);
            foreach (var entry in entriesToImport)
            {
                repository.Append(entry);
            }

            _database.MarkDataMigration(
                ActivityMigration,
                $"{entriesToImport.Length} evento(s) importado(s); JSON original preservado.");
            return entriesToImport.Length;
        }
        catch (Exception exception) when (IsLegacyReadFailure(exception))
        {
            warnings.Add(
                $"Histórico legado não foi importado; o arquivo foi preservado: "
                + exception.Message);
            return 0;
        }
    }

    private static T? DeserializeFile<T>(string filePath)
    {
        var json = File.ReadAllText(filePath);
        if (string.IsNullOrWhiteSpace(json))
        {
            return default;
        }

        return JsonSerializer.Deserialize<T>(json, SerializerOptions);
    }

    private static bool IsLegacyReadFailure(Exception exception) =>
        exception is JsonException
            or IOException
            or UnauthorizedAccessException
            or InvalidDataException
            or ArgumentException;
}
