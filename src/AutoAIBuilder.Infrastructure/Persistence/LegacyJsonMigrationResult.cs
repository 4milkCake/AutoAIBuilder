namespace AutoAIBuilder.Infrastructure.Persistence;

public sealed record LegacyJsonMigrationResult(
    int ImportedProjects,
    bool ImportedSettings,
    int ImportedActivityEntries,
    IReadOnlyList<string> Warnings)
{
    public bool HasWarnings => Warnings.Count > 0;

    public bool ImportedAnything =>
        ImportedProjects > 0 || ImportedSettings || ImportedActivityEntries > 0;
}
