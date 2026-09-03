using DuplicatorFinder.Core.Models;

namespace DuplicatorFinder.Core.Abstractions;

/// <summary>
/// Contrato para mover grupos de duplicados para uma pasta de destino escolhida pelo usuário,
/// como alternativa a excluí-los. Cada operação de "mover selecionados" cria uma única pasta
/// numerada dentro do destino (<see cref="CreateBatchFolder"/>) e move cada grupo para dentro
/// dela (<see cref="MoveGroupAsync"/>): as cópias de cada grupo vão para uma subpasta própria,
/// nomeada a partir do arquivo que sobrevive naquele grupo, e esse arquivo sobrevivente vai
/// para a raiz da pasta numerada ou fica onde está, conforme o
/// <see cref="DuplicateMoveMode"/> escolhido pelo usuário.
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
    /// Move as cópias de um grupo (<paramref name="copiesToMove"/>) para uma subpasta "{nome do
    /// arquivo mantido, sem extensão} copies moved" dentro de <paramref name="batchFolder"/>, e
    /// — só quando <paramref name="moveKeptFile"/> é verdadeiro — move também o próprio arquivo
    /// mantido para a raiz de <paramref name="batchFolder"/>. Colisões de nome são resolvidas
    /// acrescentando um sufixo numérico, nunca sobrescrevendo um arquivo já movido. Continua
    /// tentando os demais arquivos mesmo se algum falhar — os detalhes de cada falha vêm em
    /// <see cref="MoveResult.Failures"/>.
    /// </summary>
    /// <param name="batchFolder">Pasta numerada já criada por <see cref="CreateBatchFolder"/>, comum a todos os grupos movidos na mesma operação.</param>
    /// <param name="keptFilePath">Caminho completo do arquivo que sobrevive no grupo — sempre usado para nomear a subpasta de cópias, mesmo quando ele não é movido.</param>
    /// <param name="copiesToMove">Caminhos completos das cópias a mover.</param>
    /// <param name="moveKeptFile">
    /// Verdadeiro em <see cref="DuplicateMoveMode.MoveEntireGroup"/> (o mantido sai do lugar
    /// junto com as cópias); falso em <see cref="DuplicateMoveMode.KeepHighestResolutionInPlace"/>
    /// (o mantido permanece exatamente onde está e só as cópias são movidas).
    /// </param>
    /// <param name="cancellationToken">Permite cancelar a movimentação em andamento.</param>
    Task<MoveResult> MoveGroupAsync(
        string batchFolder,
        string keptFilePath,
        IEnumerable<string> copiesToMove,
        bool moveKeptFile,
        CancellationToken cancellationToken);
}
