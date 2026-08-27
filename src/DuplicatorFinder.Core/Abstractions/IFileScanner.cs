using DuplicatorFinder.Core.Models;

namespace DuplicatorFinder.Core.Abstractions;

/// <summary>
/// Contrato para varredura de pastas em busca de arquivos candidatos a duplicados.
/// Existir como interface (em vez de usar <see cref="Scanning.FileScanner"/> diretamente)
/// permite testar o resto do sistema com um scanner falso, sem tocar no disco real.
/// </summary>
public interface IFileScanner
{
    /// <summary>
    /// Percorre as pastas configuradas em <paramref name="options"/> e produz um
    /// <see cref="FileEntry"/> para cada arquivo que passa pelos filtros.
    /// É <see cref="IAsyncEnumerable{T}"/> (não uma lista) para permitir que o chamador
    /// comece a processar os primeiros arquivos antes que a varredura inteira termine.
    /// </summary>
    /// <param name="options">Pastas, filtros e demais parâmetros do escaneamento.</param>
    /// <param name="progress">Callback opcional para reportar progresso à UI.</param>
    /// <param name="cancellationToken">Permite cancelar a varredura em andamento.</param>
    IAsyncEnumerable<FileEntry> ScanAsync(
        ScanOptions options,
        IProgress<ScanProgress>? progress,
        CancellationToken cancellationToken);
}
