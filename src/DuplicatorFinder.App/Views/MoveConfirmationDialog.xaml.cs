using System.Windows;

namespace DuplicatorFinder.App.Views;

/// <summary>
/// Janela de confirmação exibida antes de qualquer movimentação real de cópias. Mesmo espírito
/// de <see cref="DeleteConfirmationDialog"/>: sem ViewModel com comandos, só retorna
/// verdadeiro/falso via <see cref="Window.DialogResult"/>.
/// </summary>
public partial class MoveConfirmationDialog : Window
{
    public MoveConfirmationDialog()
    {
        InitializeComponent();
    }

    private void OnConfirmClick(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
    }

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
