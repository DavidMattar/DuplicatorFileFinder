using System.Globalization;
using System.IO;
using DuplicatorFinder.Core.Abstractions;
using Xabe.FFmpeg;

namespace DuplicatorFinder.Infrastructure.Video;

/// <summary>
/// Implementação de <see cref="IVideoFrameExtractor"/> baseada na biblioteca Xabe.FFmpeg
/// (que por sua vez chama os executáveis ffmpeg/ffprobe reais). Antes de qualquer operação,
/// garante via <see cref="FfmpegBootstrap"/> que esses executáveis já foram baixados.
/// </summary>
public sealed class XabeFfmpegFrameExtractor : IVideoFrameExtractor
{
    private readonly FfmpegBootstrap _bootstrap;

    public XabeFfmpegFrameExtractor(FfmpegBootstrap bootstrap)
    {
        _bootstrap = bootstrap;
    }

    /// <inheritdoc />
    public async Task<(TimeSpan Duration, int Width, int Height)> GetMetadataAsync(string path, CancellationToken cancellationToken)
    {
        await _bootstrap.EnsureReadyAsync(downloadProgress: null, cancellationToken);

        var mediaInfo = await FFmpeg.GetMediaInfo(path, cancellationToken);
        var videoStream = mediaInfo.VideoStreams.FirstOrDefault();

        return (mediaInfo.Duration, videoStream?.Width ?? 0, videoStream?.Height ?? 0);
    }

    /// <inheritdoc />
    public async Task<Stream[]> ExtractFramesAsync(string path, TimeSpan[] timestamps, CancellationToken cancellationToken)
    {
        await _bootstrap.EnsureReadyAsync(downloadProgress: null, cancellationToken);

        var streams = new Stream[timestamps.Length];

        for (var i = 0; i < timestamps.Length; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            streams[i] = await ExtractSingleFrameAsync(path, timestamps[i]);
        }

        return streams;
    }

    /// <summary>
    /// Extrai um único frame para um arquivo temporário e o carrega inteiro em memória antes
    /// de apagar o arquivo temporário, para devolver um <see cref="Stream"/> que o chamador
    /// pode ler independentemente de qualquer arquivo em disco.
    /// Monta o comando ffmpeg manualmente (via <c>AddParameter</c>) em vez de usar a classe
    /// <c>Snippets</c> da biblioteca: seu construtor é <c>internal</c> nesta versão do
    /// Xabe.FFmpeg, então não é possível instanciá-la fora da própria biblioteca — confirmado
    /// via inspeção do assembly antes de escrever este código.
    /// </summary>
    private static async Task<Stream> ExtractSingleFrameAsync(string videoPath, TimeSpan timestamp)
    {
        var tempFilePath = Path.Combine(Path.GetTempPath(), $"duplicatorfinder_frame_{Guid.NewGuid():N}.jpg");

        try
        {
            var seekSeconds = timestamp.TotalSeconds.ToString(CultureInfo.InvariantCulture);

            var conversion = FFmpeg.Conversions.New()
                .AddParameter($"-ss {seekSeconds}", ParameterPosition.PreInput)
                .AddParameter($"-i \"{videoPath}\"", ParameterPosition.PreInput)
                .AddParameter("-frames:v 1", ParameterPosition.PostInput)
                .SetOutput(tempFilePath)
                .SetOverwriteOutput(true);

            await conversion.Start();

            var bytes = await File.ReadAllBytesAsync(tempFilePath);
            return new MemoryStream(bytes);
        }
        finally
        {
            if (File.Exists(tempFilePath))
            {
                File.Delete(tempFilePath);
            }
        }
    }
}
