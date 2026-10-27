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
            var ahora = DateTime.Now;
            var hoy = ahora.Date;
            var despacho = new Despacho { Fecha = ahora, Total = 0m };
            decimal total = 0m;
            var advertencias = new List<string>();

            foreach (var linea in items)
            {
                var producto = await context.Medicamentos
                    .Include(m => m.Lotes)
                    .FirstOrDefaultAsync(m => m.Id == linea.MedicamentoId);

                if (producto == null)
                {
                    await transaccion.RollbackAsync();
                    return new ResultadoDespacho(false,
                        "❌ Uno de los productos ya no existe en el inventario.");
                }

                // Repartimos la cantidad entre los lotes con las existencias de este instante:
                // por FEFO, o solo del lote que eligió la cajera.
                ResultadoAsignacion asignacion;
                if (linea.LoteId is int loteElegidoId)
                {
                    var loteElegido = producto.Lotes.FirstOrDefault(l => l.Id == loteElegidoId);
                    if (loteElegido == null)
                    {
                        await transaccion.RollbackAsync();
                        return new ResultadoDespacho(false,
                            $"❌ El lote elegido para '{producto.Nombre}' ya no existe. Vuelve a agregar el producto.");
                    }

                    asignacion = AsignadorLotes.Asignar(new[] { loteElegido }, linea.Cantidad, hoy);
                    if (!asignacion.Completa)
                    {
                        await transaccion.RollbackAsync();
                        return new ResultadoDespacho(false,
                            $"❌ El lote {loteElegido.NumeroLote} de '{producto.Nombre}' solo tiene " +
                            $"{loteElegido.Existencias} u (solicitados: {linea.Cantidad}).");
                    }
                }
                else
                {
                    asignacion = AsignadorLotes.Asignar(producto.Lotes, linea.Cantidad, hoy);
                    if (!asignacion.Completa)
                    {
                        await transaccion.RollbackAsync();
                        return new ResultadoDespacho(false,
                            $"❌ Stock insuficiente de '{producto.Nombre}' " +
                            $"(disponibles: {producto.Existencias}, solicitados: {linea.Cantidad}).");
                    }
                }

                foreach (var parte in asignacion.Asignaciones)
                {
                    parte.Lote.Existencias -= parte.Cantidad;
                    parte.Lote.FechaUltimaActualizacion = ahora;

                    despacho.Detalles.Add(new DetalleDespacho
                    {
                        MedicamentoId = producto.Id,
                        LoteId = parte.Lote.Id,
                        Cantidad = parte.Cantidad,
                        PrecioHistorico = producto.Precio
                    });
                }

                if (asignacion.UnidadesVencidas > 0)
                {
                    advertencias.Add(
                        $"⚠️ {asignacion.UnidadesVencidas} u de '{producto.Nombre}' salieron de lotes vencidos " +
                        $"({string.Join(", ", asignacion.LotesVencidos)}).");
                }

                producto.FechaUltimaActualizacion = ahora;
                total += producto.Precio * linea.Cantidad;
            }

            despacho.Total = total;
            context.Despachos.Add(despacho);
            await context.SaveChangesAsync();
            await transaccion.CommitAsync();

            LoggerService.Info($"Despacho #{despacho.Id} confirmado por un total de {despacho.Total:C}.");
            return new ResultadoDespacho(true, "✅ Despacho confirmado correctamente.", despacho.Id)
            {
                Advertencias = advertencias
            };
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
            .Select(d => new { d.Id, d.Fecha, d.Total, d.Anulado, d.FechaAnulacion, d.MotivoAnulacion, d.UsuarioAnulacion })
            .FirstOrDefaultAsync();

        if (despacho == null)
            return null;

        var detalles = await context.DetallesDespacho
            .AsNoTracking()
            .Where(x => x.DespachoId == despachoId)
            .OrderBy(x => x.Id)
            .Select(x => new
            {
                x.Id,
                x.MedicamentoId,
                x.Medicamento.CodigoBarras,
                x.Medicamento.Nombre,
                x.Cantidad,
                x.PrecioHistorico,
                x.LoteId,
                NumeroLote = x.Lote != null ? x.Lote.NumeroLote : null
            })
            .ToListAsync();

        // Una línea por producto y lote (trazabilidad); los despachos anteriores a la versión
        // 1.1 no tienen lote y quedan agrupados por producto.
        var lineas = detalles
            .GroupBy(x => new { x.MedicamentoId, x.PrecioHistorico, x.LoteId })
            .OrderBy(g => g.Min(x => x.Id))
            .Select(g => new LineaComprobante(
                g.First().CodigoBarras,
                g.First().Nombre,
                g.Sum(x => x.Cantidad),
                g.Key.PrecioHistorico,
                g.First().NumeroLote))
            .ToList();

        return new ComprobanteDespacho(despacho.Id, despacho.Fecha, despacho.Total, lineas)
        {
            Anulado = despacho.Anulado,
            FechaAnulacion = despacho.FechaAnulacion,
            MotivoAnulacion = despacho.MotivoAnulacion,
            UsuarioAnulacion = despacho.UsuarioAnulacion
        };
    }

    public async Task<ResultadoOperacion> AnularAsync(int despachoId, string motivo, string usuario)
    {
        if (string.IsNullOrWhiteSpace(motivo))
            return new ResultadoOperacion(false, "Indica el motivo para deshacer el despacho.");

        await using var context = await _factory.CreateDbContextAsync();
        await using var transaccion = await context.Database.BeginTransactionAsync();

        try
        {
            var despacho = await context.Despachos
                .Include(d => d.Detalles).ThenInclude(x => x.Lote)
                .Include(d => d.Detalles).ThenInclude(x => x.Medicamento)
                .FirstOrDefaultAsync(d => d.Id == despachoId);

            if (despacho == null)
                return new ResultadoOperacion(false, $"El despacho #{despachoId} no existe.");

            if (despacho.Anulado)
                return new ResultadoOperacion(false,
                    $"El despacho #{despachoId} ya fue deshecho el {despacho.FechaAnulacion:dd/MM/yyyy HH:mm}.");

            if (despacho.Detalles.Count == 0 || despacho.Detalles.Any(x => x.Lote == null))
                return new ResultadoOperacion(false,
                    $"El despacho #{despachoId} es anterior al control por lotes y no se puede deshacer " +
                    "automáticamente. Si hace falta, reingresa las unidades desde Ingreso de mercadería.");

            var ahora = DateTime.Now;
            foreach (var detalle in despacho.Detalles)
            {
                detalle.Lote!.Existencias += detalle.Cantidad;
                detalle.Lote.FechaUltimaActualizacion = ahora;
                detalle.Medicamento.FechaUltimaActualizacion = ahora;
            }

            despacho.Anulado = true;
            despacho.FechaAnulacion = ahora;
            despacho.MotivoAnulacion = motivo.Trim();
            despacho.UsuarioAnulacion = usuario;

            await context.SaveChangesAsync();
            await transaccion.CommitAsync();

            int unidades = despacho.Detalles.Sum(x => x.Cantidad);
            LoggerService.Info($"Despacho #{despacho.Id} deshecho por {usuario}: {unidades} u devueltas al inventario. Motivo: {despacho.MotivoAnulacion}");
            return new ResultadoOperacion(true,
                $"✅ Despacho #{despacho.Id} deshecho: {unidades} unidad{(unidades == 1 ? "" : "es")} volvieron al inventario.");
        }
        catch (Exception ex)
        {
            await transaccion.RollbackAsync();
            LoggerService.Error($"Error al deshacer el despacho #{despachoId}; se revirtió la operación", ex);
            return new ResultadoOperacion(false, "❌ No se pudo deshacer el despacho. No se realizó ningún cambio.");
        }
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
                d.Total,
                d.Anulado))
            .ToListAsync();

        // Los despachos deshechos se listan (trazabilidad) pero no cuentan como venta.
        var vigentes = despachos.Where(d => !d.Anulado).ToList();

        // Agregado por producto dentro del periodo (unidades e ingreso histórico).
        var porProducto = await context.DetallesDespacho
            .Where(x => x.Despacho.Fecha >= inicio && x.Despacho.Fecha <= fin && !x.Despacho.Anulado)
            .GroupBy(x => x.Medicamento.Nombre)
            .Select(g => new ProductoResumen(
                g.Key,
                g.Sum(x => x.Cantidad),
                g.Sum(x => x.PrecioHistorico * x.Cantidad)))
            .ToListAsync();

        // Ventas agrupadas por día (para el gráfico de barras), en memoria.
        var porDia = vigentes
            .GroupBy(d => d.Fecha.Date)
            .OrderBy(g => g.Key)
            .Select(g => new VentaDia(g.Key, g.Sum(x => x.Total)))
            .ToList();

        var ventasTotales = vigentes.Sum(d => d.Total);
        var numDespachos = vigentes.Count;
        var unidades = vigentes.Sum(d => d.Unidades);

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
