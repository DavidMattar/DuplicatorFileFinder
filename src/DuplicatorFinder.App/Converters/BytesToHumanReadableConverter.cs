using System.Globalization;
using System.Windows.Data;

namespace DuplicatorFinder.App.Converters;

/// <summary>
/// Converte um tamanho em bytes para uma string legível (ex: "1,25 GB"). Implementa
/// <see cref="IValueConverter"/> para ser usado em bindings XAML, e também expõe
/// <see cref="Format"/> como método estático para ser chamado direto de código C#
/// (ex: em ViewModels que montam mensagens de status).
/// </summary>
public sealed class BytesToHumanReadableConverter : IValueConverter
{
    private static readonly string[] Units = ["B", "KB", "MB", "GB", "TB"];

    /// <summary>Formata um tamanho em bytes na maior unidade que ainda resulte em um número >= 1.</summary>
    public static string Format(long bytes)
    {
        double size = bytes;
        var unitIndex = 0;

        while (size >= 1024 && unitIndex < Units.Length - 1)
        {
            size /= 1024;
            unitIndex++;
        }

        return $"{size.ToString("0.##", CultureInfo.InvariantCulture)} {Units[unitIndex]}";
    }

    /// <inheritdoc />
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is long bytes ? Format(bytes) : string.Empty;

    /// <inheritdoc />
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException("Este conversor só funciona em uma direção (bytes -> texto).");
}
