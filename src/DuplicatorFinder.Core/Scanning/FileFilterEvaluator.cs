using DuplicatorFinder.Core.Models;

namespace DuplicatorFinder.Core.Scanning;

/// <summary>
/// Decide se um arquivo encontrado durante a varredura deve ser incluído no escaneamento,
/// de acordo com os filtros de <see cref="ScanOptions"/> (tamanho mínimo, extensões).
/// Extraído do <see cref="FileScanner"/> como sua própria classe para poder ser testado
/// isoladamente, sem precisar simular um sistema de arquivos inteiro.
/// </summary>
public static class FileFilterEvaluator
{
    /// <summary>
    /// Retorna verdadeiro se o arquivo com o tamanho e extensão informados atende aos
    /// filtros configurados em <paramref name="options"/>.
    /// </summary>
    /// <param name="sizeBytes">Tamanho do arquivo em bytes.</param>
    /// <param name="extension">Extensão do arquivo, incluindo o ponto (ex: ".jpg"). Não sensível a maiúsculas/minúsculas.</param>
    /// <param name="options">Filtros configurados para o escaneamento atual.</param>
    public static bool PassesFilter(long sizeBytes, string extension, ScanOptions options)
    {
        if (sizeBytes < options.MinFileSizeBytes)
        {
            return false;
        }

        var normalizedExtension = extension.ToLowerInvariant();

        if (options.ExcludeExtensions.Contains(normalizedExtension))
        {
            return false;
        }

        // Lista de inclusão vazia significa "aceitar qualquer extensão que não esteja excluída".
        if (options.IncludeExtensions.Count > 0 && !options.IncludeExtensions.Contains(normalizedExtension))
        {
            return false;
        }

        return true;
    }
}
