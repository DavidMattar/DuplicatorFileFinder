namespace DuplicatorFinder.Core.Models;

/// <summary>
/// Identifica qual estratégia de detecção (<see cref="Abstractions.IDuplicateDetector"/>)
/// encontrou um determinado <see cref="DuplicateGroup"/>. É usado pela UI para escolher o
/// ícone/rótulo exibido e para decidir a estratégia de "smart select" mais adequada.
/// </summary>
public enum DuplicateKind
{
    /// <summary>Arquivos idênticos byte a byte (qualquer tipo de arquivo).</summary>
    ExactFile,

    /// <summary>Imagens visualmente parecidas, mas não necessariamente idênticas.</summary>
    SimilarImage,

    /// <summary>Vídeos com conteúdo visualmente parecido, mas não necessariamente idênticos.</summary>
    SimilarVideo,
}
