using DuplicatorFinder.Core.Engine;
using FluentAssertions;
using Xunit;

namespace DuplicatorFinder.Core.Tests.Engine;

/// <summary>
/// Testa <see cref="ThrottledProgress{T}"/>. O primeiro teste é uma regressão direta de um
/// bug real encontrado só em um teste de ponta a ponta com escaneamento de verdade: usar
/// <see cref="TimeSpan.MinValue"/> como sentinela de "nunca reportou" causava overflow de
/// <see cref="TimeSpan"/> já na primeira chamada a <see cref="ThrottledProgress{T}.Report"/>.
/// </summary>
public class ThrottledProgressTests
{
    [Fact]
    public void Report_DoesNotThrow_OnFirstCall()
    {
        var received = new List<int>();
        var throttled = new ThrottledProgress<int>(new SynchronousProgress<int>(received.Add));

        var act = () => throttled.Report(1);

        act.Should().NotThrow();
    }

    [Fact]
    public void Report_ForwardsFirstCallImmediately()
    {
        var received = new List<int>();
        var throttled = new ThrottledProgress<int>(new SynchronousProgress<int>(received.Add));

        throttled.Report(1);

        received.Should().ContainSingle().Which.Should().Be(1);
    }

    [Fact]
    public void Report_SuppressesCallsWithinMinInterval()
    {
        var received = new List<int>();
        var throttled = new ThrottledProgress<int>(new SynchronousProgress<int>(received.Add), TimeSpan.FromSeconds(10));

        throttled.Report(1);
        throttled.Report(2);
        throttled.Report(3);

        received.Should().ContainSingle().Which.Should().Be(1);
    }

    /// <summary>
    /// <see cref="Progress{T}"/> marshala o callback de forma assíncrona quando não há
    /// <see cref="System.Threading.SynchronizationContext"/> capturado (o caso comum em
    /// testes), o que tornaria estes testes instáveis (o assert poderia rodar antes do
    /// callback). Este dublê simplesmente chama o callback de forma síncrona.
    /// </summary>
    private sealed class SynchronousProgress<T>(Action<T> callback) : IProgress<T>
    {
        public void Report(T value) => callback(value);
    }
}
