namespace DuplicatorFinder.Core.Models;

/// <summary>
/// Estende <see cref="FileEntry"/> com informações específicas de imagem, usadas pelo
/// detector de imagens similares (ImageSimilarityDetector).
/// </summary>
/// <param name="File">Metadados básicos do arquivo (caminho, tamanho, datas).</param>
/// <param name="Width">Largura da imagem em pixels.</param>
/// <param name="Height">Altura da imagem em pixels.</param>
/// <param name="PerceptualHash">
/// Hash perceptual de 64 bits da imagem (ex: pHash/dHash). Duas imagens visualmente
/// parecidas terão hashes com poucos bits diferentes entre si (baixa distância de Hamming),
/// mesmo que os arquivos não sejam idênticos byte a byte.
/// </param>
public sealed record ImageEntry(
    FileEntry File,
    int Width,
    int Height,
    ulong PerceptualHash);
