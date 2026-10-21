using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;

namespace FarmaGestion.Models;

public class Medicamentos
{
    public int Id { get; set; }

    [Required(ErrorMessage = "El código de barras es obligatorio.")]
    [StringLength(50, MinimumLength = 1)]
    public string CodigoBarras { get; set; } = string.Empty;

    [Required(ErrorMessage = "El nombre es obligatorio.")]
    [StringLength(200, MinimumLength = 1)]
    public string Nombre { get; set; } = string.Empty;

    [Range(0, double.MaxValue, ErrorMessage = "El precio no puede ser negativo.")]
    public decimal Precio { get; set; }

    public DateTime FechaUltimaActualizacion { get; set; }

    /// <summary>Lotes del producto. Las existencias viven aquí, no en el producto.</summary>
    public List<Lote> Lotes { get; set; } = new();

    // ----- Propiedades calculadas a partir de los lotes (no se guardan en la base) -----

    /// <summary>Existencias totales: suma de todos los lotes (incluye vencidos).</summary>
    [NotMapped] public int Existencias => Lotes.Sum(l => l.Existencias);

    /// <summary>Unidades en lotes ya vencidos.</summary>
    [NotMapped] public int ExistenciasVencidas => Lotes.Where(l => l.Vencido).Sum(l => l.Existencias);

    /// <summary>Lotes con existencias, del vencimiento más cercano al más lejano.</summary>
    [NotMapped]
    public IEnumerable<Lote> LotesActivos => Lotes
        .Where(l => l.Existencias > 0)
        .OrderBy(l => l.FechaVencimiento ?? DateTime.MaxValue)
        .ThenBy(l => l.NumeroLote);

    [NotMapped] public int NumeroLotes => Lotes.Count(l => l.Existencias > 0);

    [NotMapped] public bool SinLotes => NumeroLotes == 0;

    /// <summary>Lote con existencias que vence primero (null si no hay lotes con fecha).</summary>
    [NotMapped]
    public Lote? ProximoLote => LotesActivos.FirstOrDefault(l => l.FechaVencimiento != null);

    [NotMapped] public DateTime? ProximoVencimiento => ProximoLote?.FechaVencimiento;

    /// <summary>Estado del lote que vence primero; SinFecha cuando no hay lotes con fecha.</summary>
    [NotMapped] public EstadoLote EstadoProximo => ProximoLote?.Estado ?? EstadoLote.SinFecha;
}
