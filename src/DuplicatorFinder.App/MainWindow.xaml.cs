using System.Windows;
using DuplicatorFinder.App.ViewModels;

namespace DuplicatorFinder.App;

/// <summary>
/// Janela principal do aplicativo. Recebe o <see cref="MainViewModel"/> por injeção de
/// dependência (ver App.xaml.cs) em vez de criá-lo — o code-behind não conhece nenhuma
/// implementação concreta de serviço, só a fachada representada pelo ViewModel.
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
