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
    public async Task ObtenerComprobante_UnaLineaPorLoteConSuNumero()
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

        Assert.Equal(4, comprobante!.Lineas.Count);          // tres lotes de MULTI + OTRO
        Assert.Equal(new[] { "L1", "L2", "L3", "L1" }, comprobante.Lineas.Select(l => l.Lote).ToArray());
        Assert.Equal(new[] { 1, 1, 2, 1 }, comprobante.Lineas.Select(l => l.Cantidad).ToArray());
        Assert.All(comprobante.Lineas.Take(3), l => Assert.Equal("MULTI", l.Codigo));
        Assert.Equal(5, comprobante.Unidades);
        Assert.Equal(13m, comprobante.Total);
    }

    [Fact]
    public async Task Confirmar_ConLoteElegido_DescuentaSoloDeEseLote()
    {
        using var factory = new TestDbContextFactory();
        var repo = new DespachoRepository(factory);
        var id = await SembrarAsync(factory, "MANUAL", 2m,
            ("CERCANO", Hoy.AddDays(30), 5),
            ("LEJANO", Hoy.AddDays(300), 5));

        int lejanoId;
        await using (var ctx = await factory.CreateDbContextAsync())
            lejanoId = (await ctx.Lotes.SingleAsync(l => l.NumeroLote == "LEJANO")).Id;

        var resultado = await repo.ConfirmarAsync(new[] { new LineaDespacho(id, 3, lejanoId) });

        Assert.True(resultado.Exito);
        await using var verif = await factory.CreateDbContextAsync();
        var lotes = await verif.Lotes.Where(l => l.MedicamentoId == id).ToDictionaryAsync(l => l.NumeroLote);
        Assert.Equal(5, lotes["CERCANO"].Existencias);       // intacto aunque venza antes
        Assert.Equal(2, lotes["LEJANO"].Existencias);
        var detalle = Assert.Single(verif.DetallesDespacho);
        Assert.Equal(lejanoId, detalle.LoteId);
    }

    [Fact]
    public async Task Confirmar_ConLoteElegidoSinStockOInexistente_NoDespacha()
    {
        using var factory = new TestDbContextFactory();
        var repo = new DespachoRepository(factory);
        var id = await SembrarAsync(factory, "MANUAL2", 2m,
            ("CHICO", Hoy.AddDays(30), 1),
            ("GRANDE", Hoy.AddDays(60), 50));

        int chicoId;
        await using (var ctx = await factory.CreateDbContextAsync())
            chicoId = (await ctx.Lotes.SingleAsync(l => l.NumeroLote == "CHICO")).Id;

        var insuficiente = await repo.ConfirmarAsync(new[] { new LineaDespacho(id, 2, chicoId) });
        Assert.False(insuficiente.Exito);
        Assert.Contains("CHICO", insuficiente.Mensaje);     // no toma del otro lote por su cuenta

        var inexistente = await repo.ConfirmarAsync(new[] { new LineaDespacho(id, 1, 9999) });
        Assert.False(inexistente.Exito);

        await using var verif = await factory.CreateDbContextAsync();
        Assert.Equal(51, await verif.Lotes.Where(l => l.MedicamentoId == id).SumAsync(l => l.Existencias));
        Assert.Empty(verif.Despachos);
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

public class AnularDespachoTests
{
    private static readonly DateTime Hoy = DateTime.Today;

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

    [Fact]
    public async Task Anular_DevuelveLasUnidadesALosMismosLotesYMarcaElDespacho()
    {
        using var factory = new TestDbContextFactory();
        var repo = new DespachoRepository(factory);
        var id = await SembrarAsync(factory, "UNDO", 2m,
            ("CERCANO", Hoy.AddDays(30), 3),
            ("LEJANO", Hoy.AddDays(300), 10));

        var venta = await repo.ConfirmarAsync(new[] { new LineaDespacho(id, 5) }); // 3 de CERCANO + 2 de LEJANO
        Assert.True(venta.Exito);

        var resultado = await repo.AnularAsync(venta.DespachoId!.Value, "Cliente devolvió", "cajera");

        Assert.True(resultado.Exito);
        await using var ctx = await factory.CreateDbContextAsync();
        var lotes = await ctx.Lotes.Where(l => l.MedicamentoId == id).ToDictionaryAsync(l => l.NumeroLote);
        Assert.Equal(3, lotes["CERCANO"].Existencias);       // volvió a lo que tenía
        Assert.Equal(10, lotes["LEJANO"].Existencias);

        var despacho = await ctx.Despachos.Include(d => d.Detalles).SingleAsync();
        Assert.True(despacho.Anulado);                       // el registro se conserva
        Assert.Equal("Cliente devolvió", despacho.MotivoAnulacion);
        Assert.Equal("cajera", despacho.UsuarioAnulacion);
        Assert.NotNull(despacho.FechaAnulacion);
        Assert.Equal(2, despacho.Detalles.Count);            // el detalle no se borra
    }

    [Fact]
    public async Task Anular_DosVeces_LaSegundaFallaSinDuplicarStock()
    {
        using var factory = new TestDbContextFactory();
        var repo = new DespachoRepository(factory);
        var id = await SembrarAsync(factory, "DOS", 1m, ("L1", Hoy.AddYears(1), 10));
        var venta = await repo.ConfirmarAsync(new[] { new LineaDespacho(id, 4) });

        Assert.True((await repo.AnularAsync(venta.DespachoId!.Value, "error", "u")).Exito);
        var segunda = await repo.AnularAsync(venta.DespachoId!.Value, "otra vez", "u");

        Assert.False(segunda.Exito);
        Assert.Contains("ya fue deshecho", segunda.Mensaje);
        await using var ctx = await factory.CreateDbContextAsync();
        Assert.Equal(10, await ctx.Lotes.Where(l => l.MedicamentoId == id).SumAsync(l => l.Existencias));
    }

    [Fact]
    public async Task Anular_SinMotivoOInexistente_Falla()
    {
        using var factory = new TestDbContextFactory();
        var repo = new DespachoRepository(factory);
        var id = await SembrarAsync(factory, "MOT", 1m, ("L1", Hoy.AddYears(1), 10));
        var venta = await repo.ConfirmarAsync(new[] { new LineaDespacho(id, 1) });

        Assert.False((await repo.AnularAsync(venta.DespachoId!.Value, "   ", "u")).Exito);
        Assert.False((await repo.AnularAsync(9999, "motivo", "u")).Exito);

        await using var ctx = await factory.CreateDbContextAsync();
        Assert.Equal(9, await ctx.Lotes.Where(l => l.MedicamentoId == id).SumAsync(l => l.Existencias));
        Assert.False((await ctx.Despachos.SingleAsync()).Anulado);
    }

    [Fact]
    public async Task Anular_DespachoAnteriorALotes_SeBloquea()
    {
        using var factory = new TestDbContextFactory();
        var repo = new DespachoRepository(factory);
        var id = await SembrarAsync(factory, "V10", 1m, ("INICIAL", null, 10));

        // Simula un despacho de la versión 1.0: detalle sin lote.
        int despachoId;
        await using (var ctx = await factory.CreateDbContextAsync())
        {
            var d = new Despacho { Fecha = DateTime.Now, Total = 2m };
            d.Detalles.Add(new DetalleDespacho { MedicamentoId = id, LoteId = null, Cantidad = 2, PrecioHistorico = 1m });
            ctx.Despachos.Add(d);
            await ctx.SaveChangesAsync();
            despachoId = d.Id;
        }

        var resultado = await repo.AnularAsync(despachoId, "motivo", "u");

        Assert.False(resultado.Exito);
        Assert.Contains("anterior al control por lotes", resultado.Mensaje);
        await using var verif = await factory.CreateDbContextAsync();
        Assert.Equal(10, await verif.Lotes.Where(l => l.MedicamentoId == id).SumAsync(l => l.Existencias));

        var comprobante = await repo.ObtenerComprobanteAsync(despachoId);
        Assert.False(comprobante!.TieneLotes);
    }

    [Fact]
    public async Task Reporte_ExcluyeAnuladosDeLosTotalesPeroLosLista()
    {
        using var factory = new TestDbContextFactory();
        var repo = new DespachoRepository(factory);
        var id = await SembrarAsync(factory, "RPT2", 10m, ("L1", Hoy.AddYears(1), 100));

        var v1 = await repo.ConfirmarAsync(new[] { new LineaDespacho(id, 4) }); // 40
        var v2 = await repo.ConfirmarAsync(new[] { new LineaDespacho(id, 1) }); // 10
        await repo.AnularAsync(v1.DespachoId!.Value, "error de captura", "u");

        var r = await repo.GenerarReporteAsync(Hoy.AddDays(-1), Hoy.AddDays(1));

        Assert.Equal(10m, r.VentasTotales);
        Assert.Equal(1, r.NumDespachos);
        Assert.Equal(1, r.Unidades);
        Assert.Equal(10m, r.Ticket);
        Assert.Equal(1, r.Top!.Unidades);                    // el ranking tampoco cuenta lo anulado
        Assert.Equal(2, r.Despachos.Count);                  // pero sigue apareciendo en la lista
        Assert.True(r.Despachos.Single(d => d.Id == v1.DespachoId).Anulado);
        Assert.False(r.Despachos.Single(d => d.Id == v2.DespachoId).Anulado);
        Assert.Equal(10m, r.PorDia.Single().Total);
    }

    [Fact]
    public async Task ObtenerComprobante_IncluyeLaAnulacion()
    {
        using var factory = new TestDbContextFactory();
        var repo = new DespachoRepository(factory);
        var id = await SembrarAsync(factory, "CMPA", 1m, ("L1", Hoy.AddYears(1), 10));
        var venta = await repo.ConfirmarAsync(new[] { new LineaDespacho(id, 1) });

        var antes = await repo.ObtenerComprobanteAsync(venta.DespachoId!.Value);
        Assert.False(antes!.Anulado);
        Assert.True(antes.TieneLotes);

        await repo.AnularAsync(venta.DespachoId!.Value, "devolución", "admin");
        var despues = await repo.ObtenerComprobanteAsync(venta.DespachoId!.Value);

        Assert.True(despues!.Anulado);
        Assert.Equal("devolución", despues.MotivoAnulacion);
        Assert.Equal("admin", despues.UsuarioAnulacion);
    }

    [Fact]
    public void PoliticaDeAcceso_EmpleadoSoloHoy_AdminCualquierDia()
    {
        var lineas = new[] { new LineaComprobante("C", "N", 1, 1m, "L1") };
        var deHoy = new ComprobanteDespacho(1, DateTime.Now, 1m, lineas);
        var deAyer = new ComprobanteDespacho(2, DateTime.Now.AddDays(-1), 1m, lineas);
        var anulado = deHoy with { Anulado = true, FechaAnulacion = DateTime.Now };
        var sinLote = new ComprobanteDespacho(3, DateTime.Now, 1m, new[] { new LineaComprobante("C", "N", 1, 1m) });

        Assert.Equal("", FarmaGestion.ViewModels.AnularDespachoViewModel.MotivoBloqueo(deHoy, esAdmin: false, Hoy));
        Assert.Equal("", FarmaGestion.ViewModels.AnularDespachoViewModel.MotivoBloqueo(deAyer, esAdmin: true, Hoy));
        Assert.Contains("administrador", FarmaGestion.ViewModels.AnularDespachoViewModel.MotivoBloqueo(deAyer, esAdmin: false, Hoy));
        Assert.Contains("ya fue deshecho", FarmaGestion.ViewModels.AnularDespachoViewModel.MotivoBloqueo(anulado, esAdmin: true, Hoy));
        Assert.Contains("anterior al control por lotes", FarmaGestion.ViewModels.AnularDespachoViewModel.MotivoBloqueo(sinLote, esAdmin: true, Hoy));
    }
}

public class ReporteProductosTests
{
    [Fact]
    public async Task Reporte_IncluyeLosProductosDeCadaDespachoSumandoLotes()
    {
        using var factory = new TestDbContextFactory();
        var repo = new DespachoRepository(factory);
        int a, b;
        await using (var ctx = await factory.CreateDbContextAsync())
        {
            var hoy = DateTime.Today;
            var medA = new Medicamentos { CodigoBarras = "A", Nombre = "Alfa", Precio = 1m };
            medA.Lotes.Add(new Lote { NumeroLote = "L1", FechaVencimiento = hoy.AddDays(10), Existencias = 1 });
            medA.Lotes.Add(new Lote { NumeroLote = "L2", FechaVencimiento = hoy.AddDays(20), Existencias = 5 });
            var medB = new Medicamentos { CodigoBarras = "B", Nombre = "Beta", Precio = 1m };
            medB.Lotes.Add(new Lote { NumeroLote = "L1", FechaVencimiento = hoy.AddDays(10), Existencias = 5 });
            ctx.AddRange(medA, medB);
            await ctx.SaveChangesAsync();
            a = medA.Id; b = medB.Id;
        }

        await repo.ConfirmarAsync(new[] { new LineaDespacho(a, 3), new LineaDespacho(b, 1) }); // Alfa sale de dos lotes

        var r = await repo.GenerarReporteAsync(DateTime.Today.AddDays(-1), DateTime.Today.AddDays(1));

        var productos = Assert.Single(r.Despachos).Productos;
        Assert.Equal(new[] { ("Alfa", 3), ("Beta", 1) }, productos.Select(p => (p.Nombre, p.Cantidad)).ToArray());
    }

    [Fact]
    public void ResumirProductos_MuestraTresYCuentaElResto()
    {
        var lista = new[] { new ProductoDespacho("Uno", 2), new ProductoDespacho("Dos", 1),
                            new ProductoDespacho("Tres", 4), new ProductoDespacho("Cuatro", 1), new ProductoDespacho("Cinco", 1) };

        Assert.Equal("Uno ×2 · Dos ×1 · Tres ×4 · +2 más", FarmaGestion.ViewModels.DespachoReporteItem.ResumirProductos(lista));
        Assert.Equal("Uno ×2 · Dos ×1", FarmaGestion.ViewModels.DespachoReporteItem.ResumirProductos(lista.Take(2).ToList()));
        Assert.Equal("Sin detalle", FarmaGestion.ViewModels.DespachoReporteItem.ResumirProductos(Array.Empty<ProductoDespacho>()));
    }
}
