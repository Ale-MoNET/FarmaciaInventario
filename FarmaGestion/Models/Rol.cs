namespace FarmaGestion.Models;

/// <summary>
/// Roles soportados por la aplicación. Centraliza los valores para evitar
/// "magic strings" dispersos por el código (antes "Admin"/"Empleado" literales).
/// </summary>
public static class Rol
{
    public const string Admin = "Admin";
    public const string Empleado = "Empleado";

    /// <summary>Lista de roles disponibles, p. ej. para poblar un ComboBox.</summary>
    public static readonly string[] Todos = { Admin, Empleado };

    public static bool EsAdmin(string? rol) =>
        string.Equals(rol, Admin, System.StringComparison.Ordinal);
}
