using Microsoft.Win32;

namespace AutoAIBuilder.Desktop.Services;

public sealed class FilePickerService : IFilePickerService
{
    public IReadOnlyList<string> PickProjectFiles()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Selecionar arquivos do projeto",
            CheckFileExists = true,
            Multiselect = true,
            Filter = "Arquivos de projeto|*.dwg;*.dxf;*.ifc;*.pdf;*.doc;*.docx;*.png;*.jpg;*.jpeg;*.bmp;*.tif;*.tiff;*.xls;*.xlsx;*.csv|Todos os arquivos|*.*"
        };

        return dialog.ShowDialog() == true
            ? dialog.FileNames
            : [];
    }
}
