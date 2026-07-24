namespace AutoAIBuilder.Desktop.Services;

public interface IDialogService
{
    bool ConfirmRemoveFileReference(string fileName);

    bool ConfirmRestoreDataBackup(string backupPath);

    bool ConfirmDataDirectoryChange(
        string currentDirectory,
        string newDirectory);
}
