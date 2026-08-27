namespace DuplicatorFinder.Core.Abstractions;

/// <summary>
/// Contrato para cálculo de hash de conteúdo de arquivos, usado pelo
/// <see cref="Detection.ExactHashDetector"/> para confirmar se dois arquivos são idênticos.
/// </summary>
public interface IFileHasher
{
    /// <summary>
    /// Calcula um hash rápido e barato, lendo apenas uma pequena parte do arquivo
    /// (ex: primeiros e últimos 64KB). Usado como pré-filtro: arquivos com quick hash
    /// diferente certamente não são idênticos, então nem precisam ter o hash completo
    /// calculado, o que economiza muita leitura de disco em arquivos grandes.
    /// </summary>
    Task<ulong> QuickHashAsync(string path, CancellationToken cancellationToken);

    /// <summary>
    /// Calcula o hash do conteúdo inteiro do arquivo, lendo-o em streaming (sem carregar
    /// tudo na memória de uma vez). Só deve ser chamado para arquivos que já passaram pelo
    /// filtro de tamanho + <see cref="QuickHashAsync"/>, para minimizar I/O.
    /// </summary>
    Task<byte[]> FullHashAsync(string path, CancellationToken cancellationToken);
}
