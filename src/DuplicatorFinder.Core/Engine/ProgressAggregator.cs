using DuplicatorFinder.Core.Models;

namespace DuplicatorFinder.Core.Engine;

/// <summary>
/// Traduz o progresso "local" de cada fase do escaneamento (ex: "3000 de 10000 arquivos
/// hasheados") em um progresso "global" (0.0 a 1.0) que representa o escaneamento inteiro,
/// usando um peso fixo por fase. Sem isso, a barra de progresso da UI voltaria para 0% a
/// cada nova fase (scan → hash rápido → hash completo → imagens → vídeos), o que pareceria
/// um bug para o usuário.
/// </summary>
public sealed class ProgressAggregator
{
    /// <summary>
    /// Peso padrão de cada fase no progresso global. A soma não precisa ser exatamente 1.0
    /// (fases desabilitadas pelo usuário simplesmente não são reportadas), mas foi calibrada
    /// para somar 1.0 quando todas as fases estão habilitadas.
    /// </summary>
    public static readonly Dictionary<string, double> DefaultPhaseWeights = new()
    {
        ["Escaneando arquivos"] = 0.10,
        ["Comparando arquivos (hash rápido)"] = 0.20,
        ["Confirmando duplicados exatos"] = 0.20,
        ["Comparando imagens"] = 0.25,
        ["Comparando vídeos"] = 0.25,
    };

    private readonly IProgress<ScanProgress> _target;
    private readonly Dictionary<string, double> _phaseWeights;
    private double _completedWeight;

    /// <param name="target">O <see cref="IProgress{ScanProgress}"/> final (tipicamente já envolto em <see cref="ThrottledProgress{T}"/>) que recebe os valores com <see cref="ScanProgress.GlobalFraction"/> já calculado.</param>
    /// <param name="phaseWeights">Pesos por fase; usa <see cref="DefaultPhaseWeights"/> se omitido.</param>
    public ProgressAggregator(IProgress<ScanProgress> target, Dictionary<string, double>? phaseWeights = null)
    {
        _target = target;
        _phaseWeights = phaseWeights ?? DefaultPhaseWeights;
    }

    /// <summary>
    /// Recebe um <see cref="ScanProgress"/> "local" (produzido pelo scanner ou por um
    /// detector, sem noção das outras fases) e repassa ao <see cref="_target"/> uma cópia
    /// com <see cref="ScanProgress.GlobalFraction"/> preenchido.
    /// </summary>
    public void Report(ScanProgress localProgress)
    {
        var phaseWeight = _phaseWeights.GetValueOrDefault(localProgress.Phase, 0.0);

        var localFraction = localProgress.TotalEstimate is > 0
            ? Math.Clamp((double)localProgress.FilesScanned / localProgress.TotalEstimate.Value, 0.0, 1.0)
            : 0.0;

        var globalFraction = Math.Clamp(_completedWeight + (phaseWeight * localFraction), 0.0, 1.0);

        _target.Report(localProgress with { GlobalFraction = globalFraction });
    }

    /// <summary>
    /// Marca uma fase como inteiramente concluída, somando seu peso ao progresso acumulado.
    /// Deve ser chamado pelo <see cref="DuplicateScanEngine"/> exatamente uma vez, na
    /// transição de cada fase para a próxima — inclusive quando a fase termina rápido demais
    /// para ter gerado algum <see cref="Report"/> (ex: zero candidatos).
    /// </summary>
    public void CompletePhase(string phaseName)
    {
        if (_phaseWeights.TryGetValue(phaseName, out var weight))
        {
            _completedWeight = Math.Clamp(_completedWeight + weight, 0.0, 1.0);
        }
    }
}
