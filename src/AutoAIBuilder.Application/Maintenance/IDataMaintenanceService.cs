namespace AutoAIBuilder.Application.Maintenance;

public interface IDataMaintenanceService
{
    string DataDirectory { get; }

    string DatabasePath { get; }

    string BackupDirectory { get; }

    int SchemaVersion { get; }

    DataBackupResult CreateBackup(string destinationPath);

    DataRestoreResult RestoreBackup(string sourcePath);

    DataRelocationResult RelocateDataDirectory(string destinationDirectory);
}
