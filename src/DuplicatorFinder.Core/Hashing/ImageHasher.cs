using System.Numerics;
using CoenM.ImageHash;
using CoenM.ImageHash.HashAlgorithms;
using DuplicatorFinder.Core.Abstractions;

namespace DuplicatorFinder.Core.Hashing;

/// <summary>
/// Implementação de <see cref="IImageHasher"/> usando o algoritmo PerceptualHash da
/// biblioteca CoenM.ImageHash (que decodifica a imagem via SixLabors.ImageSharp
/// internamente). Diferente de um hash criptográfico, pequenas mudanças na imagem
/// (recompressão, redimensionamento, ajuste leve de cor) resultam em hashes parecidos —
/// exatamente a propriedade necessária para detectar imagens "quase iguais".
/// </summary>
public sealed class ImageHasher : IImageHasher
{
    /// <summary>
    /// PerceptualHash foi escolhido entre os três algoritmos da biblioteca (Average,
    /// Difference, Perceptual) por ser o mais robusto a recompressão JPEG e pequenas
    /// variações de cor, ao custo de ser um pouco mais lento que os outros dois.
    /// </summary>
    private readonly IImageHash _algorithm = new PerceptualHash();

    /// <inheritdoc />
    public ulong ComputeHash(Stream imageStream) => _algorithm.Hash(imageStream);

    /// <inheritdoc />
    public int HammingDistance(ulong hashA, ulong hashB) => BitOperations.PopCount(hashA ^ hashB);
}
