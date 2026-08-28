using System.Diagnostics;
using DuplicatorFinder.Core.Abstractions;
using DuplicatorFinder.Core.Models;
using DuplicatorFinder.Core.Support;

namespace DuplicatorFinder.Core.Engine;

/// <summary>
/// Orquestrador central do escaneamento (padrão Facade): esconde da UI toda a complexidade
/// de varrer as pastas, particionar os arquivos por tipo, rodar os detectores habilitados em
/// paralelo, agregar os grupos resultantes e aplicar o smart-select — tudo atrás de um único
/// método, <see cref="RunAsync"/>.
/// Novos tipos de detecção (ex: documentos similares, no futuro) só precisam implementar
/// <see cref="IDuplicateDetector"/> e ser registrados na injeção de dependência: este
/// orquestrador não precisa ser alterado.
/// </summary>
public sealed class DuplicateScanEngine
{
    /// <summary>
    /// Nomes das sub-fases de progresso que cada tipo de detector pode reportar. Usado para
    /// avisar o <see cref="ProgressAggregator"/> de quais pesos marcar como concluídos quando
    /// aquele detector termina — necessário porque, por exemplo, o detector de exatos reporta
    /// duas sub-fases internamente (hash rápido e hash completo).
    /// </summary>
    private static readonly Dictionary<DuplicateKind, string[]> PhaseNamesByKind = new()
    {
        [DuplicateKind.ExactFile] = ["Comparando arquivos (hash rápido)", "Confirmando duplicados exatos"],
        [DuplicateKind.SimilarImage] = ["Comparando imagens"],
        [DuplicateKind.SimilarVideo] = ["Comparando vídeos"],
    };

    private const string ScanPhaseName = "Escaneando arquivos";

    private readonly IFileScanner _scanner;
    private readonly IReadOnlyList<IDuplicateDetector> _detectors;
    private readonly ISmartSelectStrategy _smartSelectStrategy;

    /// <param name="scanner">Responsável por enumerar os arquivos candidatos nas pastas configuradas.</param>
    /// <param name="detectors">Todas as estratégias de detecção disponíveis; as habilitadas em <see cref="ScanOptions"/> são escolhidas em tempo de execução.</param>
    /// <param name="smartSelectStrategy">Estratégia usada para decidir qual arquivo manter em cada grupo encontrado.</param>
    public DuplicateScanEngine(
        IFileScanner scanner,
        IEnumerable<IDuplicateDetector> detectors,
        ISmartSelectStrategy smartSelectStrategy)
    {
        _scanner = scanner;
        _detectors = detectors.ToList();
        _smartSelectStrategy = smartSelectStrategy;
    }

    /// <summary>
    /// Executa o escaneamento completo: varre as pastas, roda os detectores habilitados e
    /// aplica o smart-select em cada grupo encontrado. Todas as fases respeitam
    /// <paramref name="cancellationToken"/>, permitindo que o usuário cancele a qualquer momento.
    /// </summary>
    /// <param name="options">Pastas, filtros e detectores habilitados para este escaneamento.</param>
    /// <param name="smartSelectOptions">Critério a usar para decidir qual arquivo manter em cada grupo.</param>
    /// <param name="progress">
    /// Callback de progresso da UI. Internamente é decorado com <see cref="ThrottledProgress{T}"/>
    /// (para não saturar o Dispatcher) e <see cref="ProgressAggregator"/> (para calcular o
    /// percentual global entre as fases) antes de ser passado ao scanner e aos detectores.
    /// </param>
    /// <param name="cancellationToken">Permite cancelar o escaneamento em qualquer fase.</param>
    public async Task<ScanResult> RunAsync(
        ScanOptions options,
        SmartSelectOptions smartSelectOptions,
        IProgress<ScanProgress>? progress,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();

        ProgressAggregator? aggregator = progress is null
            ? null
            : new ProgressAggregator(new ThrottledProgress<ScanProgress>(progress));

        var internalProgress = aggregator is null ? null : new AggregatorProgress(aggregator);

        // Fase 1: varredura de arquivos.
        var allFiles = new List<FileEntry>();
        await foreach (var entry in _scanner.ScanAsync(options, internalProgress, cancellationToken))
        {
            allFiles.Add(entry);
        }

        aggregator?.CompletePhase(ScanPhaseName);

        // Fase 2: detectores habilitados rodam em paralelo entre si — cada um só espera pelos
        // próprios candidatos, não pelos dos outros detectores.
        var activeDetectors = _detectors.Where(detector => IsEnabled(detector.Kind, options)).ToList();

        var detectionTasks = activeDetectors.Select(detector =>
            RunDetectorAsync(detector, allFiles, options, internalProgress, aggregator, cancellationToken));

        var groupsByDetector = await Task.WhenAll(detectionTasks);
        var allGroups = groupsByDetector.SelectMany(groups => groups).ToList();

        // Fase 3: smart-select — decide o arquivo "mantido" de cada grupo encontrado.
        foreach (var group in allGroups)
        {
            _smartSelectStrategy.Apply(group, smartSelectOptions);
        }

        stopwatch.Stop();

        return new ScanResult(
            Groups: allGroups,
            Elapsed: stopwatch.Elapsed,
            TotalFilesScanned: allFiles.Count,
            TotalWastedBytes: allGroups.Sum(g => g.WastedBytes));
    }

    /// <summary>
    /// Roda um único detector sobre os candidatos relevantes para o seu <see cref="IDuplicateDetector.Kind"/>,
    /// e avisa o <see cref="ProgressAggregator"/> quando todas as sub-fases desse detector terminam.
    /// </summary>
    private static async Task<IReadOnlyList<DuplicateGroup>> RunDetectorAsync(
        IDuplicateDetector detector,
        IReadOnlyList<FileEntry> allFiles,
        ScanOptions options,
        IProgress<ScanProgress>? internalProgress,
        ProgressAggregator? aggregator,
        CancellationToken cancellationToken)
    {
        var candidates = GetCandidates(detector.Kind, allFiles);

        var groups = candidates.Count > 0
            ? await detector.DetectAsync(candidates, options, internalProgress, cancellationToken)
            : [];

        foreach (var phaseName in PhaseNamesByKind.GetValueOrDefault(detector.Kind, []))
        {
            aggregator?.CompletePhase(phaseName);
        }

        return groups;
    }

    /// <summary>Filtra os arquivos relevantes para o tipo de detector: exatos veem todos os tipos, imagem/vídeo só suas próprias extensões.</summary>
    private static List<FileEntry> GetCandidates(DuplicateKind kind, IReadOnlyList<FileEntry> allFiles) => kind switch
    {
        DuplicateKind.ExactFile => allFiles.ToList(),
        DuplicateKind.SimilarImage => allFiles.Where(f => FileTypeClassifier.IsImageExtension(f.Extension)).ToList(),
        DuplicateKind.SimilarVideo => allFiles.Where(f => FileTypeClassifier.IsVideoExtension(f.Extension)).ToList(),
        _ => [],
    };

    /// <summary>Verifica se o usuário habilitou este tipo de detecção em <see cref="ScanOptions"/>.</summary>
    private static bool IsEnabled(DuplicateKind kind, ScanOptions options) => kind switch
    {
        DuplicateKind.ExactFile => options.DetectExact,
        DuplicateKind.SimilarImage => options.DetectSimilarImages,
        DuplicateKind.SimilarVideo => options.DetectSimilarVideos,
        _ => false,
    };

    /// <summary>Adaptador simples que repassa cada <see cref="ScanProgress"/> local diretamente ao <see cref="ProgressAggregator"/>.</summary>
    private sealed class AggregatorProgress(ProgressAggregator aggregator) : IProgress<ScanProgress>
    {
        public void Report(ScanProgress value) => aggregator.Report(value);
    }
}
