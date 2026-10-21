using System;
using System.Linq;
using System.Threading.Tasks;
using FarmaGestion.Data.Repositories;
using FarmaGestion.Models;
using Xunit;

namespace FarmaGestion.Tests;

public class DespachoRepositoryTests
{
    private static async Task<int> SembrarMedicamentoAsync(TestDbContextFactory factory,
        string codigo, decimal precio, int existencias)
    {
        await using var ctx = await factory.CreateDbContextAsync();
        var med = new Medicamentos
        {
            CodigoBarras = codigo,
            Nombre = $"Med-{codigo}",
            Precio = precio,
            Existencias = existencias
        };
        ctx.Medicamentos.Add(med);
        await ctx.SaveChangesAsync();
        return med.Id;
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
    public async Task Confirmar_StockSuficiente_RebajaInventarioYCalculaTotal()
    {
        using var factory = new TestDbContextFactory();
        var repo = new DespachoRepository(factory);
        var id = await SembrarMedicamentoAsync(factory, "001", precio: 10m, existencias: 20);

        var resultado = await repo.ConfirmarAsync(new[] { new LineaDespacho(id, 5) });

        Assert.True(resultado.Exito);

        await using var ctx = await factory.CreateDbContextAsync();
        var med = await ctx.Medicamentos.FindAsync(id);
        Assert.Equal(15, med!.Existencias);                 // 20 - 5

        var despacho = ctx.Despachos.Single();
        Assert.Equal(50m, despacho.Total);                  // 5 * 10
    }

    [Fact]
    public async Task Confirmar_StockInsuficiente_NoModificaNada()
    {
        using var factory = new TestDbContextFactory();
        var repo = new DespachoRepository(factory);
        var id = await SembrarMedicamentoAsync(factory, "002", precio: 10m, existencias: 3);

        var resultado = await repo.ConfirmarAsync(new[] { new LineaDespacho(id, 10) });

        Assert.False(resultado.Exito);

        await using var ctx = await factory.CreateDbContextAsync();
        var med = await ctx.Medicamentos.FindAsync(id);
        Assert.Equal(3, med!.Existencias);                  // intacto
        Assert.Empty(ctx.Despachos);                        // no se creó despacho (rollback)
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

        await using var ctx = await factory.CreateDbContextAsync();
        // El primer producto NO debe haberse rebajado: la transacción revirtió todo.
        Assert.Equal(50, (await ctx.Medicamentos.FindAsync(idOk))!.Existencias);
        Assert.Empty(ctx.Despachos);
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
