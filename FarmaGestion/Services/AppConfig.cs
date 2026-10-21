using System;
using Microsoft.Extensions.Configuration;

namespace FarmaGestion.Services;

/// <summary>
/// Opciones de robustez de contraseñas, configurables desde appsettings.json.
/// </summary>
public class PoliticaPasswordOptions
{
    public int LongitudMinima { get; set; } = 8;
    public bool RequiereMayuscula { get; set; } = true;
    public bool RequiereDigito { get; set; } = true;
}

/// <summary>
/// Configuración de la aplicación leída de appsettings.json (si existe).
/// Centraliza la cadena de conexión y la política de contraseñas para que no
/// estén dispersas ni hardcodeadas en el código.
/// </summary>
public static class AppConfig
{
    public static IConfiguration? Configuration { get; private set; }
    public static PoliticaPasswordOptions PoliticaPassword { get; private set; } = new();
    public static string ConnectionString { get; private set; } = $"Data Source={AppPaths.RutaBaseDatos}";

    /// <summary>Carga la configuración. Debe llamarse una vez al arrancar la app.</summary>
    public static void Inicializar()
    {
        try
        {
            Configuration = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
                .Build();

            PoliticaPassword = Configuration.GetSection("PoliticaPassword").Get<PoliticaPasswordOptions>()
                               ?? new PoliticaPasswordOptions();

            var cs = Configuration.GetConnectionString("Inventario");
            if (!string.IsNullOrWhiteSpace(cs))
                ConnectionString = cs;
        }
        catch (Exception ex)
        {
            // Si la configuración falla, seguimos con los valores por defecto.
            LoggerService.Error("No se pudo cargar appsettings.json; se usan valores por defecto", ex);
        }
    }
}
