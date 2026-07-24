namespace AutoAIBuilder.Desktop.Services;

public interface IFilePickerService
{
    IReadOnlyList<string> PickProjectFiles();

    string? PickDataBackupDestination(string suggestedFileName);

    string? PickDataBackupSource();

    string? PickDataDirectory(string currentDirectory);
}
