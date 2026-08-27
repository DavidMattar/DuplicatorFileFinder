using DuplicatorFinder.Core.Abstractions;
using DuplicatorFinder.Core.Detection.Support;
using DuplicatorFinder.Core.Models;

namespace DuplicatorFinder.Core.Detection;

/// <summary>
/// Estratégia de detecção (Strategy, ver <see cref="IDuplicateDetector"/>) para vídeos com
/// conteúdo visualmente parecido — não apenas idênticos byte a byte. O pipeline é:
///   1) ler metadados (duração/resolução) de todos os candidatos — barato, via ffprobe;
///   2) pré-filtrar pares por duração parecida (só vídeos com duração próxima podem ser o
///      mesmo conteúdo) — evita a etapa cara a seguir para a maioria dos vídeos "solteiros";
///   3) extrair alguns frames-chave (10%/50%/90% da duração) só dos vídeos que sobreviveram
///      ao pré-filtro, e calcular o hash perceptual de cada frame (reaproveitando
///      <see cref="IImageHasher"/>, o mesmo usado para imagens);
///   4) comparar a distância média de Hamming entre frames correspondentes de cada par e
///      unir, via <see cref="UnionFind"/>, os pares dentro do threshold do usuário.
/// Extração de frame é cara (cada uma spawna um processo ffmpeg) — por isso os passos 2 e 3
/// existem para minimizar quantos vídeos realmente passam por ela.
/// </summary>
public sealed class VideoSimilarityDetector : IDuplicateDetector
{
    /// <summary>Pontos relativos da duração do vídeo de onde extrair um frame cada. Início/meio/fim captura a estrutura do vídeo sem precisar decodificá-lo inteiro.</summary>
    private static readonly double[] FrameTimestampFractions = [0.10, 0.50, 0.90];

    /// <summary>Diferença máxima de duração (em segundos) para dois vídeos ainda serem considerados candidatos — evita comparar vídeos claramente de durações diferentes.</summary>
    private const double DurationToleranceSeconds = 2.0;

    private const int HashBitsPerFrame = 64;

    private readonly IVideoFrameExtractor _frameExtractor;
    private readonly IImageHasher _imageHasher;

    public VideoSimilarityDetector(IVideoFrameExtractor frameExtractor, IImageHasher imageHasher)
    {
        _frameExtractor = frameExtractor;
        _imageHasher = imageHasher;
    }

    /// <inheritdoc />
    public DuplicateKind Kind => DuplicateKind.SimilarVideo;

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

        var metadata = await ReadMetadataAsync(candidates, options, cancellationToken);
        var validMetadata = metadata.Where(m => m.Duration is not null).ToList();

        var candidatePairs = FindPairsWithSimilarDuration(validMetadata);
        if (candidatePairs.Count == 0)
        {
            return [];
        }

        // Só extrai frames dos vídeos que participam de ao menos um par candidato — vídeos
        // cuja duração não combina com nenhum outro nunca precisam ser abertos pelo ffmpeg.
        var indicesNeedingFrames = candidatePairs.SelectMany(pair => new[] { pair.A, pair.B }).Distinct().ToList();
        var frameHashesByIndex = await ComputeFrameHashesAsync(indicesNeedingFrames, validMetadata, options, progress, cancellationToken);

        var maxAverageDistance = ThresholdToMaxAverageDistance(options.VideoSimilarityThreshold);

        var unionFind = new UnionFind(validMetadata.Count);
        foreach (var (a, b) in candidatePairs)
        {
            if (!frameHashesByIndex.TryGetValue(a, out var hashesA) || !frameHashesByIndex.TryGetValue(b, out var hashesB))
            {
                // Extração de frame falhou para pelo menos um dos dois (vídeo corrompido, codec não suportado) — não há como comparar.
                continue;
            }

            if (AverageFrameHammingDistance(hashesA, hashesB) <= maxAverageDistance)
            {
                unionFind.Union(a, b);
            }
        }

        return BuildGroups(unionFind, validMetadata, frameHashesByIndex);
    }

    /// <summary>Lê duração/resolução de todos os candidatos em paralelo. Vídeos que falham (corrompidos, formato não suportado) recebem <c>Duration = null</c> e saem da comparação.</summary>
    private async Task<List<VideoMetadata>> ReadMetadataAsync(
        IReadOnlyList<FileEntry> candidates,
        ScanOptions options,
        CancellationToken cancellationToken)
    {
        var results = new VideoMetadata[candidates.Count];

        var parallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = Math.Max(1, options.MaxDegreeOfParallelism),
            CancellationToken = cancellationToken,
        };

        await Parallel.ForEachAsync(Enumerable.Range(0, candidates.Count), parallelOptions, async (i, ct) =>
        {
            var file = candidates[i];
            try
            {
                var (duration, width, height) = await _frameExtractor.GetMetadataAsync(file.FullPath, ct);
                results[i] = new VideoMetadata(file, duration, width, height);
            }
            catch (Exception)
            {
                // Amplamente capturado de propósito, mesma razão do ImageSimilarityDetector:
                // um vídeo problemático nunca deve interromper o escaneamento inteiro.
                results[i] = new VideoMetadata(file, null, null, null);
            }
        });

        return results.ToList();
    }

    private static List<(int A, int B)> FindPairsWithSimilarDuration(List<VideoMetadata> validMetadata)
    {
        var pairs = new List<(int A, int B)>();

        for (var i = 0; i < validMetadata.Count; i++)
        {
            for (var j = i + 1; j < validMetadata.Count; j++)
            {
                var difference = Math.Abs((validMetadata[i].Duration!.Value - validMetadata[j].Duration!.Value).TotalSeconds);
                if (difference <= DurationToleranceSeconds)
                {
                    pairs.Add((i, j));
                }
            }
        }

        return pairs;
    }

    /// <summary>Extrai frames-chave e calcula o hash perceptual de cada um, só para os índices informados.</summary>
    private async Task<Dictionary<int, ulong[]>> ComputeFrameHashesAsync(
        List<int> indices,
        List<VideoMetadata> validMetadata,
        ScanOptions options,
        IProgress<ScanProgress>? progress,
        CancellationToken cancellationToken)
    {
        var results = new System.Collections.Concurrent.ConcurrentDictionary<int, ulong[]>();
        long processed = 0;

        var parallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = Math.Max(1, options.MaxDegreeOfParallelism),
            CancellationToken = cancellationToken,
        };

        await Parallel.ForEachAsync(indices, parallelOptions, async (index, ct) =>
        {
            var entry = validMetadata[index];

            try
            {
                var timestamps = FrameTimestampFractions
                    .Select(fraction => TimeSpan.FromSeconds(entry.Duration!.Value.TotalSeconds * fraction))
                    .ToArray();

                var frameStreams = await _frameExtractor.ExtractFramesAsync(entry.File.FullPath, timestamps, ct);

                var hashes = new ulong[frameStreams.Length];
                for (var i = 0; i < frameStreams.Length; i++)
                {
                    await using var stream = frameStreams[i];
                    hashes[i] = _imageHasher.ComputeHash(stream);
                }

                results[index] = hashes;
            }
            catch (Exception)
            {
                // Não adiciona ao dicionário — o chamador trata a ausência como "não foi possível comparar este vídeo".
            }

            var count = Interlocked.Increment(ref processed);
            progress?.Report(new ScanProgress("Comparando vídeos", count, indices.Count, entry.File.FullPath, GroupsFoundSoFar: 0));
        });

        return results.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
    }

    private double AverageFrameHammingDistance(ulong[] hashesA, ulong[] hashesB)
    {
        double total = 0;
        var frameCount = Math.Min(hashesA.Length, hashesB.Length);

        for (var i = 0; i < frameCount; i++)
        {
            total += _imageHasher.HammingDistance(hashesA[i], hashesB[i]);
        }

        return frameCount == 0 ? double.MaxValue : total / frameCount;
    }

    /// <summary>Converte o threshold de similaridade (0.0 tolerante .. 1.0 exigente) na distância média de Hamming máxima aceitável entre frames correspondentes.</summary>
    private static double ThresholdToMaxAverageDistance(double threshold) =>
        (1.0 - Math.Clamp(threshold, 0.0, 1.0)) * HashBitsPerFrame;

    private List<DuplicateGroup> BuildGroups(UnionFind unionFind, List<VideoMetadata> validMetadata, Dictionary<int, ulong[]> frameHashesByIndex)
    {
        var groups = new List<DuplicateGroup>();

        foreach (var component in unionFind.GetComponents())
        {
            if (component.Count < 2)
            {
                continue;
            }

            var members = component.Select(i => validMetadata[i]).ToList();

            groups.Add(new DuplicateGroup
            {
                Kind = DuplicateKind.SimilarVideo,
                SimilarityScore = ComputeGroupSimilarityScore(component, frameHashesByIndex),
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

    private double ComputeGroupSimilarityScore(List<int> component, Dictionary<int, ulong[]> frameHashesByIndex)
    {
        var comparable = component.Where(frameHashesByIndex.ContainsKey).ToList();
        if (comparable.Count < 2)
        {
            return 1.0;
        }

        double totalDistance = 0;
        var pairCount = 0;

        for (var i = 0; i < comparable.Count; i++)
        {
            for (var j = i + 1; j < comparable.Count; j++)
            {
                totalDistance += AverageFrameHammingDistance(frameHashesByIndex[comparable[i]], frameHashesByIndex[comparable[j]]);
                pairCount++;
            }
        }

        var averageDistance = totalDistance / pairCount;
        return 1.0 - (averageDistance / HashBitsPerFrame);
    }

    /// <summary>Metadados de um vídeo candidato: duração/resolução nulas se a leitura falhou.</summary>
    private sealed record VideoMetadata(FileEntry File, TimeSpan? Duration, int? Width, int? Height);
}
