namespace DuplicatorFinder.Core.Support;

/// <summary>
/// Classifica arquivos por extensão em categorias amplas (imagem, vídeo). Compartilhado entre
/// o <see cref="Engine.DuplicateScanEngine"/> (para decidir quais arquivos alimentam cada
/// detector) e a camada de apresentação (ex: para decidir se vale a pena oferecer um preview
/// visual de um grupo de duplicados) — existir aqui, em vez de duplicado em cada lugar que
/// precisa da lista de extensões, evita as duas listas divergirem com o tempo.
/// </summary>
public static class FileTypeClassifier
{
    private static readonly HashSet<string> ImageExtensions =
        [".jpg", ".jpeg", ".png", ".bmp", ".gif", ".webp", ".tif", ".tiff"];

    private static readonly HashSet<string> VideoExtensions =
        [".mp4", ".mkv", ".avi", ".mov", ".wmv", ".flv", ".webm", ".m4v"];

    /// <summary>Verdadeiro se a extensão informada (com ou sem o ponto, não sensível a maiúsculas/minúsculas) é de um formato de imagem suportado.</summary>
    public static bool IsImageExtension(string extension) => ImageExtensions.Contains(Normalize(extension));

    /// <summary>Verdadeiro se a extensão informada (com ou sem o ponto, não sensível a maiúsculas/minúsculas) é de um formato de vídeo suportado.</summary>
    public static bool IsVideoExtension(string extension) => VideoExtensions.Contains(Normalize(extension));

    private static string Normalize(string extension)
    {
        var lower = extension.ToLowerInvariant();
        return lower.StartsWith('.') ? lower : $".{lower}";
    }
}
