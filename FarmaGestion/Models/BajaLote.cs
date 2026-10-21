using System;
using System.ComponentModel.DataAnnotations;

namespace FarmaGestion.Models;

/// <summary>Motivos disponibles al retirar unidades de un lote.</summary>
public static class MotivosBaja
{
    public const string Vencido = "Vencido";
    public const string Danado = "Dañado";
    public const string Perdida = "Pérdida o robo";
    public const string Ajuste = "Ajuste de inventario";
    public const string Otro = "Otro";

    public static readonly string[] Todos = { Vencido, Danado, Perdida, Ajuste, Otro };
}

/// <summary>
/// Registro de una baja de inventario: unidades retiradas de un lote con su motivo,
/// quién la hizo y cuándo. Da trazabilidad a las salidas que no son ventas.
/// </summary>
public class BajaLote
{
    public int Id { get; set; }

    public int LoteId { get; set; }
    public Lote Lote { get; set; } = null!;

    public int MedicamentoId { get; set; }
    public Medicamentos Medicamento { get; set; } = null!;

    [Range(1, int.MaxValue)]
    public int Cantidad { get; set; }

    [Required, StringLength(50)]
    public string Motivo { get; set; } = string.Empty;

    [StringLength(300)]
    public string? Observacion { get; set; }

    public DateTime Fecha { get; set; }

    [StringLength(100)]
    public string Usuario { get; set; } = string.Empty;
}
