using System.Collections.Concurrent;
using System.IO;
using DuplicatorFinder.Core.Abstractions;
using DuplicatorFinder.Core.Models;

namespace DuplicatorFinder.Core.Detection;

/// <summary>
/// Estratégia de detecção (padrão Strategy, ver <see cref="IDuplicateDetector"/>) para
/// arquivos idênticos byte a byte, de qualquer tipo.
/// Usa um pipeline em 3 etapas, cada uma mais cara que a anterior, para minimizar a
/// quantidade de I/O de disco necessária:
///   1) agrupar por tamanho (grátis — já vem no <see cref="FileEntry"/>);
///   2) agrupar por "quick hash" (lê só uma amostra do arquivo);
///   3) agrupar por hash completo (lê o arquivo inteiro), só para quem sobrou da etapa 2.
/// </summary>
public sealed class ExactHashDetector : IDuplicateDetector
{
    private readonly IFileHasher _fileHasher;

    public ExactHashDetector(IFileHasher fileHasher)
    {
        _fileHasher = fileHasher;
    }

    /// <inheritdoc />
    public DuplicateKind Kind => DuplicateKind.ExactFile;

    /// <inheritdoc />
    public async Task<IReadOnlyList<DuplicateGroup>> DetectAsync(
        IReadOnlyList<FileEntry> candidates,
        ScanOptions options,
        IProgress<ScanProgress>? progress,
        CancellationToken cancellationToken)
    {
        // Etapa 1: só arquivos que têm pelo menos outro arquivo do mesmo tamanho podem ser
        // duplicados exatos — descartar o resto aqui evita qualquer I/O desnecessário.
        var sameSizeFiles = candidates
            .GroupBy(f => f.SizeBytes)
            .Where(g => g.Count() > 1)
            .SelectMany(g => g)
            .ToList();

        // Etapa 2: quick hash (amostra do arquivo) para eliminar a maioria dos falsos candidatos.
        var quickHashGroups = await GroupByAsync(
            sameSizeFiles,
            keySelector: async (file, ct) => (file.SizeBytes, await _fileHasher.QuickHashAsync(file.FullPath, ct)),
            phaseName: "Comparando arquivos (hash rápido)",
            options,
            progress,
            cancellationToken);

        var quickHashSurvivors = quickHashGroups.Where(g => g.Count > 1).SelectMany(g => g).ToList();

        // Etapa 3: hash completo (SHA-256) só nos candidatos que sobraram — confirma a
        // igualdade real do conteúdo, eliminando o risco (teórico, mas existente) de colisão do quick hash.
        var fullHashGroups = await GroupByAsync(
            quickHashSurvivors,
            keySelector: async (file, ct) => (file.SizeBytes, Convert.ToHexString(await _fileHasher.FullHashAsync(file.FullPath, ct))),
            phaseName: "Confirmando duplicados exatos",
            options,
            progress,
            cancellationToken);

        return fullHashGroups
            .Where(group => group.Count > 1)
            .Select(files => new DuplicateGroup
            {
                Kind = DuplicateKind.ExactFile,
                Files = files.Select(f => new DuplicateFile { File = f }).ToList(),
                SimilarityScore = 1.0,
            })
            .ToList();
    }

    /// <summary>
    /// Calcula, em paralelo (respeitando <see cref="ScanOptions.MaxDegreeOfParallelism"/>),
    /// uma chave de agrupamento para cada arquivo e retorna os arquivos já agrupados por
    /// essa chave. Extraído como método genérico porque as etapas de quick hash e full hash
    /// seguem exatamente o mesmo padrão, só muda como a chave é calculada.
    /// Arquivos que não puderem ser lidos (apagados/sem permissão nesse instante) são
    /// silenciosamente ignorados, para não abortar a detecção inteira por um único arquivo problemático.
    /// </summary>
    private static async Task<List<List<FileEntry>>> GroupByAsync<TKey>(
        List<FileEntry> files,
        Func<FileEntry, CancellationToken, Task<TKey>> keySelector,
        string phaseName,
        ScanOptions options,
        IProgress<ScanProgress>? progress,
        CancellationToken cancellationToken)
        where TKey : notnull
    {
        var groups = new ConcurrentDictionary<TKey, List<FileEntry>>();
        long processed = 0;

        var parallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = Math.Max(1, options.MaxDegreeOfParallelism),
            CancellationToken = cancellationToken,
        };

        await Parallel.ForEachAsync(files, parallelOptions, async (file, ct) =>
        {
            TKey key;
            try
            {
                key = await keySelector(file, ct);
            }
            catch (IOException)
            {
                return;
            }
            catch (UnauthorizedAccessException)
            {
                return;
            }

            groups.AddOrUpdate(
                key,
                _ => [file],
                (_, list) =>
                {
                    lock (list)
                    {
                        list.Add(file);
                    }

                    return list;
                });

            var count = Interlocked.Increment(ref processed);
            progress?.Report(new ScanProgress(phaseName, count, files.Count, file.FullPath, GroupsFoundSoFar: 0));
        });

        return groups.Values.ToList();
    }
}
