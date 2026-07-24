using System.Text.Json;

namespace AutoAIBuilder.Infrastructure.Persistence;

public static class StorageLocationConfiguration
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public static string ResolveDataDirectory()
    {
        try
        {
            var configuration = LoadDocument();

            return string.IsNullOrWhiteSpace(configuration?.DataDirectory)
                ? AppStoragePaths.DefaultDataDirectory
                : Path.GetFullPath(
                    Environment.ExpandEnvironmentVariables(configuration.DataDirectory));
        }
        catch (Exception exception) when (
            exception is JsonException
                or IOException
                or UnauthorizedAccessException
                or ArgumentException
                or NotSupportedException)
        {
            return AppStoragePaths.DefaultDataDirectory;
        }
    }

    public static string? GetPendingDataDirectory()
    {
        try
        {
            var pendingDirectory = LoadDocument()?.PendingDataDirectory;
            return string.IsNullOrWhiteSpace(pendingDirectory)
                ? null
                : Path.GetFullPath(
                    Environment.ExpandEnvironmentVariables(pendingDirectory));
        }
        catch (Exception exception) when (
            exception is JsonException
                or IOException
                or UnauthorizedAccessException
                or ArgumentException
                or NotSupportedException)
        {
            return null;
        }
    }

    public static void ScheduleDataDirectoryChange(string dataDirectory)
    {
        if (string.IsNullOrWhiteSpace(dataDirectory))
        {
            throw new ArgumentException(
                "A pasta de dados é obrigatória.",
                nameof(dataDirectory));
        }

        var normalizedDirectory = Path.GetFullPath(dataDirectory);
        WriteDocument(new StorageLocationDocument(
            ResolveDataDirectory(),
            normalizedDirectory));
    }

    public static void ActivatePendingDataDirectory()
    {
        var pendingDirectory = GetPendingDataDirectory();
        if (pendingDirectory is null)
        {
            return;
        }

        WriteDocument(new StorageLocationDocument(
            pendingDirectory,
            PendingDataDirectory: null));
    }

    private static StorageLocationDocument? LoadDocument()
    {
        if (!File.Exists(AppStoragePaths.StorageLocationFile))
        {
            return null;
        }

        var json = File.ReadAllText(AppStoragePaths.StorageLocationFile);
        return JsonSerializer.Deserialize<StorageLocationDocument>(
            json,
            SerializerOptions);
    }

    private static void WriteDocument(StorageLocationDocument document)
    {
        Directory.CreateDirectory(AppStoragePaths.RootDirectory);

        var temporaryPath =
            $"{AppStoragePaths.StorageLocationFile}.{Guid.NewGuid():N}.tmp";

        try
        {
            File.WriteAllText(
                temporaryPath,
                JsonSerializer.Serialize(document, SerializerOptions));
            File.Move(
                temporaryPath,
                AppStoragePaths.StorageLocationFile,
                overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    private sealed record StorageLocationDocument(
        string DataDirectory,
        string? PendingDataDirectory = null);
}
