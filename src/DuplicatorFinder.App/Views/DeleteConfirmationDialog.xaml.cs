using System.Windows;

namespace DuplicatorFinder.App.Views;

/// <summary>
/// Janela de confirmação exibida antes de qualquer exclusão real. Não tem ViewModel com
/// comandos (usa event handlers simples) porque seu único propósito é retornar
/// verdadeiro/falso via <see cref="Window.DialogResult"/> — não há estado nem lógica de
/// negócio aqui que justifique MVVM completo.
/// </summary>
public partial class DeleteConfirmationDialog : Window
{
    public DeleteConfirmationDialog()
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
