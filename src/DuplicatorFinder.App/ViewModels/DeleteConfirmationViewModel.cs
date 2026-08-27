using DuplicatorFinder.App.Converters;

namespace DuplicatorFinder.App.ViewModels;

/// <summary>
/// Dados exibidos no diálogo de confirmação de exclusão (<see cref="Views.DeleteConfirmationDialog"/>).
/// É um POCO simples (não implementa notificação de mudança) porque seus valores são
/// definidos uma única vez, na abertura do diálogo, e nunca mudam enquanto ele está na tela.
/// </summary>
public sealed class DeleteConfirmationViewModel
{
    /// <summary>Quantidade de arquivos que serão enviados para a Lixeira.</summary>
    public int FileCount { get; }

    /// <summary>Espaço em disco que será liberado, já formatado para exibição (ex: "1,25 GB").</summary>
    public string TotalSizeDisplay { get; }

    public DeleteConfirmationViewModel(int fileCount, long totalBytesToFree)
    {
        FileCount = fileCount;
        TotalSizeDisplay = BytesToHumanReadableConverter.Format(totalBytesToFree);
    }
}
