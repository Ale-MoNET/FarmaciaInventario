using System;
using System.Threading.Tasks;
using FarmaGestion.Data.Repositories;
using FarmaGestion.Models;
using FarmaGestion.ViewModels;
using Xunit;

namespace FarmaGestion.Tests;

public class ProductoViewModelTests
{
    private static async Task<(ProductoViewModel vm, MedicamentoRepository repo, TestDbContextFactory factory)> CrearAsync()
    {
        var factory = new TestDbContextFactory();
        var repo = new MedicamentoRepository(factory);

        var amoxicilina = new Medicamentos { CodigoBarras = "ABC", Nombre = "Amoxicilina 500", Precio = 12m };
        amoxicilina.Lotes.Add(new Lote { NumeroLote = "A1", FechaVencimiento = DateTime.Today.AddYears(1), Existencias = 7 });
        await repo.AgregarAsync(amoxicilina);
        await repo.AgregarAsync(new Medicamentos { CodigoBarras = "XYZ", Nombre = "Otro", Precio = 1m });

        return (new ProductoViewModel(repo), repo, factory);
    }

    [Fact]
    public async Task ComprobarCodigo_Existente_CargaElProductoEnModoEdicion()
    {
        var (vm, _, factory) = await CrearAsync();
        using (factory)
        {
            vm.CodigoBarras = " ABC ";

            Assert.True(await vm.ComprobarCodigoExistenteAsync());

            Assert.True(vm.EsEdicion);
            Assert.Equal("Amoxicilina 500", vm.Nombre);
            Assert.Equal("12", vm.Precio);
            Assert.True(vm.MostrarAviso);
            Assert.Contains("7 u", vm.Aviso);
            Assert.False(vm.MostrarPrimerLote);
        }
    }

    [Fact]
    public async Task ComprobarCodigo_Nuevo_SigueEnModoCreacion()
    {
        var (vm, _, factory) = await CrearAsync();
        using (factory)
        {
            vm.CodigoBarras = "NUEVO-1";

            Assert.False(await vm.ComprobarCodigoExistenteAsync());

            Assert.False(vm.EsEdicion);
            Assert.False(vm.MostrarAviso);
            Assert.Equal(string.Empty, vm.Nombre);
        }
    }

    [Fact]
    public async Task ComprobarCodigo_EnEdicionConCodigoDeOtroProducto_AvisaSinCambiarDeProducto()
    {
        var (vm, repo, factory) = await CrearAsync();
        using (factory)
        {
            var amoxicilina = await repo.ObtenerPorCodigoAsync("ABC");
            await vm.CargarParaEdicionAsync(amoxicilina!.Id);

            vm.CodigoBarras = "XYZ";                                   // código del otro producto
            Assert.False(await vm.ComprobarCodigoExistenteAsync());

            Assert.Equal("Amoxicilina 500", vm.Nombre);               // sigue editando el mismo
            Assert.True(vm.MostrarError);
            Assert.Contains("Otro", vm.MensajeError);

            vm.CodigoBarras = "ABC";                                   // su propio código: sin aviso
            Assert.False(await vm.ComprobarCodigoExistenteAsync());
        }
    }

    [Fact]
    public async Task Guardar_ConCodigoYaRegistrado_NoDuplicaYCargaElExistente()
    {
        var (vm, repo, factory) = await CrearAsync();
        using (factory)
        {
            vm.CodigoBarras = "ABC";
            vm.Nombre = "Nombre tecleado por error";
            vm.Precio = "99";

            await vm.GuardarCommand.ExecuteAsync(null);

            Assert.Equal(2, (await repo.ObtenerTodosAsync()).Count);   // no se creó un tercero
            Assert.True(vm.EsEdicion);
            Assert.Equal("Amoxicilina 500", vm.Nombre);                // se cargó el registrado
            Assert.True(vm.MostrarAviso);
            Assert.Equal(12m, (await repo.ObtenerPorCodigoAsync("ABC"))!.Precio);   // intacto
        }
    }
}
