using System.IO;
using System.IO.Abstractions;
using DuplicatorFinder.Core.Abstractions;
using DuplicatorFinder.Core.Models;

namespace DuplicatorFinder.Infrastructure.Move;

/// <summary>
/// Implementação de <see cref="IDuplicateMoveService"/> baseada em <see cref="IFileSystem"/>
/// (não em nenhuma API específica do Windows) — criar pastas e mover arquivos são operações
/// padrão de sistema de arquivos, ao contrário da Lixeira
/// (<see cref="Recycle.WindowsRecycleBinService"/>), que precisa de uma API concreta do SO.
/// Recebe <see cref="IFileSystem"/> por injeção para permitir testes sem disco real.
/// </summary>
public sealed class DuplicateMoveService : IDuplicateMoveService
{
    private readonly IFileSystem _fileSystem;

    public DuplicateMoveService(IFileSystem fileSystem)
    {
        _fileSystem = fileSystem;
    }

    /// <inheritdoc />
    public string CreateBatchFolder(string destinationRoot)
    {
        for (var counter = 1; ; counter++)
        {
            var candidate = _fileSystem.Path.Combine(destinationRoot, $"copias({counter})");
            if (!_fileSystem.Directory.Exists(candidate))
            {
                _fileSystem.Directory.CreateDirectory(candidate);
                return candidate;
            }
        }
    }

    /// <inheritdoc />
    public Task<MoveResult> MoveGroupAsync(
        string batchFolder,
        string keptFilePath,
        IEnumerable<string> copiesToMove,
        bool moveKeptFile,
        CancellationToken cancellationToken)
    {
        // File.Move é síncrono; roda em uma thread de background para não bloquear quem
        // chamou (tipicamente a UI thread, via um RelayCommand assíncrono) — mesmo motivo do
        // WindowsRecycleBinService.
        return Task.Run(() =>
        {
            var succeeded = new List<string>();
            var failures = new List<(string Path, string Error)>();

            // 1. Move o arquivo mantido direto para dentro da pasta numerada — só no modo
            // "mover o grupo inteiro". No modo "manter o de maior resolução no lugar" ele é
            // deliberadamente deixado onde está, e serve apenas para nomear a subpasta abaixo.
            if (moveKeptFile)
            {
                try
                {
                    var keptDestination = ResolveCollisionFreeDestination(batchFolder, keptFilePath);
                    _fileSystem.File.Move(keptFilePath, keptDestination);
                    succeeded.Add(keptFilePath);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    failures.Add((keptFilePath, ex.Message));
                }
            }

            // 2. Move as cópias para uma subpasta nomeada a partir do arquivo mantido — usa o
            // nome original dele mesmo quando o passo 1 falhou ou foi pulado, para o
            // agrupamento continuar reconhecível nos dois modos de movimentação.
            var copiesList = copiesToMove.ToList();
            if (copiesList.Count == 0)
            {
                return new MoveResult(succeeded, failures);
            }

            var keptNameWithoutExtension = _fileSystem.Path.GetFileNameWithoutExtension(keptFilePath);
            var copiesFolder = _fileSystem.Path.Combine(batchFolder, $"{keptNameWithoutExtension} copies moved");

            try
            {
                if (!_fileSystem.Directory.Exists(copiesFolder))
                {
                    _fileSystem.Directory.CreateDirectory(copiesFolder);
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                failures.AddRange(copiesList.Select(path => (path, ex.Message)));
                return new MoveResult(succeeded, failures);
            }

            foreach (var sourcePath in copiesList)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    var destinationPath = ResolveCollisionFreeDestination(copiesFolder, sourcePath);
                    _fileSystem.File.Move(sourcePath, destinationPath);
                    succeeded.Add(sourcePath);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    // Continua tentando os demais arquivos mesmo se este falhar (ex: arquivo
                    // aberto em outro programa) — o chamador decide o que fazer com as falhas.
                    failures.Add((sourcePath, ex.Message));
                }
            }

            return new MoveResult(succeeded, failures);
        }, cancellationToken);
    }

    /// <summary>
    /// Calcula o caminho de destino de <paramref name="sourcePath"/> dentro de
    /// <paramref name="destinationFolder"/>, acrescentando " (1)", " (2)" etc. ao nome se já
    /// existir um arquivo com o mesmo nome ali — nunca sobrescreve um arquivo já movido antes.
    /// </summary>
    private string ResolveCollisionFreeDestination(string destinationFolder, string sourcePath)
    {
        var fileName = _fileSystem.Path.GetFileName(sourcePath);
        var candidate = _fileSystem.Path.Combine(destinationFolder, fileName);

        if (!_fileSystem.File.Exists(candidate))
        {
            return candidate;
        }

        var nameWithoutExtension = _fileSystem.Path.GetFileNameWithoutExtension(fileName);
        var extension = _fileSystem.Path.GetExtension(fileName);

        for (var counter = 1; ; counter++)
        {
            candidate = _fileSystem.Path.Combine(destinationFolder, $"{nameWithoutExtension} ({counter}){extension}");
            if (!_fileSystem.File.Exists(candidate))
            {
                return candidate;
            }
        }
    }
}
