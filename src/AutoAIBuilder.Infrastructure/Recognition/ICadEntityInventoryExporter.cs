namespace AutoAIBuilder.Infrastructure.Recognition;

public sealed record CadEntityInventoryExporterStatus(
    bool IsAvailable,
    string Name,
    string Version,
    string ExecutablePath,
    string Publisher,
    string Message);

public interface ICadEntityInventoryExporter
{
    CadEntityInventoryExporterStatus GetStatus();

    Task ExportAsync(
        string sourceCopyPath,
        string inventoryPath,
        string workingDirectory,
        CancellationToken cancellationToken);
}
