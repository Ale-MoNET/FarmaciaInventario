using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FarmaGestion.Data.Repositories;

/// <summary>Una línea solicitada para el despacho.</summary>
public record LineaDespacho(int MedicamentoId, int Cantidad);

/// <summary>Resultado de confirmar un despacho.</summary>
public record ResultadoDespacho(bool Exito, string Mensaje, int? DespachoId = null);

/// <summary>Una línea del comprobante, con el precio al que realmente se vendió.</summary>
public record LineaComprobante(string Codigo, string Nombre, int Cantidad, decimal PrecioUnitario)
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

    public int Unidades => Lineas.Sum(l => l.Cantidad);
}

/// <summary>Resumen de un producto en el periodo (unidades e ingreso).</summary>
public record ProductoResumen(string Nombre, int Unidades, decimal Ingreso);

/// <summary>Un despacho con su conteo de unidades para el reporte.</summary>
public record DespachoReporte(int Id, DateTime Fecha, int Unidades, decimal Total);

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
    /// Confirma un despacho dentro de una transacción: valida stock, rebaja inventario
    /// y registra el despacho con sus detalles. Si algo falla, no se realiza ningún cambio.
    /// </summary>
    Task<ResultadoDespacho> ConfirmarAsync(IEnumerable<LineaDespacho> lineas);

    /// <summary>
    /// Recupera un despacho ya registrado con el detalle de sus líneas para armar el
    /// comprobante. Devuelve null si el despacho no existe.
    /// </summary>
    Task<ComprobanteDespacho?> ObtenerComprobanteAsync(int despachoId);

    Task<ReporteVentas> GenerarReporteAsync(DateTime inicio, DateTime fin);
}
