using DuplicatorFinder.App.ViewModels;

namespace DuplicatorFinder.App.Services;

/// <summary>
/// Abstrai qualquer interação com o usuário que dependa de uma API de UI concreta (diálogo
/// nativo de pasta, janela de confirmação, caixa de mensagem). Sem esta interface, os
/// ViewModels precisariam referenciar classes de janela diretamente, o que os tornaria
/// impossíveis de testar sem abrir uma janela real — por isso ela existe como um contrato
/// separado, seguindo o mesmo espírito das interfaces do Core.
/// </summary>
public interface IDialogService
{
    /// <summary>
    /// Abre o diálogo nativo de seleção de pasta; retorna null se o usuário cancelar.
    /// </summary>
    /// <param name="title">Título exibido no diálogo.</param>
    /// <param name="initialDirectory">Pasta pré-selecionada ao abrir, se ainda existir em disco; null para o padrão do sistema.</param>
    string? PickFolder(string title, string? initialDirectory = null);

    /// <summary>Exibe o diálogo de confirmação de exclusão; retorna true se o usuário confirmar.</summary>
    bool ConfirmDeletion(int fileCount, long totalBytesToFree);

    /// <summary>Exibe o diálogo de confirmação de movimentação de cópias; retorna true se o usuário confirmar.</summary>
    bool ConfirmMove(int fileCount, long totalBytesToMove, string destinationFolder);

    /// <summary>Exibe uma mensagem de erro simples ao usuário.</summary>
    void ShowError(string message);

    /// <summary>
    /// Abre uma janela não-modal mostrando as imagens de <paramref name="files"/> lado a lado,
    /// em tamanho maior que a miniatura da lista de resultados — para o usuário comparar
    /// visualmente por que um grupo foi considerado similar antes de decidir o que excluir.
    /// Arquivos que não são imagens (ex: vídeos de um grupo de duplicados exatos) são
    /// filtrados pelo chamador antes de chegar aqui; ver <see cref="ViewModels.DuplicateGroupViewModel.HasPreviewableImages"/>.
    /// </summary>
    void ShowPreview(IReadOnlyList<FileCandidateViewModel> files);

    /// <summary>
    /// Abre uma janela não-modal com uma aba por arquivo de <paramref name="files"/>; cada aba
    /// já dispara uma busca do Explorer do Windows pelo nome daquele arquivo, na pasta onde
    /// ele está — para o usuário localizar rapidamente cada cópia no disco sem navegar manualmente.
    /// </summary>
    void OpenLocations(IReadOnlyList<FileCandidateViewModel> files);
}
