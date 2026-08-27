namespace DuplicatorFinder.Core.Models;

/// <summary>
/// Resultado final e completo de um escaneamento, retornado por
/// <see cref="Engine.DuplicateScanEngine.RunAsync"/> quando todas as fases terminam.
/// </summary>
/// <param name="Groups">Todos os grupos de duplicados encontrados, de todos os tipos habilitados.</param>
/// <param name="Elapsed">Tempo total que o escaneamento levou.</param>
/// <param name="TotalFilesScanned">Quantidade total de arquivos considerados no escaneamento.</param>
/// <param name="TotalWastedBytes">Soma do espaço em disco que seria liberado excluindo as cópias de todos os grupos.</param>
public sealed record ScanResult(
    IReadOnlyList<DuplicateGroup> Groups,
    TimeSpan Elapsed,
    long TotalFilesScanned,
    long TotalWastedBytes);
