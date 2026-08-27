using DuplicatorFinder.Core.Abstractions;
using DuplicatorFinder.Core.Models;

namespace DuplicatorFinder.Core.SmartSelect;

/// <summary>
/// Implementação padrão (e única, na v1) de <see cref="ISmartSelectStrategy"/>: decide qual
/// arquivo de um grupo de duplicados mantém marcado como "original" e marca todos os demais
/// para exclusão, sem exigir nenhuma ação manual do usuário — que ainda pode sobrescrever a
/// escolha na tela de resultados antes de confirmar a exclusão.
/// </summary>
public sealed class DefaultSmartSelectStrategy : ISmartSelectStrategy
{
    /// <inheritdoc />
    public void Apply(DuplicateGroup group, SmartSelectOptions options)
    {
        if (group.Files.Count == 0)
        {
            return;
        }

        var kept = ChooseFileToKeep(group, options);

        foreach (var file in group.Files)
        {
            var isKept = ReferenceEquals(file, kept);
            file.IsKept = isKept;
            file.MarkedForDeletion = !isKept;
            file.Reason = isKept ? DescribeKeepReason(options) : "Cópia duplicada detectada";
        }
    }

    /// <summary>
    /// Escolhe qual arquivo do grupo será mantido, seguindo a ordem de prioridade:
    /// 1) pasta preferida (se configurada e presente no grupo);
    /// 2) critério primário configurado em <see cref="SmartSelectOptions.Primary"/>;
    /// 3) desempate determinístico pelo caminho completo (ordem alfabética), para que o
    ///    resultado nunca dependa da ordem "por acaso" em que os arquivos foram enumerados.
    /// </summary>
    private static DuplicateFile ChooseFileToKeep(DuplicateGroup group, SmartSelectOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.PreferredFolderPath))
        {
            var preferred = group.Files
                .Where(f => f.File.FullPath.StartsWith(options.PreferredFolderPath, StringComparison.OrdinalIgnoreCase))
                .OrderBy(f => f.File.FullPath, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();

            if (preferred is not null)
            {
                return preferred;
            }
        }

        return group.Files
            .OrderBy(f => RankByPrimaryStrategy(f, options.Primary), Comparer<object>.Default)
            .ThenBy(f => f.File.FullPath, StringComparer.OrdinalIgnoreCase)
            .First();
    }

    /// <summary>
    /// Calcula uma chave de ordenação para <paramref name="file"/> de acordo com o critério
    /// primário: o menor valor retornado é o mais prioritário para ser mantido. Cada branch
    /// do switch retorna sempre o mesmo tipo de valor entre chamadas (todas <see cref="DateTime"/>,
    /// todas <see cref="long"/>, etc.), o que é o que permite comparar os valores via
    /// <see cref="Comparer{Object}.Default"/> com segurança em tempo de execução.
    /// </summary>
    private static object RankByPrimaryStrategy(DuplicateFile file, KeepStrategy strategy) => strategy switch
    {
        KeepStrategy.OldestFile => file.File.CreatedUtc,
        KeepStrategy.NewestFile => DateTime.MaxValue - file.File.CreatedUtc,
        KeepStrategy.ShortestPath => (long)file.File.FullPath.Length,
        KeepStrategy.LargestFile => -file.File.SizeBytes,
        KeepStrategy.SmallestFile => file.File.SizeBytes,
        KeepStrategy.HighestResolution => -(long)(file.Width ?? 0) * (file.Height ?? 0),

        // PreferFolder já foi tratado antes de chegar aqui; se nenhum arquivo estava na pasta
        // preferida (ou ela não foi configurada), todos empatam e o desempate por caminho decide.
        KeepStrategy.PreferFolder => 0L,

        _ => 0L,
    };

    private static string DescribeKeepReason(SmartSelectOptions options) => options.Primary switch
    {
        KeepStrategy.OldestFile => "Arquivo mais antigo do grupo",
        KeepStrategy.NewestFile => "Arquivo mais recente do grupo",
        KeepStrategy.ShortestPath => "Caminho mais curto do grupo",
        KeepStrategy.HighestResolution => "Maior resolução do grupo",
        KeepStrategy.LargestFile => "Maior arquivo do grupo",
        KeepStrategy.SmallestFile => "Menor arquivo do grupo",
        KeepStrategy.PreferFolder => "Está na pasta preferida",
        _ => "Selecionado para manter",
    };
}
