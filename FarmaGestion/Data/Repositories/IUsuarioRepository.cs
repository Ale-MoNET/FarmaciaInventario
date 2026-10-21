using System.Collections.Generic;
using System.Threading.Tasks;
using FarmaGestion.Models;

namespace FarmaGestion.Data.Repositories;

public interface IUsuarioRepository
{
    Task<Usuario?> ObtenerActivoPorNombreAsync(string nombreUsuario);
    Task<Usuario?> ObtenerPorIdAsync(int id);
    Task<List<Usuario>> ObtenerTodosAsync();
    Task<bool> ExisteNombreAsync(string nombreUsuario);
    Task CrearAsync(Usuario usuario);

    /// <summary>
    /// Actualiza nombre, rol y estado de un usuario. Si <paramref name="nuevoPasswordHash"/>
    /// y <paramref name="nuevoSalt"/> no son nulos, también cambia la contraseña; de lo
    /// contrario, conserva la existente.
    /// </summary>
    Task ActualizarAsync(int id, string nombreUsuario, string rol, bool activo,
        string? nuevoPasswordHash, string? nuevoSalt);

    /// <summary>Activa o desactiva un usuario.</summary>
    Task CambiarActivoAsync(int id, bool activo);

    /// <summary>Elimina un usuario de forma permanente.</summary>
    Task EliminarAsync(int id);
}
