using System.Diagnostics;

namespace DuplicatorFinder.Core.Engine;

/// <summary>
/// Decorator de <see cref="IProgress{T}"/> que descarta a maioria das chamadas a
/// <see cref="Report"/>, repassando apenas uma a cada intervalo mínimo de tempo.
/// Sem isso, um escaneamento de milhões de arquivos geraria milhões de atualizações de UI
/// por segundo, o que travaria o Dispatcher do WPF. A última atualização de cada fase é
/// sempre reportada normalmente pelo chamador (fora deste decorator), então não há perda de
/// informação relevante — só de atualizações intermediárias redundantes para o olho humano.
/// </summary>
public sealed class ThrottledProgress<T> : IProgress<T>
{
    private readonly IProgress<T> _inner;
    private readonly TimeSpan _minInterval;
    private readonly Stopwatch _stopwatch = Stopwatch.StartNew();
    private readonly object _lock = new();

    /// <summary>
    /// Null significa "ainda não reportou nada". Usar <see cref="TimeSpan.MinValue"/> como
    /// sentinela aqui causaria overflow na primeira chamada (MinValue subtraído de um valor
    /// positivo excede o intervalo representável de TimeSpan) — descoberto rodando um
    /// escaneamento real de ponta a ponta, não pelos testes unitários com mocks.
    /// </summary>
    private TimeSpan? _lastReportedAt;

    /// <param name="inner">O <see cref="IProgress{T}"/> real (geralmente ligado à UI) que deve receber as atualizações filtradas.</param>
    /// <param name="minInterval">Intervalo mínimo entre duas atualizações repassadas. Padrão: 150ms.</param>
    public ThrottledProgress(IProgress<T> inner, TimeSpan? minInterval = null)
    {
        _inner = inner;
        _minInterval = minInterval ?? TimeSpan.FromMilliseconds(150);
    }

    /// <inheritdoc />
    public void Report(T value)
    {
        lock (_lock)
        {
            var now = _stopwatch.Elapsed;
            if (_lastReportedAt is not null && now - _lastReportedAt.Value < _minInterval)
            {
                return;
            }

            _lastReportedAt = now;
        }

        _inner.Report(value);
    }
}
