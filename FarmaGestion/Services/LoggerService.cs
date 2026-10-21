using System;
using System.IO;

namespace FarmaGestion.Services;

/// <summary>
/// Logger sencillo a archivo. Reemplaza los Console.WriteLine dispersos y deja
/// un rastro mínimo para diagnosticar incidentes (sin dependencias externas).
/// Para un sistema más completo se recomienda migrar a Serilog/Microsoft.Extensions.Logging.
/// </summary>
public static class LoggerService
{
    private static readonly object _candado = new();

    public static void Info(string mensaje) => Escribir("INFO", mensaje);

    public static void Advertencia(string mensaje) => Escribir("WARN", mensaje);

    public static void Error(string mensaje, Exception? ex = null) =>
        Escribir("ERROR", ex == null ? mensaje : $"{mensaje} :: {ex}");

    private static void Escribir(string nivel, string mensaje)
    {
        try
        {
            var linea = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [{nivel}] {mensaje}{Environment.NewLine}";
            lock (_candado)
            {
                File.AppendAllText(AppPaths.RutaLog, linea);
            }
        }
        catch
        {
            // El logging nunca debe tumbar la aplicación.
        }
    }
}
