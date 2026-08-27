using System.IO;
using System.IO.Abstractions;
using System.IO.Hashing;
using System.Security.Cryptography;
using DuplicatorFinder.Core.Abstractions;

namespace DuplicatorFinder.Core.Hashing;

/// <summary>
/// Implementação padrão de <see cref="IFileHasher"/>, usada pelo
/// <see cref="Detection.ExactHashDetector"/> para confirmar duplicados exatos.
/// Recebe <see cref="IFileSystem"/> por injeção para permitir testes sem disco real.
/// </summary>
public sealed class FileHasher : IFileHasher
{
    /// <summary>Quantidade de bytes lida do início e do fim do arquivo para o quick hash.</summary>
    private const int QuickHashSampleBytes = 64 * 1024;

    private readonly IFileSystem _fileSystem;

    public FileHasher(IFileSystem fileSystem)
    {
        _fileSystem = fileSystem;
    }

    /// <inheritdoc />
    public async Task<ulong> QuickHashAsync(string path, CancellationToken cancellationToken)
    {
        var length = _fileSystem.FileInfo.New(path).Length;

        await using var stream = _fileSystem.File.OpenRead(path);

        // XxHash3 é um hash não-criptográfico, muito mais rápido que SHA-256 — adequado aqui
        // porque o quick hash é só um pré-filtro, não a confirmação final de igualdade.
        var hasher = new XxHash3();

        var headBuffer = new byte[Math.Min(QuickHashSampleBytes, length)];
        var headRead = await stream.ReadAtLeastAsync(headBuffer, headBuffer.Length, throwOnEndOfStream: false, cancellationToken);
        hasher.Append(headBuffer.AsSpan(0, headRead));

        // Só lê o final do arquivo separadamente se ele for grande o suficiente para não
        // sobrepor os bytes já lidos no início (evita hashear o mesmo trecho duas vezes).
        if (length > QuickHashSampleBytes * 2)
        {
            stream.Seek(-QuickHashSampleBytes, SeekOrigin.End);
            var tailBuffer = new byte[QuickHashSampleBytes];
            var tailRead = await stream.ReadAtLeastAsync(tailBuffer, tailBuffer.Length, throwOnEndOfStream: false, cancellationToken);
            hasher.Append(tailBuffer.AsSpan(0, tailRead));
        }

        return BitConverter.ToUInt64(hasher.GetCurrentHash());
    }

    /// <inheritdoc />
    public async Task<byte[]> FullHashAsync(string path, CancellationToken cancellationToken)
    {
        await using var stream = _fileSystem.File.OpenRead(path);

        // SHA-256 é criptográfico (risco de colisão praticamente nulo) — usado aqui como
        // confirmação final antes de considerar dois arquivos definitivamente idênticos.
        using var sha256 = SHA256.Create();
        return await sha256.ComputeHashAsync(stream, cancellationToken);
    }
}
