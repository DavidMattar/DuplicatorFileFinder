namespace DuplicatorFinder.Core.Models;

/// <summary>
/// Representa um arquivo dentro de um <see cref="DuplicateGroup"/>, com o estado de decisão
/// sobre o que fazer com ele (manter como original ou marcar para exclusão).
/// É uma classe mutável (não um record) de propósito: a estratégia de smart-select e depois
/// o próprio usuário na UI vão alterar <see cref="IsKept"/>/<see cref="MarkedForDeletion"/>
/// depois que o arquivo já foi criado.
/// </summary>
public sealed class DuplicateFile
{
    /// <summary>Metadados do arquivo no disco.</summary>
    public required FileEntry File { get; init; }

    /// <summary>
    /// Largura em pixels, quando o arquivo é uma imagem ou vídeo (preenchido pelo
    /// <see cref="Detection.ImageSimilarityDetector"/>/<see cref="Detection.VideoSimilarityDetector"/>).
    /// Null para duplicados exatos de outros tipos de arquivo, onde resolução não se aplica.
    /// Existe aqui (em vez de só em <see cref="ImageEntry"/>/<see cref="VideoEntry"/>) para que
    /// <see cref="KeepStrategy.HighestResolution"/> funcione sem o smart-select precisar saber
    /// qual detector originou o grupo.
    /// </summary>
    public int? Width { get; init; }

    /// <summary>Altura em pixels — ver <see cref="Width"/>.</summary>
    public int? Height { get; init; }

    /// <summary>
    /// Verdadeiro quando este é o arquivo escolhido para permanecer (o "original").
    /// Definido inicialmente pela <see cref="Abstractions.ISmartSelectStrategy"/> e pode
    /// ser sobrescrito manualmente pelo usuário na tela de resultados.
    /// </summary>
    public bool IsKept { get; set; }

    /// <summary>
    /// Verdadeiro quando o usuário confirmou que este arquivo deve ser excluído.
    /// Nunca deve ser verdadeiro ao mesmo tempo que <see cref="IsKept"/>.
    /// </summary>
    public bool MarkedForDeletion { get; set; }

    /// <summary>
    /// Explicação legível de por que este arquivo foi marcado como original ou como cópia
    /// (ex: "Resolução mais alta do grupo", "Arquivo mais antigo"). Exibida na UI para dar
    /// transparência à decisão automática do smart-select.
    /// </summary>
    public string? Reason { get; set; }
}
