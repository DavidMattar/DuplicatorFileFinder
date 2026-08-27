using System.Windows.Controls;

namespace DuplicatorFinder.App.Views;

/// <summary>
/// Code-behind da tela de configuração de escaneamento. Vazio de propósito: toda a lógica
/// vive no <see cref="ViewModels.ScanSetupViewModel"/> (padrão MVVM) — esta classe existe só
/// porque o WPF exige uma classe parcial para cada XAML.
/// </summary>
public partial class ScanSetupView : UserControl
{
    public ScanSetupView()
    {
        InitializeComponent();
    }
}
