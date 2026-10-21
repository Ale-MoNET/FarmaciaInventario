using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using FarmaGestion.Models;

namespace FarmaGestion.Views.Converters;

/// <summary>
/// Colores de la píldora de estado de un lote: vigente (verde), por vencer (ámbar),
/// vencido (rojo), sin fecha (gris). Con <c>ConverterParameter="fg"</c> devuelve el texto.
/// </summary>
public class EstadoLoteColorConverter : IValueConverter
{
    public static readonly EstadoLoteColorConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var estado = value is EstadoLote e ? e : EstadoLote.SinFecha;
        bool foreground = parameter as string == "fg";

        (string bg, string fg) = estado switch
        {
            EstadoLote.Vencido => ("#24FF6B6B", "#FF8B8B"),
            EstadoLote.PorVencer => ("#24FFB03C", "#FFC061"),
            EstadoLote.Vigente => ("#1F34E0A1", "#5CE8B4"),
            _ => ("#14FFFFFF", "#8B96A4")
        };

        return SolidColorBrush.Parse(foreground ? fg : bg);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>Fecha corta (dd/MM/yyyy) o un guion cuando no hay fecha.</summary>
public class FechaCortaConverter : IValueConverter
{
    public static readonly FechaCortaConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is DateTime f ? f.ToString("dd/MM/yyyy") : "—";

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
