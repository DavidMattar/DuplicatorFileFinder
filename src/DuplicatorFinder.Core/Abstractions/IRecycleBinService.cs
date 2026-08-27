using DuplicatorFinder.Core.Models;

namespace DuplicatorFinder.Core.Abstractions;

/// <summary>
/// Contrato para exclusão segura de arquivos. A implementação concreta (em
/// DuplicatorFinder.Infrastructure) usa a Lixeira do Windows por padrão, nunca apagando
/// nada de forma permanente sem uma decisão explícita do usuário na camada de UI.
/// </summary>
public interface IRecycleBinService
{
    /// <summary>
    /// Envia os arquivos informados para a Lixeira do Windows (exclusão recuperável).
    /// Continua tentando excluir os demais arquivos mesmo se algum falhar (ex: arquivo em
    /// uso por outro programa) — os detalhes de cada falha vêm em <see cref="DeleteResult.Failures"/>.
    /// </summary>
    Task<DeleteResult> SendToRecycleBinAsync(IEnumerable<string> paths);
}
