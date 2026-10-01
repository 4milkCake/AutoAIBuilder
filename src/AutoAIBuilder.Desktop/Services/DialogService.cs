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

    public bool ConfirmApplySemanticCorrection(
        string sourcePoint,
        int candidateCount)
    {
        var result = MessageBox.Show(
            $"Aplicar a classificação revisada de {sourcePoint} a "
            + $"{candidateCount} ponto(s) semelhante(s)?\n\n"
            + "Uma revisão individual será registrada para cada ponto. "
            + "O DWG e os CSVs não serão modificados, e a operação poderá "
            + "ser desfeita ponto a ponto.",
            "Confirmar correção por semelhança",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question,
            MessageBoxResult.No);

        return result == MessageBoxResult.Yes;
    }

    public bool ConfirmSupervisedAutomationExecution(
        string sourceFile,
        string historicalMask,
        string outputRoot,
        int expectedPoints)
    {
        var result = MessageBox.Show(
            "Iniciar a execução supervisionada 11.6H.1?\n\n"
            + $"Original protegido:\n{sourceFile}\n\n"
            + $"Referência histórica:\n{historicalMask}\n\n"
            + $"Destino isolado:\n{outputRoot}\n\n"
            + $"Pontos esperados: {expectedPoints}\n\n"
            + "O AutoCAD será executado somente sobre uma nova cópia técnica. "
            + "O modo de preservação total não apaga entidades, blocos, "
            + "layers ou referências. "
            + "O DWG original e a referência serão conferidos por SHA-256 "
            + "antes e depois da operação.",
            "Confirmar execução supervisionada",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning,
            MessageBoxResult.No);

        return result == MessageBoxResult.Yes;
    }
}
