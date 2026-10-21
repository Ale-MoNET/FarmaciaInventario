using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace FarmaGestion.Views.Converters;

/// <summary>
/// Convierte las existencias (int) en un color según el nivel de stock:
/// agotado (rojo), bajo (ámbar) o normal (verde). Con <c>ConverterParameter="fg"</c>
/// devuelve el color del texto; en caso contrario, el color de fondo de la píldora.
/// </summary>
public class StockColorConverter : IValueConverter
{
    public static readonly StockColorConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        int existencias = value is int i ? i : 0;
        bool foreground = parameter as string == "fg";

        // (fondo, texto)
        (string bg, string fg) = existencias switch
        {
            0 => ("#24FF6B6B", "#FF8B8B"),      // agotado
            < 15 => ("#24FFB03C", "#FFC061"),   // bajo
            _ => ("#1F34E0A1", "#5CE8B4")        // normal
        };

        return SolidColorBrush.Parse(foreground ? fg : bg);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// Convierte las existencias en una etiqueta legible: "Agotado", "N u · bajo" o "N u".
/// </summary>
public class StockLabelConverter : IValueConverter
{
    public static readonly StockLabelConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        int n = value is int i ? i : 0;
        return n switch
        {
            0 => "Agotado",
            < 15 => $"{n} u · bajo",
            _ => $"{n} u"
        };
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
