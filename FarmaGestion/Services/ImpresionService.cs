using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace FarmaGestion.Services;

/// <summary>
/// Envía documentos a la impresora o al visor del sistema operativo.
///
/// Avalonia no trae API de impresión propia, así que se delega en el sistema:
/// en Windows se usa el verbo "print" (manda el PDF a la impresora predeterminada)
/// y en macOS/Linux se abre el visor de PDF del sistema, desde donde el usuario
/// imprime con Cmd/Ctrl + P y elige impresora.
/// </summary>
public static class ImpresionService
{
    /// <summary>Escribe el documento en la carpeta temporal de la app y devuelve la ruta.</summary>
    public static string GuardarEnTemporal(byte[] contenido, string nombreArchivo)
    {
        var carpeta = Path.Combine(Path.GetTempPath(), "FarmaGestion");
        Directory.CreateDirectory(carpeta);

        var ruta = Path.Combine(carpeta, LimpiarNombre(nombreArchivo));
        File.WriteAllBytes(ruta, contenido);
        return ruta;
    }

    /// <summary>
    /// Guarda el PDF de forma temporal y lo manda a imprimir con el mecanismo del
    /// sistema operativo. Devuelve un mensaje listo para mostrarse en la interfaz.
    /// </summary>
    public static (bool Exito, string Mensaje) Imprimir(byte[] pdf, string nombreArchivo)
    {
        try
        {
            var ruta = GuardarEnTemporal(pdf, nombreArchivo);

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                try
                {
                    Ejecutar(new ProcessStartInfo(ruta) { Verb = "print", UseShellExecute = true });
                    LoggerService.Info($"Comprobante enviado a la impresora predeterminada: {ruta}");
                    return (true, "Enviado a la impresora predeterminada.");
                }
                catch (Exception ex)
                {
                    // Si no hay aplicación asociada al verbo "print", abrimos el visor.
                    LoggerService.Advertencia($"No se pudo usar la impresión directa ({ex.Message}); se abre el visor.");
                    AbrirArchivo(ruta);
                    return (true, "Se abrió el visor del sistema. Usa Ctrl + P para imprimir.");
                }
            }

            AbrirArchivo(ruta);
            string atajo = RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "Cmd + P" : "Ctrl + P";
            return (true, $"Se abrió el visor del sistema. Usa {atajo} para imprimir.");
        }
        catch (Exception ex)
        {
            LoggerService.Error("No se pudo imprimir el comprobante", ex);
            return (false, "No se pudo abrir la impresión. El PDF sí se puede descargar.");
        }
    }

    /// <summary>Abre un archivo con la aplicación predeterminada del sistema.</summary>
    public static void AbrirArchivo(string ruta)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Ejecutar(new ProcessStartInfo(ruta) { UseShellExecute = true });
            return;
        }

        var comando = RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "open" : "xdg-open";
        var info = new ProcessStartInfo(comando) { UseShellExecute = false };
        info.ArgumentList.Add(ruta);
        Ejecutar(info);
    }

    private static void Ejecutar(ProcessStartInfo info)
    {
        using var proceso = Process.Start(info);
    }

    /// <summary>Quita del nombre los caracteres que el sistema de archivos no admite.</summary>
    private static string LimpiarNombre(string nombre)
    {
        var invalidos = Path.GetInvalidFileNameChars();
        var limpio = new string(nombre.Select(c => invalidos.Contains(c) ? '-' : c).ToArray());
        return string.IsNullOrWhiteSpace(limpio) ? "documento.pdf" : limpio;
    }
}
