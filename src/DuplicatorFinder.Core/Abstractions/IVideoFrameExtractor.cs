namespace DuplicatorFinder.Core.Abstractions;

/// <summary>
/// Contrato para leitura de metadados e extração de frames de um arquivo de vídeo.
/// A implementação concreta (baseada em ffmpeg/ffprobe) vive em
/// DuplicatorFinder.Infrastructure — este contrato existe em Core para que
/// <see cref="Detection.VideoSimilarityDetector"/> não precise depender diretamente de ffmpeg.
/// </summary>
public interface IVideoFrameExtractor
{
    /// <summary>
    /// Lê os metadados do vídeo (duração e resolução) sem decodificar nenhum frame.
    /// É uma operação barata (usa ffprobe), por isso é chamada para todos os vídeos
    /// candidatos antes de decidir quais realmente precisam ter frames extraídos.
    /// </summary>
    Task<(TimeSpan Duration, int Width, int Height)> GetMetadataAsync(
        string path,
        CancellationToken cancellationToken);

    /// <summary>
    /// Extrai um frame (como imagem) para cada timestamp solicitado.
    /// É uma operação relativamente cara (spawna um processo ffmpeg por frame), por isso só
    /// deve ser chamada para vídeos que já passaram pelo pré-filtro de duração/resolução.
    /// </summary>
    /// <param name="path">Caminho do arquivo de vídeo.</param>
    /// <param name="timestamps">Instantes do vídeo (relativos ao início) de onde extrair um frame cada.</param>
    /// <param name="cancellationToken">Permite cancelar a extração em andamento.</param>
    Task<Stream[]> ExtractFramesAsync(
        string path,
        TimeSpan[] timestamps,
        CancellationToken cancellationToken);
}
