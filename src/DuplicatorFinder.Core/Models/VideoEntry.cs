namespace DuplicatorFinder.Core.Models;

/// <summary>
/// Estende <see cref="FileEntry"/> com informações específicas de vídeo, usadas pelo
/// detector de vídeos similares (VideoSimilarityDetector).
/// </summary>
/// <param name="File">Metadados básicos do arquivo (caminho, tamanho, datas).</param>
/// <param name="Duration">Duração total do vídeo.</param>
/// <param name="Width">Largura do vídeo em pixels.</param>
/// <param name="Height">Altura do vídeo em pixels.</param>
/// <param name="FrameHashes">
/// Hashes perceptuais de alguns frames-chave (ex: 10%, 50% e 90% da duração).
/// Comparar esses hashes entre dois vídeos é muito mais rápido do que comparar o vídeo
/// inteiro, e já é suficiente para detectar recodificações/recortes do mesmo conteúdo.
/// </param>
public sealed record VideoEntry(
    FileEntry File,
    TimeSpan Duration,
    int Width,
    int Height,
    ulong[] FrameHashes);
