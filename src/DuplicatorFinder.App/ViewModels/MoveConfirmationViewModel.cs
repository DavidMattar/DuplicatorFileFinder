using DuplicatorFinder.App.Converters;

namespace DuplicatorFinder.App.ViewModels;

/// <summary>
/// Dados exibidos no diálogo de confirmação de movimentação (<see cref="Views.MoveConfirmationDialog"/>).
/// É um POCO simples (não implementa notificação de mudança) porque seus valores são
/// definidos uma única vez, na abertura do diálogo, e nunca mudam enquanto ele está na tela —
/// mesmo espírito de <see cref="DeleteConfirmationViewModel"/>.
/// </summary>
public sealed class MoveConfirmationViewModel
{
    /// <summary>Quantidade de arquivos que serão movidos (arquivos mantidos + cópias de todos os grupos selecionados).</summary>
    public int FileCount { get; }

    /// <summary>Tamanho total dos arquivos a mover, já formatado para exibição (ex: "1,25 GB").</summary>
    public string TotalSizeDisplay { get; }

    /// <summary>Pasta escolhida pelo usuário no popup, onde a subpasta numerada "copias(x)" será criada.</summary>
    public string DestinationFolder { get; }

    public MoveConfirmationViewModel(int fileCount, long totalBytesToMove, string destinationFolder)
    {
        FileCount = fileCount;
        TotalSizeDisplay = BytesToHumanReadableConverter.Format(totalBytesToMove);
        DestinationFolder = destinationFolder;
    }
}
