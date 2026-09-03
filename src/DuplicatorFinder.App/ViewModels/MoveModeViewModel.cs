using CommunityToolkit.Mvvm.ComponentModel;
using DuplicatorFinder.Core.Models;

namespace DuplicatorFinder.App.ViewModels;

/// <summary>
/// ViewModel do diálogo que pergunta como mover os arquivos selecionados
/// (<see cref="Views.MoveModeDialog"/>). Diferente de
/// <see cref="MoveConfirmationViewModel"/>/<see cref="DeleteConfirmationViewModel"/>, este
/// precisa de notificação de mudança: o usuário troca a opção com os RadioButtons enquanto o
/// diálogo está aberto, e o texto explicativo abaixo deles acompanha a escolha.
/// </summary>
public sealed partial class MoveModeViewModel : ObservableObject
{
    /// <summary>
    /// Verdadeiro quando a opção "mover o grupo inteiro" está selecionada. É o padrão porque
    /// preserva o comportamento que a ação "Mover selecionados" já tinha antes de o segundo
    /// modo existir — quem já usava o botão continua tendo o mesmo resultado sem mudar nada.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedMode))]
    [NotifyPropertyChangedFor(nameof(ModeExplanation))]
    private bool _isMoveEntireGroupSelected = true;

    /// <summary>
    /// Verdadeiro quando a opção "manter o de maior resolução no lugar" está selecionada.
    /// Os dois RadioButtons ficam no mesmo grupo no XAML, então esta e
    /// <see cref="IsMoveEntireGroupSelected"/> nunca são verdadeiras ao mesmo tempo.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedMode))]
    [NotifyPropertyChangedFor(nameof(ModeExplanation))]
    private bool _isKeepHighestResolutionSelected;

    /// <summary>Modo correspondente à opção marcada — o que o diálogo devolve ao chamador.</summary>
    public DuplicateMoveMode SelectedMode => IsKeepHighestResolutionSelected
        ? DuplicateMoveMode.KeepHighestResolutionInPlace
        : DuplicateMoveMode.MoveEntireGroup;

    /// <summary>
    /// Descrição do que vai acontecer com a opção marcada, exibida no diálogo para o usuário
    /// entender a diferença entre os modos sem precisar testar cada um.
    /// </summary>
    public string ModeExplanation => Describe(SelectedMode);

    /// <summary>
    /// Texto que explica um modo em uma frase. Fica como método estático público para o
    /// diálogo de confirmação (<see cref="MoveConfirmationViewModel"/>) repetir exatamente a
    /// mesma explicação, em vez de manter duas redações que podem divergir com o tempo.
    /// </summary>
    public static string Describe(DuplicateMoveMode mode) => mode switch
    {
        DuplicateMoveMode.KeepHighestResolutionInPlace =>
            "De cada grupo com arquivos marcados, só o de maior resolução fica onde está. Todas as outras cópias "
            + "daquele grupo — inclusive as que estiverem desmarcadas na lista — vão para uma pasta própria do grupo, "
            + "dentro de uma única pasta \"copias(x)\" criada no destino escolhido.",

        _ =>
            "O grupo inteiro sai do lugar: o arquivo mantido vai para a raiz de uma única pasta \"copias(x)\" no destino "
            + "escolhido, e suas cópias marcadas para uma pasta própria ao lado dele. Arquivos desmarcados não saem do lugar.",
    };
}
