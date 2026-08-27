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
    /// <summary>Abre o diálogo nativo de seleção de pasta; retorna null se o usuário cancelar.</summary>
    string? PickFolder(string title);

    /// <summary>Exibe o diálogo de confirmação de exclusão; retorna true se o usuário confirmar.</summary>
    bool ConfirmDeletion(int fileCount, long totalBytesToFree);

    /// <summary>Exibe uma mensagem de erro simples ao usuário.</summary>
    void ShowError(string message);
}
