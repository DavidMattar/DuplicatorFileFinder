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
    private const int DefaultPixelWidth = 48;

    /// <summary>
    /// Largura de decodificação em pixels: usa <see cref="DefaultPixelWidth"/> (miniatura da
    /// lista de resultados) a menos que o binding informe um <c>ConverterParameter</c> com um
    /// número maior (usado pela janela de preview, que quer imagens bem maiores) — reaproveita
    /// o mesmo decode-com-fallback-silencioso em vez de duplicar essa lógica em outro conversor.
    /// </summary>
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string path)
        {
            return null;
        }

        var pixelWidth = parameter is string parameterText && int.TryParse(parameterText, out var requested)
            ? requested
            : DefaultPixelWidth;

        try
        {
            // BeginInit/EndInit explícitos são necessários aqui: preencher as mesmas
            // propriedades via inicializador de objeto (sem Begin/EndInit) deixa o BitmapImage
            // num estado "nunca finalizado" que não lança nem em Convert() nem no binding, só
            // falha silenciosamente ao ser efetivamente desenhado — só se nota olhando a tela
            // renderizada de verdade, nunca num teste automatizado com mocks.
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.DecodePixelWidth = pixelWidth;
            bitmap.UriSource = new Uri(path);
            bitmap.EndInit();
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
