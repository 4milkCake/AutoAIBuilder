using System.Windows;

namespace AutoAIBuilder.Desktop.Services;

public sealed class DialogService : IDialogService
{
    public bool ConfirmRemoveFileReference(string fileName)
    {
        var result = MessageBox.Show(
            $"Remover a referência de “{fileName}” do catálogo?\n\nO arquivo original não será excluído nem alterado.",
            "Remover referência",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning,
            MessageBoxResult.No);

        return result == MessageBoxResult.Yes;
    }

    public bool ConfirmRestoreDataBackup(string backupPath)
    {
        var result = MessageBox.Show(
            $"Restaurar os dados a partir de:\n{backupPath}\n\n"
            + "O estado atual será salvo automaticamente antes da restauração. "
            + "Projetos, configurações e histórico serão recarregados.",
            "Restaurar backup",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning,
            MessageBoxResult.No);

        return result == MessageBoxResult.Yes;
    }

    public bool ConfirmDataDirectoryChange(
        string currentDirectory,
        string newDirectory)
    {
        var result = MessageBox.Show(
            $"Copiar o banco atual para a nova pasta?\n\n"
            + $"Atual:\n{currentDirectory}\n\n"
            + $"Nova:\n{newDirectory}\n\n"
            + "A pasta atual será preservada. O novo local será usado somente "
            + "depois que você fechar e abrir o AutoAIBuilder.",
            "Alterar pasta de dados",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question,
            MessageBoxResult.No);

        return result == MessageBoxResult.Yes;
    }
}
