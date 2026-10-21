using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using FarmaGestion.Data;
using FarmaGestion.Models;

namespace FarmaGestion.Services;

/// <summary>
/// Servicio de autenticación que utiliza PBKDF2 (Rfc2898DeriveBytes) con SHA256
/// para el hashing seguro de contraseñas. Nunca almacena contraseñas en texto plano.
/// </summary>
public static class AuthService
{
    // Parámetros de seguridad para PBKDF2
    private const int SaltSize = 16;         // 128 bits de salt
    private const int HashSize = 32;         // 256 bits de hash
    private const int Iterations = 100_000;  // Iteraciones recomendadas por OWASP

    /// <summary>
    /// Genera un salt criptográfico aleatorio codificado en Base64.
    /// </summary>
    public static string GenerarSalt()
    {
        var saltBytes = RandomNumberGenerator.GetBytes(SaltSize);
        return Convert.ToBase64String(saltBytes);
    }

    /// <summary>
    /// Genera un hash PBKDF2-SHA256 de la contraseña con el salt proporcionado.
    /// </summary>
    public static string HashPassword(string password, string salt)
    {
        var saltBytes = Convert.FromBase64String(salt);

        using var pbkdf2 = new Rfc2898DeriveBytes(
            password,
            saltBytes,
            Iterations,
            HashAlgorithmName.SHA256);

        var hashBytes = pbkdf2.GetBytes(HashSize);
        return Convert.ToBase64String(hashBytes);
    }

    /// <summary>
    /// Verifica si una contraseña coincide con el hash almacenado.
    /// Usa comparación en tiempo constante para prevenir ataques de temporización.
    /// </summary>
    public static bool VerificarPassword(string password, string hashAlmacenado, string salt)
    {
        var hashIngresado = HashPassword(password, salt);

        // Comparación en tiempo constante para evitar timing attacks
        return CryptographicOperations.FixedTimeEquals(
            Convert.FromBase64String(hashIngresado),
            Convert.FromBase64String(hashAlmacenado));
    }

    /// <summary>
    /// Crea el super usuario "admin" si no existe en la base de datos.
    /// La contraseña NO está hardcodeada: se genera una aleatoria y se escribe en
    /// un archivo local (credenciales_admin.txt) para que el operador la consulte
    /// en el primer arranque y la cambie cuanto antes.
    /// Se ejecuta automáticamente al iniciar la aplicación.
    /// </summary>
    public static void SeedSuperUsuario(InventarioContext context)
    {
        // Si ya existe un usuario admin, no hacemos nada
        if (context.Usuarios.Any(u => u.NombreUsuario == "admin"))
            return;

        var passwordInicial = GenerarPasswordAleatoria();
        var salt = GenerarSalt();
        var hash = HashPassword(passwordInicial, salt);

        var superUsuario = new Usuario
        {
            NombreUsuario = "admin",
            PasswordHash = hash,
            PasswordSalt = salt,
            Rol = Rol.Admin,
            Activo = true,
            FechaCreacion = DateTime.Now
        };

        context.Usuarios.Add(superUsuario);
        context.SaveChanges();

        GuardarCredencialesIniciales(passwordInicial);
        LoggerService.Info("Super usuario 'admin' creado con contraseña aleatoria en el primer arranque.");
    }

    /// <summary>
    /// Genera una contraseña aleatoria robusta (con mayúscula, minúscula y dígito garantizados).
    /// </summary>
    private static string GenerarPasswordAleatoria()
    {
        const string mayus = "ABCDEFGHJKLMNPQRSTUVWXYZ";
        const string minus = "abcdefghijkmnopqrstuvwxyz";
        const string digitos = "23456789";
        const string especiales = "!@#$%*?";
        string todos = mayus + minus + digitos + especiales;

        char Tomar(string conjunto) => conjunto[RandomNumberGenerator.GetInt32(conjunto.Length)];

        // Garantizamos al menos un carácter de cada clase y completamos hasta 14.
        var chars = new System.Collections.Generic.List<char>
        {
            Tomar(mayus), Tomar(minus), Tomar(digitos), Tomar(especiales)
        };
        while (chars.Count < 14)
            chars.Add(Tomar(todos));

        // Mezcla criptográfica (Fisher-Yates) para no dejar las clases en posición fija.
        for (int i = chars.Count - 1; i > 0; i--)
        {
            int j = RandomNumberGenerator.GetInt32(i + 1);
            (chars[i], chars[j]) = (chars[j], chars[i]);
        }
        return new string(chars.ToArray());
    }

    private static void GuardarCredencialesIniciales(string password)
    {
        try
        {
            var contenido =
                "FarmaGestion — Credenciales iniciales del administrador\n" +
                "=======================================================\n" +
                "Usuario:    admin\n" +
                $"Contraseña: {password}\n\n" +
                "⚠️ Cambia esta contraseña tras el primer inicio de sesión y borra este archivo.\n";
            File.WriteAllText(AppPaths.RutaCredencialesAdmin, contenido);
        }
        catch (Exception ex)
        {
            LoggerService.Error("No se pudo escribir el archivo de credenciales iniciales", ex);
        }
    }
}
