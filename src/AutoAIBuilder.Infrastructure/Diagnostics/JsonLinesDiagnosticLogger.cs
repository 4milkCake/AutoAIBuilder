using System.Text.Json;
using AutoAIBuilder.Application.Diagnostics;
using AutoAIBuilder.Infrastructure.Persistence;

namespace AutoAIBuilder.Infrastructure.Diagnostics;

public sealed class JsonLinesDiagnosticLogger : IDiagnosticLogger
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly object _sync = new();

    public JsonLinesDiagnosticLogger(string storagePath)
    {
        if (string.IsNullOrWhiteSpace(storagePath))
        {
            throw new ArgumentException(
                "O caminho do log de diagnóstico é obrigatório.",
                nameof(storagePath));
        }

        StoragePath = Path.GetFullPath(storagePath);
    }

    public string StoragePath { get; }

    public static JsonLinesDiagnosticLogger CreateDefault() =>
        new(AppStoragePaths.DiagnosticLogFile);

    public void Write(
        DiagnosticLevel level,
        string source,
        string message,
        Exception? exception = null,
        IReadOnlyDictionary<string, string>? properties = null)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            throw new ArgumentException("A origem do diagnóstico é obrigatória.", nameof(source));
        }

        if (string.IsNullOrWhiteSpace(message))
        {
            throw new ArgumentException("A mensagem do diagnóstico é obrigatória.", nameof(message));
        }

        var entry = new DiagnosticLogEntry(
            DateTimeOffset.Now,
            level,
            source.Trim(),
            message.Trim(),
            exception?.GetType().FullName,
            exception?.Message,
            exception?.StackTrace,
            properties ?? new Dictionary<string, string>());

        lock (_sync)
        {
            var directory = Path.GetDirectoryName(StoragePath)
                ?? throw new InvalidOperationException(
                    "Não foi possível determinar a pasta do log de diagnóstico.");
            Directory.CreateDirectory(directory);
            File.AppendAllText(
                StoragePath,
                JsonSerializer.Serialize(entry, SerializerOptions) + Environment.NewLine);
        }
    }

    public IReadOnlyList<DiagnosticLogEntry> GetRecent(int maximumEntries = 100)
    {
        if (maximumEntries is < 1 or > 1000)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximumEntries),
                "O limite deve estar entre 1 e 1000.");
        }

        lock (_sync)
        {
            if (!File.Exists(StoragePath))
            {
                return [];
            }

            return File.ReadLines(StoragePath)
                .Reverse()
                .Select(TryDeserialize)
                .Where(entry => entry is not null)
                .Take(maximumEntries)
                .Cast<DiagnosticLogEntry>()
                .ToArray();
        }
    }

    private static DiagnosticLogEntry? TryDeserialize(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<DiagnosticLogEntry>(line, SerializerOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
