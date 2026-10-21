using System;
using System.Linq;
using System.Threading.Tasks;
using FarmaGestion.Data.Repositories;
using FarmaGestion.Models;
using Xunit;

namespace FarmaGestion.Tests;

public class MedicamentoRepositoryTests
{
    private static readonly DateTime EnUnAnio = DateTime.Today.AddYears(1);

    private static (MedicamentoRepository repo, TestDbContextFactory factory) Crear()
    {
        var factory = new TestDbContextFactory();
        return (new MedicamentoRepository(factory), factory);
    }

    private static Medicamentos Producto(string codigo, string nombre, decimal precio, string? lote = null, int cantidad = 0)
    {
        var m = new Medicamentos { CodigoBarras = codigo, Nombre = nombre, Precio = precio };
        if (lote != null)
            m.Lotes.Add(new Lote { NumeroLote = lote, FechaVencimiento = EnUnAnio, Existencias = cantidad });
        return m;
    }

    [Fact]
    public async Task AgregarConPrimerLote_YObtener_FuncionaCorrectamente()
    {
        var (repo, factory) = Crear();
        using (factory)
        {
            await repo.AgregarAsync(Producto("001", "Paracetamol", 5m, lote: "l-01", cantidad: 10));

            var todos = await repo.ObtenerTodosAsync();
            Assert.Single(todos);

            var encontrado = await repo.ObtenerPorCodigoAsync("001");
            Assert.NotNull(encontrado);
            Assert.Equal("Paracetamol", encontrado!.Nombre);
            Assert.Equal(10, encontrado.Existencias);
            var lote = Assert.Single(encontrado.Lotes);
            Assert.Equal("L-01", lote.NumeroLote);          // se normaliza a mayúsculas
            Assert.Equal(EnUnAnio, lote.FechaVencimiento);
        }
    }

    [Fact]
    public async Task ExisteCodigo_DetectaDuplicados()
    {
        var (repo, factory) = Crear();
        using (factory)
        {
            await repo.AgregarAsync(Producto("ABC", "Ibuprofeno", 8m));

            Assert.True(await repo.ExisteCodigoAsync("ABC"));
            Assert.False(await repo.ExisteCodigoAsync("XYZ"));
        }
    }

    [Fact]
    public async Task Actualizar_SoloCambiaDatosMaestros()
    {
        var (repo, factory) = Crear();
        using (factory)
        {
            await repo.AgregarAsync(Producto("010", "Nombre viejo", 1m, lote: "A", cantidad: 7));
            var id = (await repo.ObtenerPorCodigoAsync("010"))!.Id;

            await repo.ActualizarAsync(new Medicamentos { Id = id, CodigoBarras = "010", Nombre = "Nombre nuevo", Precio = 2m });

            var m = await repo.ObtenerPorIdAsync(id);
            Assert.Equal("Nombre nuevo", m!.Nombre);
            Assert.Equal(2m, m.Precio);
            Assert.Equal(7, m.Existencias);                 // las existencias no se tocan
        }
    }

    [Fact]
    public async Task IngresarLote_CreaLoteNuevo_YLuegoSumaAlMismoLote()
    {
        var (repo, factory) = Crear();
        using (factory)
        {
            await repo.AgregarAsync(Producto("100", "Amoxicilina", 12m));
            var id = (await repo.ObtenerPorCodigoAsync("100"))!.Id;

            var r1 = await repo.IngresarLoteAsync(id, "L2026A", EnUnAnio, 20);
            Assert.True(r1.Exito);
            Assert.True(r1.LoteNuevo);

            var r2 = await repo.IngresarLoteAsync(id, " l2026a ", EnUnAnio, 5);   // mismo lote, distinto formato
            Assert.True(r2.Exito);
            Assert.False(r2.LoteNuevo);

            var m = await repo.ObtenerPorIdAsync(id);
            var lote = Assert.Single(m!.Lotes);
            Assert.Equal(25, lote.Existencias);
            Assert.Equal(25, m.Existencias);
        }
    }

    [Fact]
    public async Task IngresarLote_RechazaFechaDistintaParaElMismoLote_YCantidadNoPositiva()
    {
        var (repo, factory) = Crear();
        using (factory)
        {
            await repo.AgregarAsync(Producto("200", "Loratadina", 3m, lote: "X1", cantidad: 4));
            var id = (await repo.ObtenerPorCodigoAsync("200"))!.Id;

            var distinta = await repo.IngresarLoteAsync(id, "X1", EnUnAnio.AddMonths(2), 1);
            Assert.False(distinta.Exito);
            Assert.Contains("ya existe con vencimiento", distinta.Mensaje);

            var cero = await repo.IngresarLoteAsync(id, "X2", EnUnAnio, 0);
            Assert.False(cero.Exito);

            var m = await repo.ObtenerPorIdAsync(id);
            Assert.Equal(4, m!.Existencias);                 // nada cambió
            Assert.Single(m.Lotes);
        }
    }

    [Fact]
    public async Task DarDeBajaLote_DejaElLoteEnCero()
    {
        var (repo, factory) = Crear();
        using (factory)
        {
            await repo.AgregarAsync(Producto("300", "Omeprazol", 6m, lote: "V1", cantidad: 9));
            var m = await repo.ObtenerPorCodigoAsync("300");
            var loteId = m!.Lotes.Single().Id;

            Assert.True(await repo.DarDeBajaLoteAsync(loteId));
            Assert.False(await repo.DarDeBajaLoteAsync(9999));

            var despues = await repo.ObtenerPorIdAsync(m.Id);
            Assert.Equal(0, despues!.Existencias);
            Assert.Equal(0, despues.NumeroLotes);             // ya no cuenta como lote activo
        }
    }

    [Fact]
    public async Task ImportarRegistros_CadaFilaEsUnLote()
    {
        var (repo, factory) = Crear();
        using (factory)
        {
            await repo.AgregarAsync(Producto("777", "Aspirina", 4m, lote: "A1", cantidad: 5));

            var resultado = await repo.ImportarRegistrosAsync(new[]
            {
                new RegistroMedicamentoCsv("777", "Aspirina", 6m, 10, "A1", EnUnAnio, Fila: 2),               // mismo lote: +10
                new RegistroMedicamentoCsv("777", "Aspirina", 6m, 3, "A2", EnUnAnio.AddMonths(3), Fila: 3),  // lote nuevo
                new RegistroMedicamentoCsv("888", "Amoxicilina", 12m, 7, "B1", EnUnAnio, Fila: 4),           // producto nuevo
                new RegistroMedicamentoCsv("888", "Amoxicilina", 12m, 2, "B2", EnUnAnio, Fila: 5),           // segundo lote del nuevo
                new RegistroMedicamentoCsv("777", "Aspirina", 6m, 1, "A1", EnUnAnio.AddDays(1), Fila: 6),    // misma clave, otra fecha: rechazo
            });

            Assert.Equal(4, resultado.Procesadas);
            var rechazo = Assert.Single(resultado.Rechazadas);
            Assert.StartsWith("Fila 6:", rechazo);

            var existente = await repo.ObtenerPorCodigoAsync("777");
            Assert.Equal(18, existente!.Existencias);        // 5 + 10 + 3
            Assert.Equal(2, existente.Lotes.Count);
            Assert.Equal(6m, existente.Precio);              // precio actualizado

            var nuevo = await repo.ObtenerPorCodigoAsync("888");
            Assert.NotNull(nuevo);
            Assert.Equal(9, nuevo!.Existencias);
            Assert.Equal(2, nuevo.Lotes.Count);
        }
    }

    [Fact]
    public async Task Eliminar_BorraElProductoConSusLotes()
    {
        var (repo, factory) = Crear();
        using (factory)
        {
            await repo.AgregarAsync(Producto("900", "Temporal", 1m, lote: "T", cantidad: 2));
            var id = (await repo.ObtenerPorCodigoAsync("900"))!.Id;

            Assert.True(await repo.EliminarAsync(id));

            await using var ctx = await factory.CreateDbContextAsync();
            Assert.Empty(ctx.Medicamentos);
            Assert.Empty(ctx.Lotes);
        }
    }
}
