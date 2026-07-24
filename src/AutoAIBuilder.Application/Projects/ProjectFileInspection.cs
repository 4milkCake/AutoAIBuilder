using AutoAIBuilder.Domain.Projects;

namespace AutoAIBuilder.Application.Projects;

public sealed record ProjectFileInspection(
    ProjectFile File,
    bool Exists,
    bool HasChanged,
    long? CurrentSizeBytes,
    DateTimeOffset? CurrentLastWriteTime);
