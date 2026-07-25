namespace AutoAIBuilder.Desktop.Services;

public interface IFilePickerService
{
    IReadOnlyList<string> PickProjectFiles();

    string? PickAutomationMaskContract();

    string? PickAutomationRuleCatalog();

    string? PickAutomationOutputDirectory(string? currentDirectory);

    string? PickDataBackupDestination(string suggestedFileName);

    string? PickDataBackupSource();

    string? PickDataDirectory(string currentDirectory);
}
