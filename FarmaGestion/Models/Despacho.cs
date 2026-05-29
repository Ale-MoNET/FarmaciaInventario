using System;
using System.Collections.Generic;

namespace FarmaGestion.Models;

public class Despacho
{
    public int Id { get; set; }
    public DateTime Fecha { get; set; }
    public decimal Total { get; set; }
    
    // Relación uno a muchos: Un despacho tiene varios detalles
    public List<DetalleDespacho> Detalles { get; set; } = new();
}
public class DetalleDespacho
{
    public int Id { get; set; }
    
    // Llave foránea hacia el Despacho
    public int DespachoId { get; set; }
    public Despacho Despacho { get; set; }
    
    // Llave foránea hacia el Medicamento
    public int MedicamentoId { get; set; }
    public Medicamentos Medicamento { get; set; }
    
    public int Cantidad { get; set; }
    public decimal PrecioHistorico { get; set; } // Precio al momento de la venta
}