using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FarmaGestion.Models;

/// <summary>Estado de un lote respecto a su fecha de vencimiento.</summary>
public enum EstadoLote
{
    Vigente,
    /// <summary>Vence dentro de los próximos <see cref="Lote.DiasPorVencer"/> días.</summary>
    PorVencer,
    Vencido,
    /// <summary>Solo en lotes migrados desde la versión 1.0 que no tenían fecha registrada.</summary>
    SinFecha
}

/// <summary>
/// Lote físico de un medicamento: número de lote, fecha de vencimiento y existencias
/// propias. El stock de un producto es la suma de las existencias de sus lotes.
/// </summary>
public class Lote
{
    public const int DiasPorVencer = 90;

    public int Id { get; set; }

    public int MedicamentoId { get; set; }
    public Medicamentos Medicamento { get; set; } = null!;

    [Required(ErrorMessage = "El número de lote es obligatorio.")]
    [StringLength(100, MinimumLength = 1)]
    public string NumeroLote { get; set; } = string.Empty;

    /// <summary>
    /// Obligatoria al capturar. Solo puede ser nula en lotes creados por la migración
    /// desde la versión 1.0 para productos que no tenían fecha.
    /// </summary>
    public DateTime? FechaVencimiento { get; set; }

    [Range(0, int.MaxValue, ErrorMessage = "Las existencias no pueden ser negativas.")]
    public int Existencias { get; set; }

    public DateTime FechaIngreso { get; set; }
    public DateTime FechaUltimaActualizacion { get; set; }

    // ----- Propiedades calculadas (no se guardan en la base) -----

    [NotMapped] public bool Vencido => EstaVencido(DateTime.Today);

    [NotMapped] public EstadoLote Estado => CalcularEstado(DateTime.Today);

    [NotMapped]
    public string EstadoTexto
    {
        get
        {
            switch (Estado)
            {
                case EstadoLote.Vencido: return "Vencido";
                case EstadoLote.SinFecha: return "Sin fecha";
                case EstadoLote.PorVencer:
                    int dias = (FechaVencimiento!.Value.Date - DateTime.Today).Days;
                    return dias switch { 0 => "Vence hoy", 1 => "Vence mañana", _ => $"Vence en {dias} días" };
                default: return "Vigente";
            }
        }
    }

    public bool EstaVencido(DateTime hoy)
        => FechaVencimiento.HasValue && FechaVencimiento.Value.Date < hoy.Date;

    public EstadoLote CalcularEstado(DateTime hoy)
    {
        if (FechaVencimiento is null) return EstadoLote.SinFecha;
        int dias = (FechaVencimiento.Value.Date - hoy.Date).Days;
        if (dias < 0) return EstadoLote.Vencido;
        if (dias <= DiasPorVencer) return EstadoLote.PorVencer;
        return EstadoLote.Vigente;
    }
}
