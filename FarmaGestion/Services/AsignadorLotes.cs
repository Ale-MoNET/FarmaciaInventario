using System;
using System.Collections.Generic;
using System.Linq;
using FarmaGestion.Models;

namespace FarmaGestion.Services;

/// <summary>Unidades que salen de un lote concreto dentro de un despacho.</summary>
public sealed record AsignacionLote(Lote Lote, int Cantidad);

/// <summary>Resultado de repartir una cantidad solicitada entre los lotes de un producto.</summary>
public sealed record ResultadoAsignacion(IReadOnlyList<AsignacionLote> Asignaciones, int Faltante, DateTime Hoy)
{
    /// <summary>True si los lotes alcanzaron para cubrir toda la cantidad.</summary>
    public bool Completa => Faltante == 0;

    /// <summary>Unidades que saldrían de lotes ya vencidos.</summary>
    public int UnidadesVencidas => Asignaciones.Where(a => a.Lote.EstaVencido(Hoy)).Sum(a => a.Cantidad);

    public IEnumerable<string> LotesVencidos => Asignaciones
        .Where(a => a.Lote.EstaVencido(Hoy))
        .Select(a => a.Lote.NumeroLote);

    /// <summary>Texto corto del reparto, p. ej. "L2026A01 (80) + L2026B14 (20)".</summary>
    public string Descripcion => string.Join(" + ", Asignaciones.Select(a => $"{a.Lote.NumeroLote} ({a.Cantidad})"));
}

/// <summary>
/// Regla FEFO (first expired, first out): las unidades salen primero del lote vigente que
/// vence antes. Los lotes sin fecha van después y los vencidos al final, de modo que solo
/// se despachan unidades vencidas cuando no queda nada más (y entonces se advierte).
/// </summary>
public static class AsignadorLotes
{
    public static IEnumerable<Lote> OrdenarFefo(IEnumerable<Lote> lotes, DateTime hoy) => lotes
        .Where(l => l.Existencias > 0)
        .OrderBy(l => l.EstaVencido(hoy) ? 2 : l.FechaVencimiento is null ? 1 : 0)
        .ThenBy(l => l.FechaVencimiento ?? DateTime.MaxValue)
        .ThenBy(l => l.Id);

    public static ResultadoAsignacion Asignar(IEnumerable<Lote> lotes, int cantidad, DateTime hoy)
    {
        var asignaciones = new List<AsignacionLote>();
        int restante = Math.Max(0, cantidad);

        foreach (var lote in OrdenarFefo(lotes, hoy))
        {
            if (restante == 0) break;
            int tomar = Math.Min(lote.Existencias, restante);
            asignaciones.Add(new AsignacionLote(lote, tomar));
            restante -= tomar;
        }

        return new ResultadoAsignacion(asignaciones, restante, hoy);
    }
}
