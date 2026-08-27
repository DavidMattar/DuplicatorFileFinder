using DuplicatorFinder.Core.Models;

namespace DuplicatorFinder.Core.Abstractions;

/// <summary>
/// Contrato comum a todas as estratégias de detecção de duplicados (padrão Strategy).
/// Cada implementação (<see cref="Detection.ExactHashDetector"/>,
/// <see cref="Detection.ImageSimilarityDetector"/>, <see cref="Detection.VideoSimilarityDetector"/>)
/// sabe comparar um tipo específico de arquivo; o <see cref="Engine.DuplicateScanEngine"/>
/// apenas invoca todos os detectores habilitados sem precisar saber como cada um funciona.
/// Isso permite adicionar um novo tipo de detecção no futuro (ex: documentos de texto
/// similares) sem alterar o motor de orquestração.
/// </summary>
public interface IDuplicateDetector
{
    /// <summary>Tipo de duplicado que este detector é capaz de encontrar.</summary>
    DuplicateKind Kind { get; }

    /// <summary>
    /// Analisa os arquivos candidatos e retorna os grupos de duplicados encontrados.
    /// Recebe a lista completa de candidatos (não um a um) porque a maioria dos algoritmos
    /// de agrupamento (por tamanho, por hash, por bucket de similaridade) precisa ver todos
    /// os itens de uma vez para formar os grupos corretamente.
    /// </summary>
    /// <param name="candidates">Arquivos já filtrados pelo tipo relevante para este detector (ex: só imagens).</param>
    /// <param name="options">Opções do escaneamento, incluindo thresholds de similaridade.</param>
    /// <param name="progress">Callback opcional para reportar progresso à UI.</param>
    /// <param name="cancellationToken">Permite cancelar a detecção em andamento.</param>
    Task<IReadOnlyList<DuplicateGroup>> DetectAsync(
        IReadOnlyList<FileEntry> candidates,
        ScanOptions options,
        IProgress<ScanProgress>? progress,
        CancellationToken cancellationToken);
}
