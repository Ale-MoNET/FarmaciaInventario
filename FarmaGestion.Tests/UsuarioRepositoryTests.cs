using System.Threading.Tasks;
using FarmaGestion.Data.Repositories;
using FarmaGestion.Models;
using FarmaGestion.Services;
using Xunit;

namespace FarmaGestion.Tests;

public class UsuarioRepositoryTests
{
    private static async Task<int> CrearUsuarioAsync(IUsuarioRepository repo, string nombre,
        string rol = "Empleado", bool activo = true)
    {
        var salt = AuthService.GenerarSalt();
        var u = new Usuario
        {
            NombreUsuario = nombre,
            PasswordHash = AuthService.HashPassword("Clave123", salt),
            PasswordSalt = salt,
            Rol = rol,
            Activo = activo
        };
        await repo.CrearAsync(u);
        return u.Id;
    }

    [Fact]
    public async Task ObtenerActivoPorNombre_IgnoraInactivos()
    {
        using var factory = new TestDbContextFactory();
        var repo = new UsuarioRepository(factory);
        await CrearUsuarioAsync(repo, "activo");
        await CrearUsuarioAsync(repo, "inactivo", activo: false);

        Assert.NotNull(await repo.ObtenerActivoPorNombreAsync("activo"));
        Assert.Null(await repo.ObtenerActivoPorNombreAsync("inactivo"));
    }

    [Fact]
    public async Task ExisteNombre_DetectaDuplicados()
    {
        using var factory = new TestDbContextFactory();
        var repo = new UsuarioRepository(factory);
        await CrearUsuarioAsync(repo, "juan");

        Assert.True(await repo.ExisteNombreAsync("juan"));
        Assert.False(await repo.ExisteNombreAsync("pedro"));
    }

    [Fact]
    public async Task Actualizar_SinNuevaPassword_ConservaCredenciales()
    {
        using var factory = new TestDbContextFactory();
        var repo = new UsuarioRepository(factory);
        var id = await CrearUsuarioAsync(repo, "maria", rol: Rol.Empleado);

        var antes = await repo.ObtenerPorIdAsync(id);

        await repo.ActualizarAsync(id, "maria", Rol.Admin, activo: true, null, null);

        var despues = await repo.ObtenerPorIdAsync(id);
        Assert.Equal(Rol.Admin, despues!.Rol);
        Assert.Equal(antes!.PasswordHash, despues.PasswordHash); // hash intacto
        Assert.Equal(antes.PasswordSalt, despues.PasswordSalt);
    }

    [Fact]
    public async Task Actualizar_ConNuevaPassword_CambiaCredenciales()
    {
        using var factory = new TestDbContextFactory();
        var repo = new UsuarioRepository(factory);
        var id = await CrearUsuarioAsync(repo, "luis");

        var antes = await repo.ObtenerPorIdAsync(id);
        var nuevoSalt = AuthService.GenerarSalt();
        var nuevoHash = AuthService.HashPassword("OtraClave9", nuevoSalt);

        await repo.ActualizarAsync(id, "luis", Rol.Empleado, true, nuevoHash, nuevoSalt);

        var despues = await repo.ObtenerPorIdAsync(id);
        Assert.NotEqual(antes!.PasswordHash, despues!.PasswordHash);
        Assert.True(AuthService.VerificarPassword("OtraClave9", despues.PasswordHash, despues.PasswordSalt));
    }
}
