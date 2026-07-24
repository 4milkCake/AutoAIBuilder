namespace AutoAIBuilder.Desktop.Services;

public interface IFilePickerService
{
    IReadOnlyList<string> PickProjectFiles();
}
