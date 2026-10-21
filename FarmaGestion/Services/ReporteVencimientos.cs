using System;
using System.Collections.Generic;
using System.Linq;
using FarmaGestion.Models;

namespace FarmaGestion.Services;

/// <summary>
/// Reglas del reporte de vencimientos. El horizonte es: 0 = solo vencidos,
/// N > 0 = lotes que vencen dentro de N días (incluye hoy), -1 = todos los lotes con existencias.
/// </summary>
public static class ReporteVencimientos
{
    public const int Vencidos = 0;
    public const int Todos = -1;

    /// <summary>Días que faltan para vencer (negativo si ya venció; null si el lote no tiene fecha).</summary>
    public static int? DiasParaVencer(Lote lote, DateTime hoy)
        => lote.FechaVencimiento is null ? null : (lote.FechaVencimiento.Value.Date - hoy.Date).Days;

    public static IEnumerable<Lote> Filtrar(IEnumerable<Lote> lotes, int horizonte, DateTime hoy)
    {
        var conStock = lotes.Where(l => l.Existencias > 0);

        if (horizonte == Todos)
            return conStock.OrderBy(l => l.FechaVencimiento ?? DateTime.MaxValue).ThenBy(l => l.Medicamento?.Nombre);

        if (horizonte == Vencidos)
            return conStock.Where(l => DiasParaVencer(l, hoy) < 0).OrderBy(l => l.FechaVencimiento);

        return conStock
            .Where(l => DiasParaVencer(l, hoy) is int d && d >= 0 && d <= horizonte)
            .OrderBy(l => l.FechaVencimiento);
    }

    /// <summary>Conteos para las pestañas del reporte (los rangos de días incluyen los menores).</summary>
    public static (int Vencidos, int En30, int En60, int En90) Contar(IEnumerable<Lote> lotes, DateTime hoy)
    {
        int vencidos = 0, en30 = 0, en60 = 0, en90 = 0;
        foreach (var lote in lotes.Where(l => l.Existencias > 0))
        {
            var dias = DiasParaVencer(lote, hoy);
            if (dias is null) continue;
            if (dias < 0) { vencidos++; continue; }
            if (dias <= 30) en30++;
            if (dias <= 60) en60++;
            if (dias <= 90) en90++;
        }
        return (vencidos, en30, en60, en90);
    }

    /// <summary>Texto corto para la columna "Días": "Vencido hace 3 días", "Hoy", "12 días", "Sin fecha".</summary>
    public static string TextoDias(Lote lote, DateTime hoy)
    {
        var dias = DiasParaVencer(lote, hoy);
        if (dias is null) return "Sin fecha";
        if (dias < 0) return $"Hace {-dias} día{(dias == -1 ? "" : "s")}";
        if (dias == 0) return "Hoy";
        return $"{dias} día{(dias == 1 ? "" : "s")}";
    }
}
