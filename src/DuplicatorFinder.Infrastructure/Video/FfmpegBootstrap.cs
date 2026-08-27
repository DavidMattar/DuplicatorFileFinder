using System.IO;
using Xabe.FFmpeg;
using Xabe.FFmpeg.Downloader;

namespace DuplicatorFinder.Infrastructure.Video;

/// <summary>
/// Garante que os executáveis ffmpeg/ffprobe estejam disponíveis antes de qualquer operação
/// de vídeo. O Xabe.FFmpeg não vem com esses binários embutidos (são grandes e específicos
/// de sistema operacional) — são baixados uma única vez, na primeira vez que o usuário usa a
/// detecção de vídeos, para uma pasta fixa em %LocalAppData%, e reaproveitados depois disso.
/// </summary>
public sealed class FfmpegBootstrap
{
    private static readonly SemaphoreSlim DownloadGate = new(1, 1);
    private static bool _isReady;

    private readonly string _ffmpegFolder;

    public FfmpegBootstrap()
    {
        _ffmpegFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DuplicatorFinder",
            "ffmpeg");
    }

    /// <summary>
    /// Garante que ffmpeg/ffprobe existem em disco e que o Xabe.FFmpeg sabe onde encontrá-los.
    /// Seguro de chamar concorrentemente de várias threads — só a primeira chamada de fato
    /// baixa algo (protegido por <see cref="SemaphoreSlim"/>); as demais só aguardam.
    /// </summary>
    /// <param name="downloadProgress">Progresso do download (0.0 a 1.0), reportado apenas na primeira execução.</param>
    /// <param name="cancellationToken">Permite cancelar a espera pelo download (o download em si, uma vez iniciado, não é interrompido).</param>
    public async Task EnsureReadyAsync(IProgress<double>? downloadProgress, CancellationToken cancellationToken)
    {
        if (_isReady)
        {
            return;
        }

        await DownloadGate.WaitAsync(cancellationToken);
        try
        {
            if (_isReady)
            {
                return;
            }

            Directory.CreateDirectory(_ffmpegFolder);
            FFmpeg.SetExecutablesPath(_ffmpegFolder);

            var ffmpegExists = File.Exists(Path.Combine(_ffmpegFolder, "ffmpeg.exe"));
            var ffprobeExists = File.Exists(Path.Combine(_ffmpegFolder, "ffprobe.exe"));

            if (!ffmpegExists || !ffprobeExists)
            {
                var progressAdapter = downloadProgress is null
                    ? null
                    : new Progress<ProgressInfo>(info => downloadProgress.Report(
                        info.TotalBytes > 0 ? (double)info.DownloadedBytes / info.TotalBytes : 0.0));

                await FFmpegDownloader.GetLatestVersion(FFmpegVersion.Official, _ffmpegFolder, progressAdapter);
            }

            _isReady = true;
        }
        finally
        {
            DownloadGate.Release();
        }
    }
}
