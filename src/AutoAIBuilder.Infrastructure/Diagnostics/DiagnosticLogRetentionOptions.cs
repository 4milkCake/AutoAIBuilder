namespace AutoAIBuilder.Infrastructure.Diagnostics;

public sealed record DiagnosticLogRetentionOptions
{
    public const long DefaultMaximumFileSizeBytes = 2 * 1024 * 1024;
    public const int DefaultMaximumArchiveFiles = 5;
    public const int DefaultMaximumEntrySizeBytes = 24 * 1024;

    public long MaximumFileSizeBytes { get; init; } =
        DefaultMaximumFileSizeBytes;

    public int MaximumArchiveFiles { get; init; } =
        DefaultMaximumArchiveFiles;

    public int MaximumEntrySizeBytes { get; init; } =
        DefaultMaximumEntrySizeBytes;

    public int MaximumMessageLength { get; init; } = 4_000;

    public int MaximumStackTraceLength { get; init; } = 12_000;

    public int MaximumPropertyCount { get; init; } = 32;

    public int MaximumPropertyValueLength { get; init; } = 1_000;

    internal void Validate()
    {
        if (MaximumEntrySizeBytes < 512)
        {
            throw new ArgumentOutOfRangeException(
                nameof(MaximumEntrySizeBytes),
                "Cada evento deve permitir ao menos 512 bytes.");
        }

        if (MaximumFileSizeBytes < MaximumEntrySizeBytes)
        {
            throw new ArgumentOutOfRangeException(
                nameof(MaximumFileSizeBytes),
                "O arquivo deve comportar ao menos um evento completo.");
        }

        if (MaximumArchiveFiles is < 1 or > 20)
        {
            throw new ArgumentOutOfRangeException(
                nameof(MaximumArchiveFiles),
                "A retenção deve manter entre 1 e 20 arquivos anteriores.");
        }

        if (MaximumMessageLength is < 128 or > 32_000)
        {
            throw new ArgumentOutOfRangeException(nameof(MaximumMessageLength));
        }

        if (MaximumStackTraceLength is < 128 or > 64_000)
        {
            throw new ArgumentOutOfRangeException(nameof(MaximumStackTraceLength));
        }

        if (MaximumPropertyCount is < 1 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(MaximumPropertyCount));
        }

        if (MaximumPropertyValueLength is < 32 or > 8_000)
        {
            throw new ArgumentOutOfRangeException(
                nameof(MaximumPropertyValueLength));
        }
    }
}
