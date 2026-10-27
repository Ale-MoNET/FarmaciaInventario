using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FarmaGestion.Data.Repositories;

/// <summary>
/// Una línea solicitada para el despacho. <paramref name="LoteId"/> fuerza un lote concreto;
/// si es nulo, las unidades salen por regla FEFO.
/// </summary>
public record LineaDespacho(int MedicamentoId, int Cantidad, int? LoteId = null);

/// <summary>Resultado de confirmar un despacho.</summary>
public record ResultadoDespacho(bool Exito, string Mensaje, int? DespachoId = null)
{
    /// <summary>Avisos no bloqueantes, p. ej. unidades que salieron de lotes vencidos.</summary>
    public IReadOnlyList<string> Advertencias { get; init; } = Array.Empty<string>();
}

/// <summary>
/// Una línea del comprobante, con el precio al que realmente se vendió y el lote del que
/// salió (nulo en despachos anteriores a la versión 1.1).
/// </summary>
public record LineaComprobante(string Codigo, string Nombre, int Cantidad, decimal PrecioUnitario, string? Lote = null)
{
    public decimal Subtotal => PrecioUnitario * Cantidad;
}

/// <summary>
/// Datos de un despacho ya registrado, listos para mostrarse en el comprobante,
/// exportarse a PDF o imprimirse.
/// </summary>
public record ComprobanteDespacho(int Id, DateTime Fecha, decimal Total, IReadOnlyList<LineaComprobante> Lineas)
{
    /// <summary>Usuario que atendió el despacho; lo completa el ViewModel desde la sesión.</summary>
    public string Atendio { get; init; } = string.Empty;

    /// <summary>True si el despacho fue deshecho (las unidades volvieron al inventario).</summary>
    public bool Anulado { get; init; }
    public DateTime? FechaAnulacion { get; init; }
    public string? MotivoAnulacion { get; init; }
    public string? UsuarioAnulacion { get; init; }

    /// <summary>
    /// False en despachos anteriores a la versión 1.1 (sin lote por línea): no se sabe a qué
    /// lote devolver las unidades, así que no pueden deshacerse automáticamente.
    /// </summary>
    public bool TieneLotes => Lineas.Count > 0 && Lineas.All(l => l.Lote != null);

    public int Unidades => Lineas.Sum(l => l.Cantidad);
}

/// <summary>Resumen de un producto en el periodo (unidades e ingreso).</summary>
public record ProductoResumen(string Nombre, int Unidades, decimal Ingreso);

/// <summary>Un despacho con su conteo de unidades para el reporte. Los anulados se listan pero no suman.</summary>
public record DespachoReporte(int Id, DateTime Fecha, int Unidades, decimal Total, bool Anulado = false);

/// <summary>Total de ventas de un día (para el gráfico de barras).</summary>
public record VentaDia(DateTime Dia, decimal Total);

/// <summary>Datos crudos del reporte de ventas; la presentación se arma en el ViewModel.</summary>
public class ReporteVentas
{
    public decimal VentasTotales { get; init; }
    public int NumDespachos { get; init; }
    public int Unidades { get; init; }
    public decimal Ticket { get; init; }
    public ProductoResumen? Top { get; init; }
    public ProductoResumen? Bottom { get; init; }
    public List<DespachoReporte> Despachos { get; init; } = new();
    public List<VentaDia> PorDia { get; init; } = new();
}

public interface IDespachoRepository
{
    /// <summary>
    /// Confirma un despacho dentro de una transacción: valida stock, rebaja los lotes por
    /// regla FEFO (vence primero, sale primero) y registra el despacho con un detalle por
    /// lote. Si algo falla, no se realiza ningún cambio.
    /// </summary>
    Task<ResultadoDespacho> ConfirmarAsync(IEnumerable<LineaDespacho> lineas);

    /// <summary>
    /// Recupera un despacho ya registrado con el detalle de sus líneas para armar el
    /// comprobante (una línea por producto y lote). Devuelve null si el despacho no existe.
    /// </summary>
    Task<ComprobanteDespacho?> ObtenerComprobanteAsync(int despachoId);

    /// <summary>
    /// Deshace un despacho dentro de una transacción: devuelve cada línea al lote del que
    /// salió, marca el despacho como anulado (con motivo, usuario y fecha) y lo excluye de
    /// los reportes. No se puede anular dos veces ni anular despachos sin lote por línea
    /// (anteriores a la versión 1.1).
    /// </summary>
    Task<ResultadoOperacion> AnularAsync(int despachoId, string motivo, string usuario);

    /// <summary>Ventas del periodo. Los despachos anulados aparecen en la lista marcados, pero no suman.</summary>
    Task<ReporteVentas> GenerarReporteAsync(DateTime inicio, DateTime fin);
}
