using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using AutoAIBuilder.Application.Diagnostics;
using AutoAIBuilder.Infrastructure.Persistence;

namespace AutoAIBuilder.Infrastructure.Diagnostics;

public sealed class JsonLinesDiagnosticLogger : IDiagnosticLogger
{
    private const string RedactedValue = "[REMOVIDO]";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private static readonly Regex SensitiveKeyPattern = new(
        "password|senha|token|secret|segredo|api[-_ ]?key|authorization|"
        + "autorizacao|credential|credencial",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex InlineSecretPattern = new(
        @"\b(password|senha|token|secret|segredo|api[-_ ]?key|authorization|"
        + @"autorizacao|credential|credencial)\b\s*[:=]\s*[^\s,;]+",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex BearerTokenPattern = new(
        @"\bBearer\s+[A-Za-z0-9._~+/=-]+",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private readonly object _sync = new();
    private readonly DiagnosticLogRetentionOptions _retention;

    public JsonLinesDiagnosticLogger(
        string storagePath,
        DiagnosticLogRetentionOptions? retention = null)
    {
        if (string.IsNullOrWhiteSpace(storagePath))
        {
            throw new ArgumentException(
                "O caminho do log de diagnóstico é obrigatório.",
                nameof(storagePath));
        }

        StoragePath = Path.GetFullPath(storagePath);
        _retention = retention ?? new DiagnosticLogRetentionOptions();
        _retention.Validate();
    }

    public string StoragePath { get; }

    public DiagnosticLogRetentionOptions Retention => _retention;

    public int ArchiveCount
    {
        get
        {
            lock (_sync)
            {
                return GetArchivePaths()
                    .Count(File.Exists);
            }
        }
    }

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

        var entry = SanitizeEntry(new DiagnosticLogEntry(
            DateTimeOffset.Now,
            level,
            Limit(source.Trim(), 200),
            message.Trim(),
            exception?.GetType().FullName,
            exception?.Message,
            exception?.StackTrace,
            properties ?? new Dictionary<string, string>()));
        var serializedEntry = SerializeWithinLimit(entry);
        var serializedBytes =
            Encoding.UTF8.GetByteCount(serializedEntry + Environment.NewLine);

        lock (_sync)
        {
            var directory = Path.GetDirectoryName(StoragePath)
                ?? throw new InvalidOperationException(
                    "Não foi possível determinar a pasta do log de diagnóstico.");
            Directory.CreateDirectory(directory);
            RotateIfRequired(serializedBytes);
            File.AppendAllText(
                StoragePath,
                serializedEntry + Environment.NewLine,
                Encoding.UTF8);
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
            var entries = new List<DiagnosticLogEntry>(maximumEntries);
            foreach (var path in GetLogPathsNewestFirst())
            {
                if (!File.Exists(path))
                {
                    continue;
                }

                foreach (var line in File.ReadLines(path).Reverse())
                {
                    var entry = TryDeserialize(line);
                    if (entry is not null)
                    {
                        entries.Add(entry);
                    }

                    if (entries.Count == maximumEntries)
                    {
                        return entries;
                    }
                }
            }

            return entries;
        }
    }

    private DiagnosticLogEntry SanitizeEntry(DiagnosticLogEntry entry)
    {
        var sanitizedProperties = entry.Properties
            .Take(_retention.MaximumPropertyCount)
            .ToDictionary(
                pair => Limit(pair.Key, 200),
                pair => SensitiveKeyPattern.IsMatch(pair.Key)
                    ? RedactedValue
                    : Limit(
                        Redact(pair.Value),
                        _retention.MaximumPropertyValueLength),
                StringComparer.OrdinalIgnoreCase);

        return entry with
        {
            Source = Limit(entry.Source, 200),
            Message = Limit(
                Redact(entry.Message),
                _retention.MaximumMessageLength),
            ExceptionType = entry.ExceptionType is null
                ? null
                : Limit(entry.ExceptionType, 500),
            ExceptionMessage = entry.ExceptionMessage is null
                ? null
                : Limit(
                    Redact(entry.ExceptionMessage),
                    _retention.MaximumMessageLength),
            StackTrace = entry.StackTrace is null
                ? null
                : Limit(
                    Redact(entry.StackTrace),
                    _retention.MaximumStackTraceLength),
            Properties = sanitizedProperties
        };
    }

    private string SerializeWithinLimit(DiagnosticLogEntry entry)
    {
        var candidate = entry;
        var serialized = JsonSerializer.Serialize(candidate, SerializerOptions);
        if (Encoding.UTF8.GetByteCount(serialized) <=
            _retention.MaximumEntrySizeBytes)
        {
            return serialized;
        }

        candidate = candidate with
        {
            StackTrace = null,
            Properties = candidate.Properties
                .Take(8)
                .ToDictionary(
                    pair => pair.Key,
                    pair => Limit(pair.Value, 128),
                    StringComparer.OrdinalIgnoreCase),
            ExceptionMessage = candidate.ExceptionMessage is null
                ? null
                : Limit(candidate.ExceptionMessage, 512)
        };

        serialized = JsonSerializer.Serialize(candidate, SerializerOptions);
        var messageLength = Math.Min(candidate.Message.Length, 1_024);
        while (Encoding.UTF8.GetByteCount(serialized) >
                   _retention.MaximumEntrySizeBytes
               && messageLength > 64)
        {
            messageLength /= 2;
            candidate = candidate with
            {
                Message = Limit(candidate.Message, messageLength)
            };
            serialized = JsonSerializer.Serialize(candidate, SerializerOptions);
        }

        if (Encoding.UTF8.GetByteCount(serialized) >
            _retention.MaximumEntrySizeBytes)
        {
            candidate = candidate with
            {
                Message = "Evento reduzido por exceder o limite configurado.",
                ExceptionMessage = null,
                Properties = new Dictionary<string, string>()
            };
            serialized = JsonSerializer.Serialize(candidate, SerializerOptions);
        }

        return serialized;
    }

    private void RotateIfRequired(int incomingBytes)
    {
        if (!File.Exists(StoragePath))
        {
            return;
        }

        var currentLength = new FileInfo(StoragePath).Length;
        if (currentLength + incomingBytes <= _retention.MaximumFileSizeBytes)
        {
            return;
        }

        var archives = GetArchivePaths();
        for (var index = archives.Count - 1; index >= 1; index--)
        {
            var source = archives[index - 1];
            var destination = archives[index];
            if (File.Exists(source))
            {
                File.Move(source, destination, overwrite: true);
            }
        }

        File.Move(StoragePath, archives[0], overwrite: true);
    }

    private IReadOnlyList<string> GetLogPathsNewestFirst() =>
        [StoragePath, .. GetArchivePaths()];

    private IReadOnlyList<string> GetArchivePaths()
    {
        var directory = Path.GetDirectoryName(StoragePath)
            ?? throw new InvalidOperationException(
                "Não foi possível determinar a pasta do log de diagnóstico.");
        var fileName = Path.GetFileNameWithoutExtension(StoragePath);
        var extension = Path.GetExtension(StoragePath);
        return Enumerable.Range(1, _retention.MaximumArchiveFiles)
            .Select(index =>
                Path.Combine(directory, $"{fileName}.{index}{extension}"))
            .ToArray();
    }

    private static string Redact(string value)
    {
        var sanitized = InlineSecretPattern.Replace(
            value,
            match =>
            {
                var separatorIndex = match.Value.IndexOfAny([':', '=']);
                var key = separatorIndex >= 0
                    ? match.Value[..separatorIndex].Trim()
                    : "credencial";
                return $"{key}={RedactedValue}";
            });
        return BearerTokenPattern.Replace(
            sanitized,
            $"Bearer {RedactedValue}");
    }

    private static string Limit(string value, int maximumLength) =>
        value.Length <= maximumLength
            ? value
            : value[..maximumLength];

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
