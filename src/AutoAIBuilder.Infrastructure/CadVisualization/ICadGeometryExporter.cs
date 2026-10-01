namespace AutoAIBuilder.Infrastructure.CadVisualization;

public interface ICadGeometryExporter
{
    CadExporterStatus GetStatus();

    Task ExportAsync(
        string sourceCopyPath,
        string artifactPath,
        string workingDirectory,
        CancellationToken cancellationToken);
}

public sealed record CadExporterStatus(
    bool IsAvailable,
    string Name,
    string Version,
    string ExecutablePath,
    string Publisher,
    string Message);
