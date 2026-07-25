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

    public bool ConfirmVerifiedCopyExecution(
        string fileName,
        string outputRoot,
        string sha256)
    {
        var result = MessageBox.Show(
            "Executar o piloto “Cópia técnica verificada”?\n\n"
            + $"Entrada catalogada:\n{fileName}\n\n"
            + $"Destino:\n{outputRoot}\n\n"
            + $"SHA-256 simulado:\n{sha256}\n\n"
            + "O original não será editado. A execução trabalhará sobre uma "
            + "cópia isolada e publicará uma nova pasta com manifesto.",
            "Confirmar execução sobre cópia",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question,
            MessageBoxResult.No);

        return result == MessageBoxResult.Yes;
    }

    public bool ConfirmMaskCatalogActivation(
        string maskName,
        string maskVersion,
        bool activate)
    {
        var action = activate ? "Ativar" : "Desativar";
        var consequence = activate
            ? "Outras versões ativas da mesma máscara serão desativadas. "
              + "Esta ação apenas seleciona a versão para integração futura; "
              + "nenhuma automação será executada."
            : "A máscara permanecerá instalada no catálogo e poderá ser "
              + "reativada. Nenhuma automação será executada.";
        var result = MessageBox.Show(
            $"{action} “{maskName}” versão {maskVersion}?\n\n{consequence}",
            $"{action} máscara no catálogo",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question,
            MessageBoxResult.No);

        return result == MessageBoxResult.Yes;
    }
}
