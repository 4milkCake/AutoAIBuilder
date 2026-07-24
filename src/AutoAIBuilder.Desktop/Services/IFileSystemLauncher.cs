namespace AutoAIBuilder.Desktop.Services;

public interface IFileSystemLauncher
{
    void OpenFile(string filePath);

    void RevealFile(string filePath);
}
