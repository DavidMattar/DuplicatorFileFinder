using System.Windows;
using DuplicatorFinder.App.ViewModels;
using DuplicatorFinder.App.Views;
using Microsoft.Win32;

namespace DuplicatorFinder.App.Services;

/// <summary>
/// Implementação real de <see cref="IDialogService"/>, usando as APIs de diálogo nativas do
/// WPF/.NET. É a única classe do projeto que sabe que essas APIs concretas existem — todo o
/// resto do app fala apenas com a interface.
/// </summary>
public sealed class DialogService : IDialogService
{
    /// <inheritdoc />
    public string? PickFolder(string title)
    {
        // Microsoft.Win32.OpenFolderDialog é nativo do .NET 8 WPF — não precisa de nenhum
        // pacote NuGet adicional só para escolher uma pasta.
        var dialog = new OpenFolderDialog { Title = title };
        return dialog.ShowDialog() == true ? dialog.FolderName : null;
    }

    /// <inheritdoc />
    public bool ConfirmDeletion(int fileCount, long totalBytesToFree)
    {
        var viewModel = new DeleteConfirmationViewModel(fileCount, totalBytesToFree);
        var dialog = new DeleteConfirmationDialog
        {
            DataContext = viewModel,
            Owner = Application.Current.MainWindow,
        };

        return dialog.ShowDialog() == true;
    }

    /// <inheritdoc />
    public void ShowError(string message)
    {
        MessageBox.Show(message, "DuplicatorFinder", MessageBoxButton.OK, MessageBoxImage.Error);
    }
}
