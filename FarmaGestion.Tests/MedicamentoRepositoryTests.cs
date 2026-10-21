using System.Threading.Tasks;
using FarmaGestion.Data.Repositories;
using FarmaGestion.Models;
using Xunit;

namespace FarmaGestion.Tests;

public class MedicamentoRepositoryTests
{
    private static (MedicamentoRepository repo, TestDbContextFactory factory) Crear()
    {
        var factory = new TestDbContextFactory();
        return (new MedicamentoRepository(factory), factory);
    }

    [Fact]
    public async Task AgregarYObtener_FuncionaCorrectamente()
    {
        var (repo, factory) = Crear();
        using (factory)
        {
            await repo.AgregarAsync(new Medicamentos { CodigoBarras = "001", Nombre = "Paracetamol", Precio = 5m, Existencias = 10 });

            var todos = await repo.ObtenerTodosAsync();
            Assert.Single(todos);

            var encontrado = await repo.ObtenerPorCodigoAsync("001");
            Assert.NotNull(encontrado);
            Assert.Equal("Paracetamol", encontrado!.Nombre);
        }
    }

    [Fact]
    public async Task ExisteCodigo_DetectaDuplicados()
    {
        var (repo, factory) = Crear();
        using (factory)
        {
            await repo.AgregarAsync(new Medicamentos { CodigoBarras = "ABC", Nombre = "Ibuprofeno", Precio = 8m, Existencias = 3 });

            Assert.True(await repo.ExisteCodigoAsync("ABC"));
            Assert.False(await repo.ExisteCodigoAsync("XYZ"));
        }
    }

    [Fact]
    public async Task ImportarRegistros_SumaExistenciasSiYaExiste()
    {
        var (repo, factory) = Crear();
        using (factory)
        {
            await repo.AgregarAsync(new Medicamentos { CodigoBarras = "777", Nombre = "Aspirina", Precio = 4m, Existencias = 5 });

            await repo.ImportarRegistrosAsync(new[]
            {
                new RegistroMedicamentoCsv("777", "Aspirina", 6m, 10, null, null),   // existente: +10
                new RegistroMedicamentoCsv("888", "Amoxicilina", 12m, 7, null, null) // nuevo
            });

            var existente = await repo.ObtenerPorCodigoAsync("777");
            Assert.Equal(15, existente!.Existencias);      // 5 + 10
            Assert.Equal(6m, existente.Precio);            // precio actualizado

            var nuevo = await repo.ObtenerPorCodigoAsync("888");
            Assert.NotNull(nuevo);
            Assert.Equal(7, nuevo!.Existencias);
        }
    }
}
