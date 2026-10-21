using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FarmaGestion.Models;
using Microsoft.EntityFrameworkCore;

namespace FarmaGestion.Data.Repositories;

public class UsuarioRepository : IUsuarioRepository
{
    private readonly IDbContextFactory<InventarioContext> _factory;

    public UsuarioRepository(IDbContextFactory<InventarioContext> factory)
    {
        _factory = factory;
    }

    public async Task<Usuario?> ObtenerActivoPorNombreAsync(string nombreUsuario)
    {
        await using var context = await _factory.CreateDbContextAsync();
        return await context.Usuarios
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.NombreUsuario == nombreUsuario && u.Activo);
    }

    public async Task<Usuario?> ObtenerPorIdAsync(int id)
    {
        await using var context = await _factory.CreateDbContextAsync();
        return await context.Usuarios.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id);
    }

    public async Task<List<Usuario>> ObtenerTodosAsync()
    {
        await using var context = await _factory.CreateDbContextAsync();
        return await context.Usuarios
            .AsNoTracking()
            .OrderBy(u => u.NombreUsuario)
            .ToListAsync();
    }

    public async Task<bool> ExisteNombreAsync(string nombreUsuario)
    {
        await using var context = await _factory.CreateDbContextAsync();
        return await context.Usuarios.AnyAsync(u => u.NombreUsuario == nombreUsuario);
    }

    public async Task CrearAsync(Usuario usuario)
    {
        await using var context = await _factory.CreateDbContextAsync();
        context.Usuarios.Add(usuario);
        await context.SaveChangesAsync();
    }

    public async Task ActualizarAsync(int id, string nombreUsuario, string rol, bool activo,
        string? nuevoPasswordHash, string? nuevoSalt)
    {
        await using var context = await _factory.CreateDbContextAsync();
        var usuario = await context.Usuarios.FindAsync(id);
        if (usuario == null) return;

        usuario.NombreUsuario = nombreUsuario;
        usuario.Rol = rol;
        usuario.Activo = activo;

        if (nuevoPasswordHash != null && nuevoSalt != null)
        {
            usuario.PasswordHash = nuevoPasswordHash;
            usuario.PasswordSalt = nuevoSalt;
        }

        await context.SaveChangesAsync();
    }

    public async Task CambiarActivoAsync(int id, bool activo)
    {
        await using var context = await _factory.CreateDbContextAsync();
        var usuario = await context.Usuarios.FindAsync(id);
        if (usuario == null) return;

        usuario.Activo = activo;
        await context.SaveChangesAsync();
    }

    public async Task EliminarAsync(int id)
    {
        await using var context = await _factory.CreateDbContextAsync();
        var usuario = await context.Usuarios.FindAsync(id);
        if (usuario == null) return;

        context.Usuarios.Remove(usuario);
        await context.SaveChangesAsync();
    }
}
