using System;
using System.ComponentModel.DataAnnotations;

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

    [Range(0, int.MaxValue, ErrorMessage = "Las existencias no pueden ser negativas.")]
    public int Existencias { get; set; }

    public DateTime FechaUltimaActualizacion { get; set; }

    // Campos adicionales solicitados
    [StringLength(100)]
    public string? Lote { get; set; }

    public DateTime? FechaVencimiento { get; set; }
}
