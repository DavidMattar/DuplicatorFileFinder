using DuplicatorFinder.Core.Models;

namespace DuplicatorFinder.Core.Abstractions;

/// <summary>
/// Contrato para mover grupos de duplicados inteiros (o arquivo mantido e suas cópias) para
/// uma pasta de destino escolhida pelo usuário, como alternativa a excluí-los. Cada operação
/// de "mover selecionados" cria uma única pasta numerada dentro do destino
/// (<see cref="CreateBatchFolder"/>) e move cada grupo para dentro dela
/// (<see cref="MoveGroupAsync"/>): o arquivo mantido fica direto na pasta numerada, com uma
/// subpasta ao lado contendo as cópias daquele grupo.
/// </summary>
public interface IDuplicateMoveService
{
    /// <summary>
    /// Cria, dentro de <paramref name="destinationRoot"/>, uma subpasta ainda não usada
    /// chamada "copias(x)" — x é o menor número inteiro positivo (a partir de 1) para o qual
    /// essa subpasta não existe ainda — e retorna o caminho completo dela. Chamado uma única
    /// vez por operação de "mover selecionados", antes de mover qualquer arquivo, para que
    /// todos os grupos movidos na mesma operação caiam juntos na mesma pasta numerada.
    /// </summary>
    string CreateBatchFolder(string destinationRoot);

    /// <summary>
    /// Move o arquivo mantido de um grupo direto para dentro de <paramref name="batchFolder"/>,
    /// e suas cópias (<paramref name="copiesToMove"/>) para uma subpasta "{nome do arquivo
    /// mantido, sem extensão} copies moved" criada ao lado dele, dentro do mesmo
    /// <paramref name="batchFolder"/>. Colisões de nome são resolvidas acrescentando um sufixo
    /// numérico, nunca sobrescrevendo um arquivo já movido. Continua tentando os demais
    /// arquivos mesmo se algum falhar — os detalhes de cada falha vêm em <see cref="MoveResult.Failures"/>.
    /// </summary>
    /// <param name="batchFolder">Pasta numerada já criada por <see cref="CreateBatchFolder"/>, comum a todos os grupos movidos na mesma operação.</param>
    /// <param name="keptFilePath">Caminho completo do arquivo mantido do grupo.</param>
    /// <param name="copiesToMove">Caminhos completos das cópias a mover.</param>
    /// <param name="cancellationToken">Permite cancelar a movimentação em andamento.</param>
    Task<MoveResult> MoveGroupAsync(
        string batchFolder,
        string keptFilePath,
        IEnumerable<string> copiesToMove,
        CancellationToken cancellationToken);
}
