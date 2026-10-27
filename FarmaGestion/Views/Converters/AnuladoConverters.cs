using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace FarmaGestion.Views.Converters;

/// <summary>Atenúa la fila de un despacho deshecho.</summary>
public class AnuladoOpacidadConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true ? 0.55 : 1.0;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>Tacha el total de un despacho deshecho.</summary>
public class AnuladoTachadoConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true ? TextDecorations.Strikethrough : null;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
