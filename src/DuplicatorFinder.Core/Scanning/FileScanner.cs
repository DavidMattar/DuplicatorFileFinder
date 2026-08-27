using System.IO;
using System.IO.Abstractions;
using System.Runtime.CompilerServices;
using DuplicatorFinder.Core.Abstractions;
using DuplicatorFinder.Core.Models;

namespace DuplicatorFinder.Core.Scanning;

/// <summary>
/// Implementação padrão de <see cref="IFileScanner"/>: percorre as pastas configuradas
/// (recursivamente, se solicitado) e produz um <see cref="FileEntry"/> para cada arquivo que
/// passa pelos filtros de <see cref="FileFilterEvaluator"/>.
/// Recebe <see cref="IFileSystem"/> por injeção, em vez de usar as classes estáticas
/// <c>System.IO.File</c>/<c>Directory</c> diretamente, para que os testes possam substituir
/// o disco real por um sistema de arquivos falso em memória (<c>MockFileSystem</c>).
/// </summary>
public sealed class FileScanner : IFileScanner
{
    private readonly IFileSystem _fileSystem;

    /// <param name="fileSystem">
    /// Abstração do sistema de arquivos. Em produção é o disco real
    /// (<see cref="System.IO.Abstractions.FileSystem"/>); em testes, um <c>MockFileSystem</c>.
    /// </param>
    public FileScanner(IFileSystem fileSystem)
    {
        _fileSystem = fileSystem;
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<FileEntry> ScanAsync(
        ScanOptions options,
        IProgress<ScanProgress>? progress,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        long filesScanned = 0;

        foreach (var rootFolder in options.RootFolders)
        {
            IEnumerable<string> filePaths;
            try
            {
                filePaths = EnumerateFiles(rootFolder, options.IncludeSubfolders);
            }
            catch (IOException)
            {
                // Pasta raiz inacessível ou removida entre a configuração e o início do scan:
                // pula para a próxima pasta em vez de abortar o escaneamento inteiro.
                continue;
            }

            foreach (var filePath in filePaths)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var entry = TryCreateEntry(filePath, options);
                if (entry is null)
                {
                    continue;
                }

                filesScanned++;
                progress?.Report(new ScanProgress("Escaneando arquivos", filesScanned, TotalEstimate: null, filePath, GroupsFoundSoFar: 0));

                yield return entry;

                // Cede o controle periodicamente para não monopolizar a thread em pastas com muitos arquivos.
                if (filesScanned % 500 == 0)
                {
                    await Task.Yield();
                }
            }
        }
    }

    /// <summary>
    /// Enumera os arquivos de uma pasta usando <see cref="EnumerationOptions.IgnoreInaccessible"/>,
    /// para que uma subpasta sem permissão de leitura seja simplesmente ignorada em vez de
    /// interromper a varredura inteira com uma <see cref="UnauthorizedAccessException"/>.
    /// </summary>
    private IEnumerable<string> EnumerateFiles(string rootFolder, bool includeSubfolders)
    {
        var enumerationOptions = new EnumerationOptions
        {
            RecurseSubdirectories = includeSubfolders,
            IgnoreInaccessible = true,
            AttributesToSkip = FileAttributes.System | FileAttributes.ReparsePoint,
        };

        return _fileSystem.Directory.EnumerateFiles(rootFolder, "*", enumerationOptions);
    }

    /// <summary>
    /// Lê os metadados de um arquivo e o converte em <see cref="FileEntry"/>, ou retorna
    /// null quando o arquivo não passa pelos filtros configurados ou não pôde ser lido
    /// (ex: foi apagado entre a enumeração e esta leitura).
    /// </summary>
    private FileEntry? TryCreateEntry(string filePath, ScanOptions options)
    {
        try
        {
            var info = _fileSystem.FileInfo.New(filePath);
            var extension = info.Extension;

            if (!FileFilterEvaluator.PassesFilter(info.Length, extension, options))
            {
                return null;
            }

            return new FileEntry(
                FullPath: info.FullName,
                SizeBytes: info.Length,
                CreatedUtc: info.CreationTimeUtc,
                ModifiedUtc: info.LastWriteTimeUtc,
                Extension: extension.ToLowerInvariant());
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }
}
