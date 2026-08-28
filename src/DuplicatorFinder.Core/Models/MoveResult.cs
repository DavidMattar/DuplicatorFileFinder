namespace DuplicatorFinder.Core.Models;

/// <summary>
/// Resultado de uma tentativa de mover cópias de um grupo de duplicados para a pasta
/// "copies moved", retornado por <see cref="Abstractions.IDuplicateMoveService"/>. Separa
/// sucessos de falhas pela mesma razão de <see cref="DeleteResult"/>: é comum que um arquivo
/// falhe (em uso, sem permissão) sem que isso deva impedir a movimentação dos demais.
/// </summary>
/// <param name="SucceededPaths">Caminhos originais dos arquivos movidos com sucesso.</param>
/// <param name="Failures">Caminhos originais que falharam, com a respectiva mensagem de erro.</param>
public sealed record MoveResult(
    IReadOnlyList<string> SucceededPaths,
    IReadOnlyList<(string Path, string Error)> Failures);
