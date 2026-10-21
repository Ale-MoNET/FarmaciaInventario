using System;
using System.Collections.Generic;
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

/// <summary>Opción del selector de lote en caja: automático (FEFO) o un lote concreto.</summary>
public sealed class OpcionLote
{
    public int? LoteId { get; init; }
    public Lote? Lote { get; init; }
    public string Texto { get; init; } = string.Empty;
}

public partial class ItemCarrito : ObservableObject
{
    private Medicamentos _medicamento = null!;

    /// <summary>
    /// Producto con sus lotes. Al reasignarlo (p. ej. tras volver a escanearlo) se refrescan
    /// las opciones de lote conservando la elección si el lote sigue disponible.
    /// </summary>
    public Medicamentos Medicamento
    {
        get => _medicamento;
        set
        {
            _medicamento = value;
            ReconstruirOpcionesLote();
        }
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Subtotal))]
    [NotifyPropertyChangedFor(nameof(SubtotalTexto))]
    private int _cantidad;

    /// <summary>De qué lotes saldrán las unidades, p. ej. "Sale de L01 (3) + L02 (2)".</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TieneDetalleLotes))]
    private string _detalleLotes = string.Empty;

    /// <summary>Aviso no bloqueante: la cantidad incluye unidades de lotes vencidos.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TieneAdvertencia))]
    private string? _advertencia;

    /// <summary>Opciones del selector: la primera es "automático"; luego cada lote con existencias.</summary>
    public ObservableCollection<OpcionLote> OpcionesLote { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Disponible))]
    private OpcionLote? _opcionLoteSeleccionada;

    /// <summary>El selector solo se muestra cuando hay más de un lote entre los que elegir.</summary>
    [ObservableProperty] private bool _mostrarSelectorLote;

    public bool TieneDetalleLotes => !string.IsNullOrEmpty(DetalleLotes);
    public bool TieneAdvertencia => !string.IsNullOrEmpty(Advertencia);

    /// <summary>Unidades vendibles: las del lote elegido, o todas las del producto en automático.</summary>
    public int Disponible => OpcionLoteSeleccionada?.Lote?.Existencias ?? Medicamento.Existencias;

    public decimal Subtotal => Medicamento.Precio * Cantidad;
    public string PrecioTexto => Medicamento.Precio.ToString("C");
    public string SubtotalTexto => Subtotal.ToString("C");

    // Callbacks conectados por el ViewModel al agregar el ítem.
    public Action? OnIncrementar;
    public Action? OnDecrementar;
    public Action? OnQuitar;
    public Action? OnSeleccionLoteCambiada;
    public Action? OnCantidadCambiada;

    [RelayCommand] private void Incrementar() => OnIncrementar?.Invoke();
    [RelayCommand] private void Decrementar() => OnDecrementar?.Invoke();
    [RelayCommand] private void Quitar() => OnQuitar?.Invoke();

    partial void OnCantidadChanged(int value)
    {
        ActualizarAsignacion();
        OnCantidadCambiada?.Invoke();
    }

    partial void OnOpcionLoteSeleccionadaChanged(OpcionLote? value)
    {
        if (_medicamento is null) return;
        // Al elegir un lote concreto, la cantidad no puede superar lo que ese lote tiene.
        if (Cantidad > Disponible && Disponible > 0) Cantidad = Disponible;
        ActualizarAsignacion();
        OnSeleccionLoteCambiada?.Invoke();
    }

    private void ReconstruirOpcionesLote()
    {
        int? previo = OpcionLoteSeleccionada?.LoteId;

        OpcionesLote.Clear();
        OpcionesLote.Add(new OpcionLote { Texto = "Automático · sale primero el que vence antes" });
        foreach (var lote in _medicamento.LotesActivos)
        {
            string fecha = lote.FechaVencimiento?.ToString("dd/MM/yyyy") ?? "sin fecha";
            string estado = lote.Vencido ? " · VENCIDO" : string.Empty;
            OpcionesLote.Add(new OpcionLote
            {
                LoteId = lote.Id,
                Lote = lote,
                Texto = $"Lote {lote.NumeroLote} · vence {fecha} · {lote.Existencias} u{estado}"
            });
        }

        MostrarSelectorLote = _medicamento.NumeroLotes > 1;
        OpcionLoteSeleccionada = (previo != null ? OpcionesLote.FirstOrDefault(o => o.LoteId == previo) : null)
                                 ?? OpcionesLote[0];
    }

    /// <summary>Recalcula el reparto por lotes con las existencias conocidas del producto.</summary>
    public void ActualizarAsignacion()
    {
        if (_medicamento is null) return;

        bool manual = OpcionLoteSeleccionada?.Lote is not null;
        IEnumerable<Lote> lotes = manual ? new[] { OpcionLoteSeleccionada!.Lote! } : _medicamento.Lotes;

        var reparto = AsignadorLotes.Asignar(lotes, Cantidad, DateTime.Today);
        DetalleLotes = reparto.Asignaciones.Count == 0
            ? string.Empty
            : manual ? $"Lote elegido: {reparto.Descripcion}" : $"Sale de {reparto.Descripcion}";
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
            if (existente.Cantidad < existente.Disponible)
            {
                existente.Cantidad++;
            }
            else
            {
                existente.ActualizarAsignacion();
                MostrarAlerta(existente.OpcionLoteSeleccionada?.Lote is { } loteElegido
                    ? $"El lote {loteElegido.NumeroLote} de “{producto.Nombre}” no tiene más unidades; elige otro lote o el modo automático."
                    : $"No hay más existencias de “{producto.Nombre}”.");
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

        var lineas = Carritos.Select(i => new LineaDespacho(i.Medicamento.Id, i.Cantidad, i.OpcionLoteSeleccionada?.LoteId));
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
            if (item.Cantidad < item.Disponible)
            {
                item.Cantidad++;
                Recalcular();
            }
            else
            {
                MostrarAlerta(item.OpcionLoteSeleccionada?.Lote is { } loteElegido
                    ? $"El lote {loteElegido.NumeroLote} de “{item.Medicamento.Nombre}” no tiene más unidades; elige otro lote o el modo automático."
                    : $"No hay más existencias de “{item.Medicamento.Nombre}”.");
            }
        };
        item.OnSeleccionLoteCambiada = Recalcular;
        item.OnCantidadCambiada = Recalcular;
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
