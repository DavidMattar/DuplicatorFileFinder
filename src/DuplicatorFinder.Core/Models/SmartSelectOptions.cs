namespace DuplicatorFinder.Core.Models;

/// <summary>
/// Critérios possíveis para decidir automaticamente qual arquivo de um grupo de duplicados
/// deve ser mantido como "original". Usado por <see cref="Abstractions.ISmartSelectStrategy"/>.
/// </summary>
public enum KeepStrategy
{
    /// <summary>Mantém o arquivo com a data de criação mais antiga.</summary>
    OldestFile,

    /// <summary>Mantém o arquivo com a data de criação mais recente.</summary>
    NewestFile,

    /// <summary>Mantém o arquivo cujo caminho completo é o mais curto (geralmente o mais "organizado").</summary>
    ShortestPath,

    /// <summary>Mantém a imagem/vídeo de maior resolução (largura x altura).</summary>
    HighestResolution,

    /// <summary>Mantém o arquivo de maior tamanho em bytes.</summary>
    LargestFile,

    /// <summary>Mantém o arquivo de menor tamanho em bytes.</summary>
    SmallestFile,

    /// <summary>Mantém o arquivo que estiver dentro de <see cref="SmartSelectOptions.PreferredFolderPath"/>, se houver algum.</summary>
    PreferFolder,
}

/// <summary>
/// Configuração usada pela <see cref="Abstractions.ISmartSelectStrategy"/> para decidir,
/// dentro de cada <see cref="DuplicateGroup"/>, qual arquivo marcar como mantido.
/// </summary>
public sealed class SmartSelectOptions
{
    /// <summary>Critério principal de decisão.</summary>
    public KeepStrategy Primary { get; init; } = KeepStrategy.OldestFile;

    /// <summary>
    /// Pasta preferida para manter arquivos, usada apenas quando <see cref="Primary"/> é
    /// <see cref="KeepStrategy.PreferFolder"/>, ou como critério de desempate antes do
    /// critério primário quando definida.
    /// </summary>
    public string? PreferredFolderPath { get; init; }
}
