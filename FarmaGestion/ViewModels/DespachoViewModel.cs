using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using FarmaGestion.Data.Repositories;
using FarmaGestion.Models;
using FarmaGestion.Services;

namespace FarmaGestion.ViewModels;

public partial class ItemCarrito : ObservableObject
{
    public Medicamentos Medicamento { get; set; } = null!;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Subtotal))]
    [NotifyPropertyChangedFor(nameof(SubtotalTexto))]
    private int _cantidad;

    /// <summary>De qué lotes saldrán las unidades (regla FEFO), p. ej. "Sale de L01 (3) + L02 (2)".</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TieneDetalleLotes))]
    private string _detalleLotes = string.Empty;

    /// <summary>Aviso no bloqueante: la cantidad incluye unidades de lotes vencidos.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TieneAdvertencia))]
    private string? _advertencia;

    public bool TieneDetalleLotes => !string.IsNullOrEmpty(DetalleLotes);
    public bool TieneAdvertencia => !string.IsNullOrEmpty(Advertencia);

    public decimal Subtotal => Medicamento.Precio * Cantidad;
    public string PrecioTexto => Medicamento.Precio.ToString("C");
    public string SubtotalTexto => Subtotal.ToString("C");

    // Callbacks conectados por el ViewModel al agregar el ítem.
    public Action? OnIncrementar;
    public Action? OnDecrementar;
    public Action? OnQuitar;

    [RelayCommand] private void Incrementar() => OnIncrementar?.Invoke();
    [RelayCommand] private void Decrementar() => OnDecrementar?.Invoke();
    [RelayCommand] private void Quitar() => OnQuitar?.Invoke();

    partial void OnCantidadChanged(int value) => ActualizarAsignacion();

    /// <summary>Recalcula el reparto por lotes con las existencias conocidas del producto.</summary>
    public void ActualizarAsignacion()
    {
        var reparto = AsignadorLotes.Asignar(Medicamento.Lotes, Cantidad, DateTime.Today);
        DetalleLotes = reparto.Asignaciones.Count > 0 ? $"Sale de {reparto.Descripcion}" : string.Empty;
        Advertencia = reparto.UnidadesVencidas > 0
            ? $"Incluye {reparto.UnidadesVencidas} u de lotes vencidos ({string.Join(", ", reparto.LotesVencidos)})"
            : null;
    }
}

public partial class DespachoViewModel : ObservableObject
{
    private readonly IMedicamentoRepository _medicamentoRepo;
    private readonly IDespachoRepository _despachoRepo;

    [ObservableProperty] private string _codigoBusqueda = string.Empty;
    [ObservableProperty] private decimal _totalDespacho;
    [ObservableProperty] private string _totalTexto = 0m.ToString("C");

    [ObservableProperty] private string _mensajeAlerta = string.Empty;
    [ObservableProperty] private bool _hayAlerta;

    [ObservableProperty] private bool _carritoVacio = true;
    [ObservableProperty] private string _itemCountLabel = "0 artículos";

    public ObservableCollection<ItemCarrito> Carritos { get; } = new();

    /// <summary>
    /// Se invoca tras un despacho exitoso para que la vista muestre el comprobante
    /// (la ventana emergente con las opciones de descargar en PDF o imprimir).
    /// </summary>
    public Func<ComprobanteDespacho, Task>? OnDespachoConfirmado;

    public DespachoViewModel(IMedicamentoRepository medicamentoRepo, IDespachoRepository despachoRepo)
    {
        _medicamentoRepo = medicamentoRepo;
        _despachoRepo = despachoRepo;
    }

    [RelayCommand]
    private async Task AgregarCarritoAsync()
    {
        HayAlerta = false;
        MensajeAlerta = string.Empty;

        if (string.IsNullOrWhiteSpace(CodigoBusqueda)) return;

        var producto = await _medicamentoRepo.ObtenerPorCodigoAsync(CodigoBusqueda.Trim());

        if (producto == null)
        {
            MostrarAlerta($"No se encontró un producto para “{CodigoBusqueda.Trim()}”.");
            CodigoBusqueda = string.Empty;
            return;
        }

        if (producto.Existencias <= 0)
        {
            MostrarAlerta($"“{producto.Nombre}” está agotado.");
            CodigoBusqueda = string.Empty;
            return;
        }

        var existente = Carritos.FirstOrDefault(i => i.Medicamento.Id == producto.Id);
        if (existente != null)
        {
            // Refrescamos los lotes por si cambiaron desde que se agregó.
            existente.Medicamento = producto;
            if (existente.Cantidad < producto.Existencias)
            {
                existente.Cantidad++;
            }
            else
            {
                existente.ActualizarAsignacion();
                MostrarAlerta($"No hay más existencias de “{producto.Nombre}”.");
            }
        }
        else
        {
            var item = new ItemCarrito { Medicamento = producto, Cantidad = 1 };
            ConectarItem(item);
            Carritos.Add(item);
            AvisarSiHayVencidos(producto, item);
        }

        CodigoBusqueda = string.Empty;
        Recalcular();
    }

    /// <summary>
    /// Política acordada: los lotes vencidos no bloquean la venta, solo se advierte.
    /// Mientras haya lotes vigentes, las unidades salen de ellos y se recuerda dar de baja.
    /// </summary>
    private void AvisarSiHayVencidos(Medicamentos producto, ItemCarrito item)
    {
        if (producto.ExistenciasVencidas <= 0) return;

        var lotesVencidos = string.Join(", ", producto.Lotes
            .Where(l => l.Vencido && l.Existencias > 0)
            .Select(l => l.NumeroLote));

        if (item.TieneAdvertencia)
        {
            MostrarAlerta($"“{producto.Nombre}” solo tiene unidades vencidas (lote {lotesVencidos}). " +
                          "Se despacharán con advertencia; revisa antes de confirmar.");
        }
        else
        {
            MostrarAlerta($"“{producto.Nombre}” tiene {producto.ExistenciasVencidas} u vencidas en el lote {lotesVencidos}. " +
                          "No se despachan mientras haya vigentes; dales de baja en Inventario.");
        }
    }

    [RelayCommand(CanExecute = nameof(PuedeConfirmar))]
    private async Task ConfirmarDespachoAsync()
    {
        HayAlerta = false;
        MensajeAlerta = string.Empty;

        var lineas = Carritos.Select(i => new LineaDespacho(i.Medicamento.Id, i.Cantidad));
        var resultado = await _despachoRepo.ConfirmarAsync(lineas);

        if (resultado.Exito)
        {
            WeakReferenceMessenger.Default.Send(new InventarioActualizadoMessage());
            Carritos.Clear();
            CodigoBusqueda = string.Empty;
            Recalcular();

            var mensaje = resultado.Advertencias.Count == 0
                ? resultado.Mensaje
                : resultado.Mensaje + " " + string.Join(" ", resultado.Advertencias);
            MostrarAlerta(mensaje);

            await MostrarComprobanteAsync(resultado.DespachoId);
            return;
        }

        MostrarAlerta(resultado.Mensaje);
    }

    /// <summary>
    /// Recupera el despacho recién registrado y se lo entrega a la vista para el
    /// comprobante. Si algo falla no se interrumpe el flujo: el despacho ya quedó
    /// guardado y el comprobante puede consultarse después en reportes.
    /// </summary>
    private async Task MostrarComprobanteAsync(int? despachoId)
    {
        var mostrarComprobante = OnDespachoConfirmado;
        if (despachoId is null || mostrarComprobante is null) return;

        try
        {
            var comprobante = await _despachoRepo.ObtenerComprobanteAsync(despachoId.Value);
            if (comprobante is null)
            {
                LoggerService.Info($"El despacho #{despachoId} se guardó pero no se pudo recuperar para el comprobante.");
                return;
            }

            var usuario = SesionActual.Usuario?.NombreUsuario ?? string.Empty;
            await mostrarComprobante(comprobante with { Atendio = usuario });
        }
        catch (Exception ex)
        {
            LoggerService.Error("No se pudo mostrar el comprobante del despacho", ex);
        }
    }

    private bool PuedeConfirmar() => Carritos.Count > 0;

    private void ConectarItem(ItemCarrito item)
    {
        item.OnIncrementar = () =>
        {
            if (item.Cantidad < item.Medicamento.Existencias)
            {
                item.Cantidad++;
                Recalcular();
            }
            else
            {
                MostrarAlerta($"No hay más existencias de “{item.Medicamento.Nombre}”.");
            }
        };
        item.OnDecrementar = () =>
        {
            item.Cantidad--;
            if (item.Cantidad <= 0) Carritos.Remove(item);
            Recalcular();
        };
        item.OnQuitar = () =>
        {
            Carritos.Remove(item);
            Recalcular();
        };
    }

    private void Recalcular()
    {
        TotalDespacho = Carritos.Sum(i => i.Subtotal);
        TotalTexto = TotalDespacho.ToString("C");
        CarritoVacio = Carritos.Count == 0;
        int unidades = Carritos.Sum(i => i.Cantidad);
        ItemCountLabel = $"{unidades} artículo{(unidades == 1 ? "" : "s")}";
        ConfirmarDespachoCommand.NotifyCanExecuteChanged();
    }

    private void MostrarAlerta(string mensaje)
    {
        MensajeAlerta = mensaje;
        HayAlerta = !string.IsNullOrWhiteSpace(mensaje);
    }
}
