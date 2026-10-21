using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace FarmaGestion.Views.Converters;

/// <summary>Fondo del botón de filtro por rol: resaltado si es el filtro activo.</summary>
public class RoleFilterBgConverter : IValueConverter
{
    public static readonly RoleFilterBgConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        bool activo = string.Equals(value as string, parameter as string, StringComparison.Ordinal);
        return SolidColorBrush.Parse(activo ? "#1F34E0A1" : "#00000000");
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>Color de texto del botón de filtro por rol.</summary>
public class RoleFilterFgConverter : IValueConverter
{
    public static readonly RoleFilterFgConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        bool activo = string.Equals(value as string, parameter as string, StringComparison.Ordinal);
        return SolidColorBrush.Parse(activo ? "#5CE8B4" : "#8B96A4");
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
