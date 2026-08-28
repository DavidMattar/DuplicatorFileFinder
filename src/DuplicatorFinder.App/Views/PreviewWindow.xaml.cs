using System.Windows;

namespace DuplicatorFinder.App.Views;

/// <summary>
/// Janela não-modal que exibe as imagens de um grupo de duplicados lado a lado. Code-behind
/// vazio de propósito — toda a lógica vive no <see cref="ViewModels.PreviewViewModel"/>.
/// </summary>
public partial class PreviewWindow : Window
{
    public PreviewWindow()
    {
        InitializeComponent();
    }
}
