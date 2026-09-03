using System.Windows;

namespace DuplicatorFinder.App.Views;

/// <summary>
/// Janela que pergunta qual dos modos de movimentação usar, antes de escolher a pasta de
/// destino. Mesmo espírito de <see cref="MoveConfirmationDialog"/>: sem comandos no ViewModel,
/// só devolve verdadeiro/falso via <see cref="Window.DialogResult"/> — quem chamou lê o modo
/// escolhido no <see cref="ViewModels.MoveModeViewModel"/> que passou como DataContext.
/// </summary>
public partial class MoveModeDialog : Window
{
    public MoveModeDialog()
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
