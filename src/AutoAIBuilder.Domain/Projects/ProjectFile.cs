namespace AutoAIBuilder.Domain.Projects;

public sealed record ProjectFile(
    Guid Id,
    string Name,
    string SourcePath,
    string Extension,
    long SizeBytes,
    ProjectFileKind Kind,
    DateTimeOffset ImportedAt)
{
    public DateTimeOffset? LastKnownWriteTime { get; init; }
}
