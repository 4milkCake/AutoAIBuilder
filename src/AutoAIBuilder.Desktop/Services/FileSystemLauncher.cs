using System.Diagnostics;

namespace AutoAIBuilder.Desktop.Services;

public sealed class FileSystemLauncher : IFileSystemLauncher
{
    public void OpenFile(string filePath)
    {
        Process.Start(new ProcessStartInfo(filePath)
        {
            UseShellExecute = true
        });
    }

    public void RevealFile(string filePath)
    {
        Process.Start(new ProcessStartInfo(
            "explorer.exe",
            $"/select,\"{filePath}\"")
        {
            UseShellExecute = true
        });
    }
}
