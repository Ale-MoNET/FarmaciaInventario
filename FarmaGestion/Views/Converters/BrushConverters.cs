using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace FarmaGestion.Views.Converters;

/// <summary>Convierte una cadena de color (ej. "#34E0A1") en un pincel.</summary>
public class StringToBrushConverter : IValueConverter
{
    public static readonly StringToBrushConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var s = value as string;
        if (string.IsNullOrWhiteSpace(s)) return Brushes.Transparent;
        try { return SolidColorBrush.Parse(s); }
        catch { return Brushes.Transparent; }
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
