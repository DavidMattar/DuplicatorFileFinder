using System.Windows;

namespace DuplicatorFinder.App.Views;

/// <summary>
/// Janela não-modal com uma aba por cópia de um grupo de duplicados, cada uma abrindo uma
/// busca do Explorer pelo respectivo arquivo. Code-behind vazio de propósito — toda a lógica
/// vive no <see cref="ViewModels.OpenLocationsViewModel"/>.
/// </summary>
public partial class OpenLocationsWindow : Window
{
    public OpenLocationsWindow()
    {
        InitializeComponent();
    }
}
