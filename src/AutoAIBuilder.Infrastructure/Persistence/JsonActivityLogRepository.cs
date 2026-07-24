using System.Text.Json;
using AutoAIBuilder.Application.History;

namespace AutoAIBuilder.Infrastructure.Persistence;

public sealed class JsonActivityLogRepository : IActivityLogRepository
{
    public const int MaximumEntries = 500;

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    private readonly string _filePath;
    private readonly object _sync = new();

    public JsonActivityLogRepository(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("O caminho do histórico é obrigatório.", nameof(filePath));
        }

        _filePath = Path.GetFullPath(filePath);
    }

    public static JsonActivityLogRepository CreateDefault()
        => new(AppStoragePaths.ActivityLogFile);

    public IReadOnlyList<ActivityLogEntry> GetAll()
    {
        lock (_sync)
        {
            return Load().ToArray();
        }
    }

    public void Append(ActivityLogEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        lock (_sync)
        {
            var entries = Load(throwOnInvalidJson: true);
            entries.Add(entry);
            entries = entries
                .OrderBy(item => item.OccurredAt)
                .TakeLast(MaximumEntries)
                .ToList();

            var directory = Path.GetDirectoryName(_filePath)
                ?? throw new InvalidOperationException("Não foi possível determinar a pasta do histórico.");
            Directory.CreateDirectory(directory);

            var temporaryPath = $"{_filePath}.tmp";
            File.WriteAllText(
                temporaryPath,
                JsonSerializer.Serialize(entries, SerializerOptions));
            File.Move(temporaryPath, _filePath, true);
        }
    }

    private List<ActivityLogEntry> Load(bool throwOnInvalidJson = false)
    {
        if (!File.Exists(_filePath))
        {
            return [];
        }

        try
        {
            var json = File.ReadAllText(_filePath);
            return string.IsNullOrWhiteSpace(json)
                ? []
                : JsonSerializer.Deserialize<List<ActivityLogEntry>>(json, SerializerOptions) ?? [];
        }
        catch (JsonException exception) when (throwOnInvalidJson)
        {
            throw new InvalidDataException(
                "O arquivo de histórico está corrompido e foi preservado sem alterações.",
                exception);
        }
        catch (JsonException)
        {
            return [];
        }
    }
}
