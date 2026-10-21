using FarmaGestion.Models;

namespace FarmaGestion.Services;

/// <summary>
/// Mantiene el usuario autenticado durante la sesión. Lo consultan las vistas y
/// ViewModels para aplicar control de acceso por rol (RBAC). Es un punto único de
/// verdad; al cerrar sesión se limpia con <see cref="Limpiar"/>.
/// </summary>
public static class SesionActual
{
    public static Usuario? Usuario { get; private set; }

    public static bool EsAdmin => Rol.EsAdmin(Usuario?.Rol);

    public static void Establecer(Usuario usuario) => Usuario = usuario;

    public static void Limpiar() => Usuario = null;
}
