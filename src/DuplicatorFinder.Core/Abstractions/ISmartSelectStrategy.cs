using DuplicatorFinder.Core.Models;

namespace DuplicatorFinder.Core.Abstractions;

/// <summary>
/// Contrato (padrão Strategy) para a lógica que decide, dentro de um grupo de duplicados,
/// qual arquivo marcar como "mantido" e quais marcar como cópias a excluir.
/// Existir como interface permite no futuro oferecer outras estratégias sem alterar o
/// restante do pipeline (ex: "sempre pedir confirmação manual", "priorizar pasta de backup").
/// </summary>
public interface ISmartSelectStrategy
{
    /// <summary>
    /// Aplica a estratégia de seleção diretamente sobre os itens de <paramref name="group"/>:
    /// define <see cref="DuplicateFile.IsKept"/> e <see cref="DuplicateFile.MarkedForDeletion"/>
    /// de cada arquivo do grupo, e preenche <see cref="DuplicateFile.Reason"/> com uma
    /// explicação legível da decisão.
    /// </summary>
    void Apply(DuplicateGroup group, SmartSelectOptions options);
}
