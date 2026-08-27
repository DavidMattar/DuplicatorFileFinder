namespace DuplicatorFinder.Core.Abstractions;

/// <summary>
/// Contrato para cálculo e comparação de hash perceptual de imagens, usado tanto pelo
/// <see cref="Detection.ImageSimilarityDetector"/> quanto pelo
/// <see cref="Detection.VideoSimilarityDetector"/> (que aplica o mesmo algoritmo em frames
/// extraídos de vídeo).
/// </summary>
public interface IImageHasher
{
    /// <summary>
    /// Calcula o hash perceptual de 64 bits de uma imagem a partir do seu conteúdo decodificado.
    /// Diferente de um hash criptográfico, pequenas mudanças na imagem (recompressão,
    /// redimensionamento, leve ajuste de cor) resultam em hashes parecidos, não completamente diferentes.
    /// </summary>
    ulong ComputeHash(Stream imageStream);

    /// <summary>
    /// Calcula a distância de Hamming entre dois hashes perceptuais — ou seja, quantos bits
    /// diferem entre eles. Quanto menor o valor, mais parecidas as imagens são
    /// consideradas (0 = hashes idênticos).
    /// </summary>
    int HammingDistance(ulong hashA, ulong hashB);
}
