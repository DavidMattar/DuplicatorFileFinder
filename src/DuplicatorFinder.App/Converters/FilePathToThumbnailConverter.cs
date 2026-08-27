using System.Globalization;
using System.Windows.Data;
using System.Windows.Media.Imaging;

namespace DuplicatorFinder.App.Converters;

/// <summary>
/// Converte o caminho de um arquivo em uma miniatura pequena (<see cref="BitmapImage"/>) para
/// exibição na lista de resultados. Retorna null silenciosamente para qualquer arquivo que
/// não seja uma imagem decodificável (outro tipo de arquivo, corrompido, sem permissão) — a
/// UI simplesmente não mostra nada nesse caso, em vez de quebrar o binding.
/// Decodifica de forma síncrona e sem cache entre chamadas: aceitável para a quantidade
/// típica de resultados de um escaneamento (dezenas a poucas centenas de grupos); um
/// carregamento assíncrono com cache LRU é uma melhoria de polish para uma versão futura,
/// caso escaneamentos com muitos milhares de imagens duplicadas tornem isso perceptível.
/// </summary>
public sealed class FilePathToThumbnailConverter : IValueConverter
{
    private const int ThumbnailPixelWidth = 48;

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string path)
        {
            return null;
        }

        try
        {
            var bitmap = new BitmapImage
            {
                CacheOption = BitmapCacheOption.OnLoad,
                DecodePixelWidth = ThumbnailPixelWidth,
                UriSource = new Uri(path),
            };
            bitmap.Freeze();
            return bitmap;
        }
        catch (Exception)
        {
            // Amplamente capturado de propósito: qualquer falha de decodificação aqui deve
            // resultar em "sem miniatura", nunca em quebrar o binding da UI inteira.
            return null;
        }
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException("Este conversor só funciona em uma direção (caminho -> miniatura).");
}
