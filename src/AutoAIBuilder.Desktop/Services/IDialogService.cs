namespace AutoAIBuilder.Desktop.Services;

public interface IDialogService
{
    bool ConfirmRemoveFileReference(string fileName);

    bool ConfirmRestoreDataBackup(string backupPath);

    bool ConfirmDataDirectoryChange(
        string currentDirectory,
        string newDirectory);

    bool ConfirmVerifiedCopyExecution(
        string fileName,
        string outputRoot,
        string sha256);

    bool ConfirmMaskCatalogActivation(
        string maskName,
        string maskVersion,
        bool activate);

    bool ConfirmApplySemanticCorrection(
        string sourcePoint,
        int candidateCount) => false;

    bool ConfirmSupervisedAutomationExecution(
        string sourceFile,
        string historicalMask,
        string outputRoot,
        int expectedPoints) => false;
}
