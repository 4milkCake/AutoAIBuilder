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
}
