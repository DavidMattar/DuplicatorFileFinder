namespace DuplicatorFinder.Core.Models;

/// <summary>
/// Resultado de uma tentativa de exclusão em lote, retornado por
/// <see cref="Abstractions.IRecycleBinService"/>. Separa sucessos de falhas porque, em uma
/// exclusão de muitos arquivos, é comum que alguns falhem (arquivo em uso, sem permissão)
/// sem que isso deva impedir a exclusão dos demais.
/// </summary>
/// <param name="SucceededPaths">Caminhos que foram enviados com sucesso para a Lixeira.</param>
/// <param name="Failures">Caminhos que falharam, com a respectiva mensagem de erro.</param>
public sealed record DeleteResult(
    IReadOnlyList<string> SucceededPaths,
    IReadOnlyList<(string Path, string Error)> Failures);
