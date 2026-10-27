using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using FarmaGestion.Data.Repositories;
using FarmaGestion.Services;

namespace FarmaGestion.ViewModels;

/// <summary>
/// Ventana "Deshacer despacho": devuelve las unidades a sus lotes dejando registro del
/// motivo y de quién lo hizo. Regla de acceso: el administrador puede deshacer cualquier
/// despacho; un empleado solo los del día en curso.
/// </summary>
public partial class AnularDespachoViewModel : ObservableObject
{
    private readonly IDespachoRepository _despachoRepo;
    private ComprobanteDespacho _comprobante = null!;

    [ObservableProperty] private string _encabezado = string.Empty;
    [ObservableProperty] private string _detalle = string.Empty;
    [ObservableProperty] private string _motivo = string.Empty;

    /// <summary>Qué se va a devolver: una línea por producto y lote, p. ej. "5 × Paracetamol · Lote L1".</summary>
    public ObservableCollection<string> Productos { get; } = new();

    /// <summary>Por qué no se puede deshacer (vacío cuando sí se puede).</summary>
    [ObservableProperty] private string _bloqueo = string.Empty;
    [ObservableProperty] private bool _estaBloqueado;

    [ObservableProperty] private string _mensajeError = string.Empty;
    [ObservableProperty] private bool _mostrarError;

    /// <summary>Se invoca con el resultado del repositorio cuando el despacho quedó deshecho.</summary>
    public Action<string>? OnDeshecho;

    public AnularDespachoViewModel(IDespachoRepository despachoRepo)
    {
        _despachoRepo = despachoRepo;
    }

    public void Inicializar(ComprobanteDespacho comprobante)
    {
        _comprobante = comprobante;
        Encabezado = $"Despacho #{comprobante.Id:D5} · {comprobante.Fecha:dd/MM/yyyy HH:mm}";
        Detalle = $"{comprobante.Unidades} unidad{(comprobante.Unidades == 1 ? "" : "es")} en " +
                  $"{comprobante.Lineas.Count} línea{(comprobante.Lineas.Count == 1 ? "" : "s")} · " +
                  $"total {comprobante.Total:C}";

        Productos.Clear();
        foreach (var linea in comprobante.Lineas)
        {
            string lote = string.IsNullOrWhiteSpace(linea.Lote) ? string.Empty : $" · Lote {linea.Lote}";
            Productos.Add($"{linea.Cantidad} × {linea.Nombre}{lote}");
        }

        Bloqueo = MotivoBloqueo(comprobante, SesionActual.EsAdmin, DateTime.Today);
        EstaBloqueado = Bloqueo.Length > 0;
    }

    /// <summary>
    /// Texto que explica por qué no puede deshacerse el despacho, o cadena vacía si procede.
    /// Es estática y recibe la fecha para poder probarse sin sesión.
    /// </summary>
    public static string MotivoBloqueo(ComprobanteDespacho comprobante, bool esAdmin, DateTime hoy)
    {
        if (comprobante.Anulado)
            return $"Este despacho ya fue deshecho el {comprobante.FechaAnulacion:dd/MM/yyyy HH:mm}" +
                   (string.IsNullOrWhiteSpace(comprobante.UsuarioAnulacion) ? "." : $" por {comprobante.UsuarioAnulacion}.");

        if (!comprobante.TieneLotes)
            return "Este despacho es anterior al control por lotes y no se puede deshacer automáticamente. " +
                   "Si hace falta, reingresa las unidades desde Ingreso de mercadería.";

        if (!esAdmin && comprobante.Fecha.Date != hoy.Date)
            return "Solo el administrador puede deshacer despachos de días anteriores. " +
                   "Pídele que lo haga desde Reportes.";

        return string.Empty;
    }

    [RelayCommand]
    private async Task DeshacerAsync()
    {
        MostrarError = false;
        MensajeError = string.Empty;

        if (EstaBloqueado)
        {
            Error(Bloqueo);
            return;
        }
        if (string.IsNullOrWhiteSpace(Motivo))
        {
            Error("Escribe el motivo: quedará registrado junto con tu usuario.");
            return;
        }

        try
        {
            var usuario = SesionActual.Usuario?.NombreUsuario ?? string.Empty;
            var resultado = await _despachoRepo.AnularAsync(_comprobante.Id, Motivo.Trim(), usuario);
            if (!resultado.Exito)
            {
                Error(resultado.Mensaje);
                return;
            }

            WeakReferenceMessenger.Default.Send(new InventarioActualizadoMessage());
            OnDeshecho?.Invoke(resultado.Mensaje);
        }
        catch (Exception ex)
        {
            LoggerService.Error($"Error al deshacer el despacho #{_comprobante.Id}", ex);
            Error("No se pudo deshacer el despacho. Inténtalo de nuevo.");
        }
    }

    private void Error(string mensaje)
    {
        MensajeError = mensaje;
        MostrarError = true;
    }
}
