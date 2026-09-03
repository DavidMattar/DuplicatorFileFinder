using DuplicatorFinder.App.Converters;
using DuplicatorFinder.Core.Models;

namespace DuplicatorFinder.App.ViewModels;

/// <summary>
/// Dados exibidos no diálogo de confirmação de movimentação (<see cref="Views.MoveConfirmationDialog"/>).
/// É um POCO simples (não implementa notificação de mudança) porque seus valores são
/// definidos uma única vez, na abertura do diálogo, e nunca mudam enquanto ele está na tela —
/// mesmo espírito de <see cref="DeleteConfirmationViewModel"/>.
/// </summary>
public sealed class MoveConfirmationViewModel
{
    /// <summary>Quantidade de arquivos que efetivamente sairão do lugar, já de acordo com o modo escolhido.</summary>
    public int FileCount { get; }

    /// <summary>Tamanho total dos arquivos a mover, já formatado para exibição (ex: "1,25 GB").</summary>
    public string TotalSizeDisplay { get; }

    /// <summary>Pasta escolhida pelo usuário no popup, onde a subpasta numerada "copias(x)" será criada.</summary>
    public string DestinationFolder { get; }

    /// <summary>
    /// Explicação do modo escolhido, reaproveitada de <see cref="MoveModeViewModel.Describe"/>
    /// para o texto ser literalmente o mesmo que o usuário acabou de ler ao escolher o modo.
    /// </summary>
    public string ModeExplanation { get; }

    public MoveConfirmationViewModel(int fileCount, long totalBytesToMove, string destinationFolder, DuplicateMoveMode mode)
    {
        FileCount = fileCount;
        TotalSizeDisplay = BytesToHumanReadableConverter.Format(totalBytesToMove);
        DestinationFolder = destinationFolder;
        ModeExplanation = MoveModeViewModel.Describe(mode);
    }
}
