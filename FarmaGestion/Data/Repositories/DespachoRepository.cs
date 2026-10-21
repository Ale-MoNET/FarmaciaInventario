using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FarmaGestion.Models;
using FarmaGestion.Services;
using Microsoft.EntityFrameworkCore;

namespace FarmaGestion.Data.Repositories;

public class DespachoRepository : IDespachoRepository
{
    private readonly IDbContextFactory<InventarioContext> _factory;

    public DespachoRepository(IDbContextFactory<InventarioContext> factory)
    {
        _factory = factory;
    }

    public async Task<ResultadoDespacho> ConfirmarAsync(IEnumerable<LineaDespacho> lineas)
    {
        var items = lineas.ToList();
        if (items.Count == 0)
            return new ResultadoDespacho(false, "⚠️ El carrito está vacío, no se puede despachar.");

        await using var context = await _factory.CreateDbContextAsync();
        await using var transaccion = await context.Database.BeginTransactionAsync();

        try
        {
            var despacho = new Despacho { Fecha = DateTime.Now, Total = 0m };
            decimal total = 0m;

            foreach (var linea in items)
            {
                var producto = await context.Medicamentos.FindAsync(linea.MedicamentoId);

                if (producto == null)
                {
                    await transaccion.RollbackAsync();
                    return new ResultadoDespacho(false,
                        "❌ Uno de los productos ya no existe en el inventario.");
                }

                // Revalidamos las existencias en este instante (pudieron cambiar).
                if (producto.Existencias < linea.Cantidad)
                {
                    await transaccion.RollbackAsync();
                    return new ResultadoDespacho(false,
                        $"❌ Stock insuficiente de '{producto.Nombre}' " +
                        $"(disponibles: {producto.Existencias}, solicitados: {linea.Cantidad}).");
                }

                producto.Existencias -= linea.Cantidad;
                total += producto.Precio * linea.Cantidad;

                despacho.Detalles.Add(new DetalleDespacho
                {
                    MedicamentoId = producto.Id,
                    Cantidad = linea.Cantidad,
                    PrecioHistorico = producto.Precio
                });
            }

            despacho.Total = total;
            context.Despachos.Add(despacho);
            await context.SaveChangesAsync();
            await transaccion.CommitAsync();

            LoggerService.Info($"Despacho #{despacho.Id} confirmado por un total de {despacho.Total:C}.");
            return new ResultadoDespacho(true, "✅ Despacho confirmado correctamente.", despacho.Id);
        }
        catch (Exception ex)
        {
            await transaccion.RollbackAsync();
            LoggerService.Error("Error al confirmar el despacho; se revirtió la operación", ex);
            return new ResultadoDespacho(false,
                "❌ Ocurrió un error al confirmar el despacho. No se realizó ningún cambio.");
        }
    }

    public async Task<ComprobanteDespacho?> ObtenerComprobanteAsync(int despachoId)
    {
        await using var context = await _factory.CreateDbContextAsync();

        var despacho = await context.Despachos
            .AsNoTracking()
            .Where(d => d.Id == despachoId)
            .Select(d => new
            {
                d.Id,
                d.Fecha,
                d.Total,
                Lineas = d.Detalles
                    .OrderBy(x => x.Id)
                    .Select(x => new LineaComprobante(
                        x.Medicamento.CodigoBarras,
                        x.Medicamento.Nombre,
                        x.Cantidad,
                        x.PrecioHistorico))
                    .ToList()
            })
            .FirstOrDefaultAsync();

        if (despacho == null)
            return null;

        return new ComprobanteDespacho(despacho.Id, despacho.Fecha, despacho.Total, despacho.Lineas);
    }

    public async Task<ReporteVentas> GenerarReporteAsync(DateTime inicio, DateTime fin)
    {
        await using var context = await _factory.CreateDbContextAsync();

        // Despachos del periodo, con su conteo de unidades.
        var despachos = await context.Despachos
            .Where(d => d.Fecha >= inicio && d.Fecha <= fin)
            .OrderByDescending(d => d.Fecha)
            .Select(d => new DespachoReporte(
                d.Id,
                d.Fecha,
                d.Detalles.Sum(x => (int?)x.Cantidad) ?? 0,
                d.Total))
            .ToListAsync();

        // Agregado por producto dentro del periodo (unidades e ingreso histórico).
        var porProducto = await context.DetallesDespacho
            .Where(x => x.Despacho.Fecha >= inicio && x.Despacho.Fecha <= fin)
            .GroupBy(x => x.Medicamento.Nombre)
            .Select(g => new ProductoResumen(
                g.Key,
                g.Sum(x => x.Cantidad),
                g.Sum(x => x.PrecioHistorico * x.Cantidad)))
            .ToListAsync();

        // Ventas agrupadas por día (para el gráfico de barras), en memoria.
        var porDia = despachos
            .GroupBy(d => d.Fecha.Date)
            .OrderBy(g => g.Key)
            .Select(g => new VentaDia(g.Key, g.Sum(x => x.Total)))
            .ToList();

        var ventasTotales = despachos.Sum(d => d.Total);
        var numDespachos = despachos.Count;
        var unidades = despachos.Sum(d => d.Unidades);

        ProductoResumen? top = null, bottom = null;
        if (porProducto.Count > 0)
        {
            top = porProducto.OrderByDescending(p => p.Unidades).First();
            bottom = porProducto.OrderBy(p => p.Unidades).First();
        }

        return new ReporteVentas
        {
            VentasTotales = ventasTotales,
            NumDespachos = numDespachos,
            Unidades = unidades,
            Ticket = numDespachos > 0 ? ventasTotales / numDespachos : 0m,
            Top = top,
            Bottom = bottom,
            Despachos = despachos,
            PorDia = porDia
        };
    }
}
