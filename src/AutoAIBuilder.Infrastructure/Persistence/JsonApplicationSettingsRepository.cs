using System.Text.Json;
using AutoAIBuilder.Application.Settings;

namespace AutoAIBuilder.Infrastructure.Persistence;

public sealed class JsonApplicationSettingsRepository : IApplicationSettingsRepository
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    private readonly string _filePath;
    private readonly object _sync = new();

    public JsonApplicationSettingsRepository(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("O caminho das configurações é obrigatório.", nameof(filePath));
        }

        _filePath = Path.GetFullPath(filePath);
    }

    public static JsonApplicationSettingsRepository CreateDefault()
        => new(AppStoragePaths.SettingsFile);

    public ApplicationSettings Load()
    {
        lock (_sync)
        {
            if (!File.Exists(_filePath))
            {
                return ApplicationSettings.CreateDefault();
            }

            var json = File.ReadAllText(_filePath);
            return string.IsNullOrWhiteSpace(json)
                ? ApplicationSettings.CreateDefault()
                : JsonSerializer.Deserialize<ApplicationSettings>(json, SerializerOptions)
                    ?? ApplicationSettings.CreateDefault();
        }
    }

    public void Save(ApplicationSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        lock (_sync)
        {
            var directory = Path.GetDirectoryName(_filePath)
                ?? throw new InvalidOperationException("Não foi possível determinar a pasta de configurações.");

            Directory.CreateDirectory(directory);

            var temporaryPath = $"{_filePath}.tmp";
            File.WriteAllText(
                temporaryPath,
                JsonSerializer.Serialize(settings, SerializerOptions));
            File.Move(temporaryPath, _filePath, true);
        }
    }
}
