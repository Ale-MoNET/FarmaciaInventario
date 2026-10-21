using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using FarmaGestion.Data.Repositories;
using FarmaGestion.Models;
using FarmaGestion.Services;

namespace FarmaGestion.ViewModels;

/// <summary>
/// Ventana "Ingresar mercadería": se escanea el producto y se capturan lote, vencimiento y
/// cantidad. Si el lote ya existe se le suman las unidades; si no, se crea.
/// </summary>
public partial class IngresoMercaderiaViewModel : ObservableObject
{
    private readonly IMedicamentoRepository _medicamentoRepo;
    private Medicamentos? _producto;

    [ObservableProperty] private string _codigoBarras = string.Empty;
    [ObservableProperty] private string _nombreProducto = string.Empty;
    [ObservableProperty] private string _resumenProducto = string.Empty;
    [ObservableProperty] private bool _productoEncontrado;

    [ObservableProperty] private string _numeroLote = string.Empty;
    [ObservableProperty] private DateTime? _fechaVencimiento;
    [ObservableProperty] private string _cantidad = string.Empty;

    [ObservableProperty] private string _mensaje = string.Empty;
    [ObservableProperty] private bool _mostrarError;
    [ObservableProperty] private bool _mostrarExito;

    /// <summary>La vista lo usa para ofrecer registrar el producto cuando el código no existe.</summary>
    public Func<string, Task>? OnProductoNoEncontrado;

    /// <summary>Se invoca tras un ingreso correcto (la vista devuelve el foco al código).</summary>
    public Action? OnIngresoRegistrado;

    public IngresoMercaderiaViewModel(IMedicamentoRepository medicamentoRepo)
    {
        _medicamentoRepo = medicamentoRepo;
    }

    /// <summary>Busca el producto del código escrito. Devuelve true si lo encontró.</summary>
    public async Task<bool> BuscarProductoAsync()
    {
        LimpiarMensajes();
        var codigo = CodigoBarras.Trim();
        if (codigo.Length == 0) return false;

        try
        {
            _producto = await _medicamentoRepo.ObtenerPorCodigoAsync(codigo);

            if (_producto == null && OnProductoNoEncontrado != null)
            {
                // La vista puede abrir la ventana de registro; al volver, reintentamos una vez.
                await OnProductoNoEncontrado(codigo);
                _producto = await _medicamentoRepo.ObtenerPorCodigoAsync(codigo);
            }
        }
        catch (Exception ex)
        {
            LoggerService.Error("Error al buscar el producto para ingreso", ex);
            MostrarMensajeError("No se pudo consultar el inventario. Inténtalo de nuevo.");
            return false;
        }

        if (_producto == null)
        {
            ProductoEncontrado = false;
            NombreProducto = string.Empty;
            ResumenProducto = string.Empty;
            MostrarMensajeError($"No existe un producto con el código {codigo}. Regístralo primero.");
            return false;
        }

        ProductoEncontrado = true;
        NombreProducto = _producto.Nombre;
        int lotes = _producto.NumeroLotes;
        ResumenProducto = lotes == 0
            ? "Sin existencias registradas"
            : $"Stock actual: {_producto.Existencias} u en {lotes} lote{(lotes == 1 ? "" : "s")}";
        return true;
    }

    [RelayCommand]
    private async Task RegistrarAsync()
    {
        LimpiarMensajes();

        if (_producto == null)
        {
            MostrarMensajeError("Escanea o escribe primero el código del producto.");
            return;
        }

        var lote = NumeroLote.Trim();
        if (lote.Length == 0)
        {
            MostrarMensajeError("El número de lote es obligatorio.");
            return;
        }
        if (FechaVencimiento is null)
        {
            MostrarMensajeError("Indica la fecha de vencimiento del lote.");
            return;
        }
        if (FechaVencimiento.Value.Date < DateTime.Today)
        {
            MostrarMensajeError("La fecha de vencimiento ya pasó; revisa el año.");
            return;
        }
        if (!int.TryParse(Cantidad.Trim(), out var cantidad) || cantidad <= 0)
        {
            MostrarMensajeError("La cantidad debe ser un número entero mayor que cero.");
            return;
        }

        try
        {
            var resultado = await _medicamentoRepo.IngresarLoteAsync(_producto.Id, lote, FechaVencimiento.Value, cantidad);
            if (!resultado.Exito)
            {
                MostrarMensajeError(resultado.Mensaje);
                return;
            }

            WeakReferenceMessenger.Default.Send(new InventarioActualizadoMessage());

            Mensaje = resultado.Mensaje;
            MostrarExito = true;

            // Listo para el siguiente escaneo.
            _producto = null;
            ProductoEncontrado = false;
            NombreProducto = string.Empty;
            ResumenProducto = string.Empty;
            CodigoBarras = string.Empty;
            NumeroLote = string.Empty;
            FechaVencimiento = null;
            Cantidad = string.Empty;

            OnIngresoRegistrado?.Invoke();
        }
        catch (Exception ex)
        {
            LoggerService.Error("Error al registrar el ingreso de mercadería", ex);
            MostrarMensajeError("No se pudo registrar el ingreso. Inténtalo de nuevo.");
        }
    }

    private void LimpiarMensajes()
    {
        Mensaje = string.Empty;
        MostrarError = false;
        MostrarExito = false;
    }

    private void MostrarMensajeError(string mensaje)
    {
        Mensaje = mensaje;
        MostrarError = true;
        MostrarExito = false;
    }
}
