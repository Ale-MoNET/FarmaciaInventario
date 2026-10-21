using System;
using System.IO;

namespace FarmaGestion.Services;

/// <summary>
/// Centraliza las rutas de archivos de la aplicación. La base de datos y los logs
/// se guardan en la carpeta de datos del usuario (Application Support en macOS,
/// AppData en Windows) en lugar del directorio de trabajo, de modo que la app
/// funcione igual sin importar desde dónde se ejecute (instalador, acceso directo, etc.).
/// </summary>
public static class AppPaths
{
    private const string CarpetaApp = "FarmaGestion";

    /// <summary>Carpeta base de datos de la aplicación; se crea si no existe.</summary>
    public static string CarpetaDatos
    {
        get
        {
            var baseDir = Environment.GetFolderPath(
                Environment.SpecialFolder.ApplicationData,
                Environment.SpecialFolderOption.Create);
            var dir = Path.Combine(baseDir, CarpetaApp);
            Directory.CreateDirectory(dir);
            return dir;
        }
    }

    /// <summary>Ruta completa del archivo SQLite.</summary>
    public static string RutaBaseDatos => Path.Combine(CarpetaDatos, "inventario_medicamentos.db");

    /// <summary>Ruta del archivo de log de la aplicación.</summary>
    public static string RutaLog => Path.Combine(CarpetaDatos, "farmagestion.log");

    /// <summary>Ruta donde se escriben las credenciales del admin generadas en el primer arranque.</summary>
    public static string RutaCredencialesAdmin => Path.Combine(CarpetaDatos, "credenciales_admin.txt");
}
