namespace AutoAIBuilder.Desktop.Services;

public interface IFilePickerService
{
    IReadOnlyList<string> PickProjectFiles();

    IReadOnlyList<string> PickSemanticCsvFiles() => [];

    string? PickRecognitionDwg(string? currentPath) => null;

    string? PickAutomationMaskContract();

    string? PickAutomationRuleCatalog();

    string? PickAutomationOutputDirectory(string? currentDirectory);

    string? PickAutoLispDirectory(string? currentDirectory) => null;

    string? PickSupervisedHistoricalMask(string? currentPath) => null;

    string? PickSupervisedSourceDwg(string? currentPath) => null;

    string? PickSupervisedCsv(string title, string? currentPath) => null;

    string? PickDataBackupDestination(string suggestedFileName);

    string? PickDataBackupSource();

    string? PickDataDirectory(string currentDirectory);
}
