namespace DuplicatorFinder.Core.Models;

/// <summary>
/// Snapshot do progresso de um escaneamento em andamento, reportado periodicamente via
/// <see cref="IProgress{T}"/> para a UI atualizar a barra de progresso e o texto de status.
/// Instâncias são criadas com alta frequência internamente, então são passadas já "prontas"
/// (sem lógica) — quem decide a frequência de envio para a UI é o <see cref="Engine.ThrottledProgress{T}"/>.
/// </summary>
/// <param name="Phase">Nome legível da fase atual (ex: "Escaneando arquivos", "Comparando imagens").</param>
/// <param name="FilesScanned">Quantidade de arquivos já processados na fase atual.</param>
/// <param name="TotalEstimate">Estimativa do total de arquivos da fase, ou null se ainda desconhecida.</param>
/// <param name="CurrentFile">Caminho do arquivo sendo processado neste instante, para feedback visual.</param>
/// <param name="GroupsFoundSoFar">Quantidade de grupos de duplicados já encontrados até agora.</param>
/// <param name="GlobalFraction">
/// Progresso combinado de TODO o escaneamento (todas as fases), de 0.0 a 1.0, já calculado
/// pelo <see cref="Engine.ProgressAggregator"/> a partir do peso de cada fase. É o valor que
/// a barra de progresso principal da UI deve usar, para não "voltar para 0%" a cada nova fase.
/// </param>
public sealed record ScanProgress(
    string Phase,
    long FilesScanned,
    long? TotalEstimate,
    string? CurrentFile,
    int GroupsFoundSoFar,
    double GlobalFraction = 0.0);
