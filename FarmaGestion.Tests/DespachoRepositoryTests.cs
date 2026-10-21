using System;
using System.Linq;
using System.Threading.Tasks;
using FarmaGestion.Data.Repositories;
using FarmaGestion.Models;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FarmaGestion.Tests;

public class DespachoRepositoryTests
{
    private static readonly DateTime Hoy = DateTime.Today;

    /// <summary>Siembra un producto con los lotes indicados (número, vencimiento, cantidad).</summary>
    private static async Task<int> SembrarAsync(TestDbContextFactory factory, string codigo, decimal precio,
        params (string Lote, DateTime? Vence, int Cantidad)[] lotes)
    {
        await using var ctx = await factory.CreateDbContextAsync();
        var med = new Medicamentos { CodigoBarras = codigo, Nombre = $"Med-{codigo}", Precio = precio };
        foreach (var (lote, vence, cantidad) in lotes)
            med.Lotes.Add(new Lote { NumeroLote = lote, FechaVencimiento = vence, Existencias = cantidad });
        ctx.Medicamentos.Add(med);
        await ctx.SaveChangesAsync();
        return med.Id;
    }

    /// <summary>Siembra un producto con un único lote vigente.</summary>
    private static Task<int> SembrarMedicamentoAsync(TestDbContextFactory factory, string codigo, decimal precio, int existencias)
        => SembrarAsync(factory, codigo, precio, ("L1", Hoy.AddYears(1), existencias));

    private static async Task<int> StockAsync(TestDbContextFactory factory, int medicamentoId)
    {
        await using var ctx = await factory.CreateDbContextAsync();
        return await ctx.Lotes.Where(l => l.MedicamentoId == medicamentoId).SumAsync(l => l.Existencias);
    }

    [Fact]
    public async Task Confirmar_CarritoVacio_DevuelveError()
    {
        using var factory = new TestDbContextFactory();
        var repo = new DespachoRepository(factory);

        var resultado = await repo.ConfirmarAsync(Array.Empty<LineaDespacho>());

        Assert.False(resultado.Exito);
    }

    [Fact]
    public async Task Confirmar_StockSuficiente_RebajaElLoteYCalculaTotal()
    {
        using var factory = new TestDbContextFactory();
        var repo = new DespachoRepository(factory);
        var id = await SembrarMedicamentoAsync(factory, "001", precio: 10m, existencias: 20);

        var resultado = await repo.ConfirmarAsync(new[] { new LineaDespacho(id, 5) });

        Assert.True(resultado.Exito);
        Assert.Empty(resultado.Advertencias);
        Assert.Equal(15, await StockAsync(factory, id));     // 20 - 5

        await using var ctx = await factory.CreateDbContextAsync();
        var despacho = ctx.Despachos.Include(d => d.Detalles).Single();
        Assert.Equal(50m, despacho.Total);                   // 5 * 10
        var detalle = Assert.Single(despacho.Detalles);
        Assert.NotNull(detalle.LoteId);                      // queda registrado el lote de salida
    }

    [Fact]
    public async Task Confirmar_StockInsuficiente_NoModificaNada()
    {
        using var factory = new TestDbContextFactory();
        var repo = new DespachoRepository(factory);
        var id = await SembrarMedicamentoAsync(factory, "002", precio: 10m, existencias: 3);

        var resultado = await repo.ConfirmarAsync(new[] { new LineaDespacho(id, 10) });

        Assert.False(resultado.Exito);
        Assert.Equal(3, await StockAsync(factory, id));      // intacto

        await using var ctx = await factory.CreateDbContextAsync();
        Assert.Empty(ctx.Despachos);                         // no se creó despacho (rollback)
    }

    [Fact]
    public async Task Confirmar_VariasLineas_UnaSinStock_RevierteTodo()
    {
        using var factory = new TestDbContextFactory();
        var repo = new DespachoRepository(factory);
        var idOk = await SembrarMedicamentoAsync(factory, "A", precio: 5m, existencias: 50);
        var idMal = await SembrarMedicamentoAsync(factory, "B", precio: 5m, existencias: 1);

        var resultado = await repo.ConfirmarAsync(new[]
        {
            new LineaDespacho(idOk, 10),
            new LineaDespacho(idMal, 5) // insuficiente
        });

        Assert.False(resultado.Exito);
        // El primer producto NO debe haberse rebajado: la transacción revirtió todo.
        Assert.Equal(50, await StockAsync(factory, idOk));

        await using var ctx = await factory.CreateDbContextAsync();
        Assert.Empty(ctx.Despachos);
    }

    [Fact]
    public async Task Confirmar_Fefo_SaleDelLoteQueVencePrimeroYReparteEntreLotes()
    {
        using var factory = new TestDbContextFactory();
        var repo = new DespachoRepository(factory);
        var id = await SembrarAsync(factory, "FEFO", 2m,
            ("LEJANO", Hoy.AddDays(300), 10),
            ("CERCANO", Hoy.AddDays(30), 3));

        var resultado = await repo.ConfirmarAsync(new[] { new LineaDespacho(id, 5) });

        Assert.True(resultado.Exito);

        await using var ctx = await factory.CreateDbContextAsync();
        var lotes = await ctx.Lotes.Where(l => l.MedicamentoId == id).ToDictionaryAsync(l => l.NumeroLote);
        Assert.Equal(0, lotes["CERCANO"].Existencias);       // se agotó primero
        Assert.Equal(8, lotes["LEJANO"].Existencias);        // 10 - 2

        var detalles = await ctx.DetallesDespacho.Include(d => d.Lote).OrderBy(d => d.Id).ToListAsync();
        Assert.Equal(2, detalles.Count);                     // un detalle por lote
        Assert.Equal(("CERCANO", 3), (detalles[0].Lote!.NumeroLote, detalles[0].Cantidad));
        Assert.Equal(("LEJANO", 2), (detalles[1].Lote!.NumeroLote, detalles[1].Cantidad));
    }

    [Fact]
    public async Task Confirmar_LotesVencidos_SeUsanAlFinalYSoloAdvierten()
    {
        using var factory = new TestDbContextFactory();
        var repo = new DespachoRepository(factory);
        var id = await SembrarAsync(factory, "VENC", 1m,
            ("VENCIDO", Hoy.AddDays(-10), 5),
            ("VIGENTE", Hoy.AddDays(100), 2));

        var resultado = await repo.ConfirmarAsync(new[] { new LineaDespacho(id, 4) });

        Assert.True(resultado.Exito);                        // política: advertir, no bloquear
        var advertencia = Assert.Single(resultado.Advertencias);
        Assert.Contains("2 u", advertencia);
        Assert.Contains("VENCIDO", advertencia);

        await using var ctx = await factory.CreateDbContextAsync();
        var lotes = await ctx.Lotes.Where(l => l.MedicamentoId == id).ToDictionaryAsync(l => l.NumeroLote);
        Assert.Equal(0, lotes["VIGENTE"].Existencias);
        Assert.Equal(3, lotes["VENCIDO"].Existencias);
    }

    [Fact]
    public async Task ObtenerComprobante_DevuelveElDetalleDelDespacho()
    {
        using var factory = new TestDbContextFactory();
        var repo = new DespachoRepository(factory);
        var id = await SembrarMedicamentoAsync(factory, "CMP", precio: 12.5m, existencias: 10);

        var resultado = await repo.ConfirmarAsync(new[] { new LineaDespacho(id, 2) });
        Assert.True(resultado.Exito);
        Assert.NotNull(resultado.DespachoId);

        var comprobante = await repo.ObtenerComprobanteAsync(resultado.DespachoId!.Value);

        Assert.NotNull(comprobante);
        Assert.Equal(25m, comprobante!.Total);
        Assert.Equal(2, comprobante.Unidades);

        var linea = Assert.Single(comprobante.Lineas);
        Assert.Equal("CMP", linea.Codigo);
        Assert.Equal("Med-CMP", linea.Nombre);
        Assert.Equal(2, linea.Cantidad);
        Assert.Equal(12.5m, linea.PrecioUnitario);   // precio histórico
        Assert.Equal(25m, linea.Subtotal);
    }

    [Fact]
    public async Task ObtenerComprobante_AgrupaEnUnaLineaAunqueSalgaDeVariosLotes()
    {
        using var factory = new TestDbContextFactory();
        var repo = new DespachoRepository(factory);
        var id = await SembrarAsync(factory, "MULTI", 3m,
            ("L1", Hoy.AddDays(10), 1),
            ("L2", Hoy.AddDays(20), 1),
            ("L3", Hoy.AddDays(30), 5));
        var otro = await SembrarMedicamentoAsync(factory, "OTRO", precio: 1m, existencias: 9);

        var resultado = await repo.ConfirmarAsync(new[] { new LineaDespacho(id, 4), new LineaDespacho(otro, 1) });
        var comprobante = await repo.ObtenerComprobanteAsync(resultado.DespachoId!.Value);

        Assert.Equal(2, comprobante!.Lineas.Count);          // una línea por producto, no por lote
        Assert.Equal(("MULTI", 4, 12m), (comprobante.Lineas[0].Codigo, comprobante.Lineas[0].Cantidad, comprobante.Lineas[0].Subtotal));
        Assert.Equal(("OTRO", 1), (comprobante.Lineas[1].Codigo, comprobante.Lineas[1].Cantidad));
        Assert.Equal(5, comprobante.Unidades);
        Assert.Equal(13m, comprobante.Total);
    }

    [Fact]
    public async Task ObtenerComprobante_DespachoInexistente_DevuelveNull()
    {
        using var factory = new TestDbContextFactory();
        var repo = new DespachoRepository(factory);

        Assert.Null(await repo.ObtenerComprobanteAsync(9999));
    }

    [Fact]
    public async Task ObtenerComprobante_ConservaElPrecioAunqueCambieElProducto()
    {
        using var factory = new TestDbContextFactory();
        var repo = new DespachoRepository(factory);
        var id = await SembrarMedicamentoAsync(factory, "HIST", precio: 10m, existencias: 10);

        var resultado = await repo.ConfirmarAsync(new[] { new LineaDespacho(id, 1) });

        // El producto sube de precio después del despacho.
        await using (var ctx = await factory.CreateDbContextAsync())
        {
            var med = await ctx.Medicamentos.FindAsync(id);
            med!.Precio = 99m;
            await ctx.SaveChangesAsync();
        }

        var comprobante = await repo.ObtenerComprobanteAsync(resultado.DespachoId!.Value);

        Assert.Equal(10m, comprobante!.Lineas[0].PrecioUnitario);
    }

    [Fact]
    public async Task Eliminar_ProductoConDespachos_SeBloquea()
    {
        using var factory = new TestDbContextFactory();
        var despachos = new DespachoRepository(factory);
        var productos = new MedicamentoRepository(factory);
        var id = await SembrarMedicamentoAsync(factory, "DEL", precio: 1m, existencias: 10);

        await despachos.ConfirmarAsync(new[] { new LineaDespacho(id, 1) });

        Assert.False(await productos.EliminarAsync(id));
    }

    [Fact]
    public async Task GenerarReporte_DevuelveRankingYTotales()
    {
        using var factory = new TestDbContextFactory();
        var repo = new DespachoRepository(factory);
        var id = await SembrarMedicamentoAsync(factory, "RPT", precio: 10m, existencias: 100);

        await repo.ConfirmarAsync(new[] { new LineaDespacho(id, 4) }); // total 40

        var reporte = await repo.GenerarReporteAsync(DateTime.Today.AddDays(-1), DateTime.Today.AddDays(1));

        Assert.Equal(40m, reporte.VentasTotales);
        Assert.Single(reporte.Despachos);
        Assert.Equal(4, reporte.Unidades);
        Assert.NotNull(reporte.Top);
        Assert.Equal(4, reporte.Top!.Unidades);
    }
}
