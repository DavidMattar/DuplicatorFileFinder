using System.IO.Abstractions;
using DuplicatorFinder.Core.Abstractions;
using DuplicatorFinder.Core.Detection.Support;
using DuplicatorFinder.Core.Models;
using SixLabors.ImageSharp;

namespace DuplicatorFinder.Core.Detection;

/// <summary>
/// Estratégia de detecção (Strategy, ver <see cref="IDuplicateDetector"/>) para imagens
/// visualmente parecidas — não apenas idênticas byte a byte. O pipeline é:
///   1) calcular o hash perceptual de cada imagem (<see cref="IImageHasher"/>);
///   2) comparar a distância de Hamming de cada par de hashes contra o threshold do usuário;
///   3) unir, via <see cref="UnionFind"/>, todos os pares dentro do threshold em grupos.
/// A comparação é O(n²) nos candidatos (todo par é comparado). Isso é intencional: uma
/// indexação por "bandas" (LSH) foi tentada primeiro, mas tem baixo recall aqui — a
/// diferença entre hashes de imagens parecidas (após redimensionar/recomprimir) tende a
/// ficar espalhada pelos 64 bits, então raramente duas imagens parecidas caem exatamente na
/// mesma banda, o que faria o detector perder duplicados reais silenciosamente. Como cada
/// comparação é só um XOR + contagem de bits (nanossegundos), o(n²) continua rápido o
/// suficiente para os tamanhos de biblioteca de fotos deste app (dezenas de milhares de
/// arquivos); só se tornaria um problema real em uma escala muito maior, e nesse ponto a
/// solução correta seria uma estrutura de índice vetorial aproximado (ANN) de verdade, não
/// um banding manual.
/// </summary>
public sealed class ImageSimilarityDetector : IDuplicateDetector
{
    /// <summary>Hashes perceptuais desta biblioteca têm 64 bits — usado para converter o threshold (0..1) em uma distância de Hamming máxima.</summary>
    private const int HashBits = 64;

    private readonly IImageHasher _imageHasher;
    private readonly IFileSystem _fileSystem;

    public ImageSimilarityDetector(IImageHasher imageHasher, IFileSystem fileSystem)
    {
        _imageHasher = imageHasher;
        _fileSystem = fileSystem;
    }

    /// <inheritdoc />
    public DuplicateKind Kind => DuplicateKind.SimilarImage;

    /// <inheritdoc />
    public async Task<IReadOnlyList<DuplicateGroup>> DetectAsync(
        IReadOnlyList<FileEntry> candidates,
        ScanOptions options,
        IProgress<ScanProgress>? progress,
        CancellationToken cancellationToken)
    {
        if (candidates.Count < 2)
        {
            return [];
        }

        var decoded = await DecodeAllAsync(candidates, options, progress, cancellationToken);

        // Imagens que não puderam ser decodificadas (corrompidas, formato não suportado)
        // simplesmente não participam da comparação — não têm hash para comparar.
        var validEntries = decoded.Where(entry => entry.Hash is not null).ToList();

        var maxHammingDistance = ThresholdToMaxHammingDistance(options.ImageSimilarityThreshold);

        var unionFind = new UnionFind(validEntries.Count);
        for (var a = 0; a < validEntries.Count; a++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            for (var b = a + 1; b < validEntries.Count; b++)
            {
                var distance = _imageHasher.HammingDistance(validEntries[a].Hash!.Value, validEntries[b].Hash!.Value);
                if (distance <= maxHammingDistance)
                {
                    unionFind.Union(a, b);
                }
            }
        }

        return BuildGroups(unionFind, validEntries);
    }

    /// <summary>
    /// Decodifica todas as imagens candidatas em paralelo, lendo os metadados (resolução) de
    /// forma barata via <see cref="Image.IdentifyAsync(Stream, CancellationToken)"/> (sem decodificar todos os pixels)
    /// e calculando o hash perceptual completo separadamente.
    /// </summary>
    private async Task<List<DecodedImage>> DecodeAllAsync(
        IReadOnlyList<FileEntry> candidates,
        ScanOptions options,
        IProgress<ScanProgress>? progress,
        CancellationToken cancellationToken)
    {
        var results = new DecodedImage[candidates.Count];
        long processed = 0;

        var parallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = Math.Max(1, options.MaxDegreeOfParallelism),
            CancellationToken = cancellationToken,
        };

        await Parallel.ForEachAsync(Enumerable.Range(0, candidates.Count), parallelOptions, async (i, ct) =>
        {
            var file = candidates[i];
            results[i] = await DecodeOneAsync(file, ct);

            var count = Interlocked.Increment(ref processed);
            progress?.Report(new ScanProgress("Comparando imagens", count, candidates.Count, file.FullPath, GroupsFoundSoFar: 0));
        });

        return results.ToList();
    }

    private async Task<DecodedImage> DecodeOneAsync(FileEntry file, CancellationToken cancellationToken)
    {
        try
        {
            int width;
            int height;

            await using (var identifyStream = _fileSystem.File.OpenRead(file.FullPath))
            {
                var info = await Image.IdentifyAsync(identifyStream, cancellationToken);
                width = info.Width;
                height = info.Height;
            }

            await using var hashStream = _fileSystem.File.OpenRead(file.FullPath);
            var hash = _imageHasher.ComputeHash(hashStream);

            return new DecodedImage(file, width, height, hash);
        }
        catch (Exception)
        {
            // Deliberadamente amplo: bibliotecas de imagem lançam vários tipos de exceção
            // diferentes para "isto não é uma imagem válida" (formato desconhecido, arquivo
            // corrompido, EOF inesperado). Nenhum desses casos deve interromper o
            // escaneamento inteiro — só significa que este arquivo não entra na comparação.
            return new DecodedImage(file, null, null, null);
        }
    }

    /// <summary>Converte o threshold de similaridade (0.0 tolerante .. 1.0 exigente) na distância de Hamming máxima aceitável entre dois hashes de 64 bits.</summary>
    private static int ThresholdToMaxHammingDistance(double threshold)
    {
        var tolerance = 1.0 - Math.Clamp(threshold, 0.0, 1.0);
        return (int)Math.Round(tolerance * HashBits);
    }

    private List<DuplicateGroup> BuildGroups(UnionFind unionFind, List<DecodedImage> validEntries)
    {
        var groups = new List<DuplicateGroup>();

        foreach (var component in unionFind.GetComponents())
        {
            if (component.Count < 2)
            {
                continue;
            }

            var members = component.Select(i => validEntries[i]).ToList();

            groups.Add(new DuplicateGroup
            {
                Kind = DuplicateKind.SimilarImage,
                SimilarityScore = ComputeGroupSimilarityScore(members),
                Files = members.Select(m => new DuplicateFile
                {
                    File = m.File,
                    Width = m.Width,
                    Height = m.Height,
                }).ToList(),
            });
        }

        return groups;
    }

    /// <summary>Similaridade média do grupo (0..1), calculada a partir da distância de Hamming média entre todos os pares do grupo.</summary>
    private double ComputeGroupSimilarityScore(List<DecodedImage> members)
    {
        if (members.Count < 2)
        {
            return 1.0;
        }

        double totalDistance = 0;
        var pairCount = 0;

        for (var i = 0; i < members.Count; i++)
        {
            for (var j = i + 1; j < members.Count; j++)
            {
                totalDistance += _imageHasher.HammingDistance(members[i].Hash!.Value, members[j].Hash!.Value);
                pairCount++;
            }
        }

        var averageDistance = totalDistance / pairCount;
        return 1.0 - (averageDistance / HashBits);
    }

    /// <summary>Resultado da decodificação de uma imagem candidata: metadados + hash perceptual (null se a decodificação falhou).</summary>
    private sealed record DecodedImage(FileEntry File, int? Width, int? Height, ulong? Hash);
}
