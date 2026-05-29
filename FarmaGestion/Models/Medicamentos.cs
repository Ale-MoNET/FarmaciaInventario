using System;
using Avalonia.Controls;

namespace FarmaGestion.Models;

public class Medicamentos
{
    public int Id { get; set; }
    public string CodigoBarras { get; set; } 
    public string Nombre { get; set; }
    public decimal Precio { get; set; }
    public int Existencias { get; set; }
    public DateTime FechaUltimaActualizacion { get; set; }
}