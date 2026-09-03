using System.IO;
using System.Windows;
using DuplicatorFinder.App.ViewModels;
using DuplicatorFinder.App.Views;
using DuplicatorFinder.Core.Models;
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
    public string? PickFolder(string title, string? initialDirectory = null)
    {
        // Microsoft.Win32.OpenFolderDialog é nativo do .NET 8 WPF — não precisa de nenhum
        // pacote NuGet adicional só para escolher uma pasta.
        var dialog = new OpenFolderDialog { Title = title };

        if (!string.IsNullOrWhiteSpace(initialDirectory) && Directory.Exists(initialDirectory))
        {
            dialog.InitialDirectory = initialDirectory;
        }

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
    public DuplicateMoveMode? PickMoveMode()
    {
        var viewModel = new MoveModeViewModel();
        var dialog = new MoveModeDialog
        {
            DataContext = viewModel,
            Owner = Application.Current.MainWindow,
        };

        return dialog.ShowDialog() == true ? viewModel.SelectedMode : null;
    }

    /// <inheritdoc />
    public bool ConfirmMove(int fileCount, long totalBytesToMove, string destinationFolder, DuplicateMoveMode mode)
    {
        var viewModel = new MoveConfirmationViewModel(fileCount, totalBytesToMove, destinationFolder, mode);
        var dialog = new MoveConfirmationDialog
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

    /// <inheritdoc />
    public void ShowPreview(IReadOnlyList<FileCandidateViewModel> files)
    {
        var window = new PreviewWindow
        {
            DataContext = new PreviewViewModel(files),
            Owner = Application.Current.MainWindow,
        };

        // Show() (não-modal): o usuário deve poder continuar interagindo com a tela de
        // resultados (ex: ajustar seleção) enquanto compara as imagens.
        window.Show();
    }

    /// <inheritdoc />
    public void OpenLocations(IReadOnlyList<FileCandidateViewModel> files)
    {
        var window = new OpenLocationsWindow
        {
            DataContext = new OpenLocationsViewModel(files),
            Owner = Application.Current.MainWindow,
        };

        window.Show();
    }
}
