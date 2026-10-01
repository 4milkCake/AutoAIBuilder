using System.IO;
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

    public IReadOnlyList<string> PickSemanticCsvFiles()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Selecionar CSVs semânticos v07/v081",
            CheckFileExists = true,
            Multiselect = true,
            DefaultExt = ".csv",
            Filter = "Relatórios semânticos CSV|*.csv"
        };

        return dialog.ShowDialog() == true
            ? dialog.FileNames
            : [];
    }

    public string? PickRecognitionDwg(string? currentPath)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Selecionar novo DWG para reconhecimento supervisionado",
            CheckFileExists = true,
            Multiselect = false,
            DefaultExt = ".dwg",
            Filter = "Desenho AutoCAD|*.dwg",
            InitialDirectory = GetExistingDirectory(currentPath)
        };

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    public string? PickAutomationMaskContract()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Selecionar contrato JSON da máscara",
            CheckFileExists = true,
            Multiselect = false,
            DefaultExt = ".json",
            Filter = "Contrato de máscara JSON|*.json"
        };

        return dialog.ShowDialog() == true
            ? dialog.FileName
            : null;
    }

    public string? PickAutomationRuleCatalog()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Selecionar catálogo JSON de regras",
            CheckFileExists = true,
            Multiselect = false,
            DefaultExt = ".json",
            Filter = "Catálogo de regras JSON|*.json"
        };

        return dialog.ShowDialog() == true
            ? dialog.FileName
            : null;
    }

    public string? PickAutomationOutputDirectory(string? currentDirectory)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Selecionar pasta de saída da cópia técnica",
            Multiselect = false,
            InitialDirectory =
                !string.IsNullOrWhiteSpace(currentDirectory)
                && Directory.Exists(currentDirectory)
                    ? currentDirectory
                    : null
        };

        return dialog.ShowDialog() == true
            ? dialog.FolderName
            : null;
    }

    public string? PickAutoLispDirectory(string? currentDirectory)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Selecionar pasta das rotinas AutoLISP",
            Multiselect = false,
            InitialDirectory =
                !string.IsNullOrWhiteSpace(currentDirectory)
                && Directory.Exists(currentDirectory)
                    ? currentDirectory
                    : null
        };

        return dialog.ShowDialog() == true
            ? dialog.FolderName
            : null;
    }

    public string? PickSupervisedHistoricalMask(string? currentPath)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Selecionar máscara histórica validada",
            CheckFileExists = true,
            Multiselect = false,
            DefaultExt = ".dwg",
            Filter = "Desenho AutoCAD|*.dwg",
            InitialDirectory = GetExistingDirectory(currentPath)
        };

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    public string? PickSupervisedSourceDwg(string? currentPath)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Selecionar DWG original de entrada",
            CheckFileExists = true,
            Multiselect = false,
            DefaultExt = ".dwg",
            Filter = "Desenho AutoCAD|*.dwg",
            InitialDirectory = GetExistingDirectory(currentPath)
        };

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    public string? PickSupervisedCsv(string title, string? currentPath)
    {
        var dialog = new OpenFileDialog
        {
            Title = title,
            CheckFileExists = true,
            Multiselect = false,
            DefaultExt = ".csv",
            Filter = "Relatório CSV|*.csv",
            InitialDirectory = GetExistingDirectory(currentPath)
        };

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    public string? PickDataBackupDestination(string suggestedFileName)
    {
        var dialog = new SaveFileDialog
        {
            Title = "Criar backup dos dados do AutoAIBuilder",
            FileName = suggestedFileName,
            AddExtension = true,
            DefaultExt = ".aabbackup",
            Filter = "Backup do AutoAIBuilder|*.aabbackup|Banco SQLite|*.db|Todos os arquivos|*.*",
            OverwritePrompt = true
        };

        return dialog.ShowDialog() == true
            ? dialog.FileName
            : null;
    }

    public string? PickDataBackupSource()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Selecionar backup do AutoAIBuilder",
            CheckFileExists = true,
            Multiselect = false,
            Filter = "Backup do AutoAIBuilder|*.aabbackup;*.db|Todos os arquivos|*.*"
        };

        return dialog.ShowDialog() == true
            ? dialog.FileName
            : null;
    }

    public string? PickDataDirectory(string currentDirectory)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Selecionar nova pasta de dados do AutoAIBuilder",
            Multiselect = false,
            InitialDirectory = Directory.Exists(currentDirectory)
                ? currentDirectory
                : null
        };

        return dialog.ShowDialog() == true
            ? dialog.FolderName
            : null;
    }

    private static string? GetExistingDirectory(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        if (Directory.Exists(path))
        {
            return path;
        }

        var directory = Path.GetDirectoryName(path);
        return directory is not null && Directory.Exists(directory)
            ? directory
            : null;
    }
}
