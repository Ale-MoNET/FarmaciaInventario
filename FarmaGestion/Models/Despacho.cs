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

    /// <summary>
    /// Lote del que salieron las unidades. Un producto puede generar varios detalles en el
    /// mismo despacho (uno por lote). Es nulo en despachos anteriores a la versión 1.1.
    /// </summary>
    public int? LoteId { get; set; }
    public Lote? Lote { get; set; }
    
    public int Cantidad { get; set; }
    public decimal PrecioHistorico { get; set; } // Precio al momento de la venta
}
