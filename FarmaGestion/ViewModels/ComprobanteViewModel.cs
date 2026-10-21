using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using FarmaGestion.Data.Repositories;
using FarmaGestion.Services;

namespace FarmaGestion.ViewModels;

/// <summary>Fila del detalle mostrado en el comprobante.</summary>
public class LineaComprobanteItem
{
    public string Codigo { get; init; } = string.Empty;
    public string Nombre { get; init; } = string.Empty;
    public string CantidadTexto { get; init; } = string.Empty;
    public string PrecioTexto { get; init; } = string.Empty;
    public string SubtotalTexto { get; init; } = string.Empty;
    public string LoteTexto { get; init; } = string.Empty;
    public bool TieneLote => LoteTexto.Length > 0;
}

/// <summary>
/// ViewModel del comprobante que se muestra al confirmar un despacho. Expone los
/// datos ya formateados para la ventana y produce el PDF que se descarga o imprime.
/// </summary>
public partial class ComprobanteViewModel : ObservableObject
{
    private readonly ComprobanteDespacho _comprobante;

    [ObservableProperty] private string _estado = string.Empty;
    [ObservableProperty] private bool _hayEstado;
    [ObservableProperty] private string _estadoColor = "#5CE8B4";

    public ComprobanteViewModel(ComprobanteDespacho comprobante)
    {
        _comprobante = comprobante;

        foreach (var linea in comprobante.Lineas)
        {
            Lineas.Add(new LineaComprobanteItem
            {
                Codigo = linea.Codigo,
                Nombre = linea.Nombre,
                CantidadTexto = linea.Cantidad.ToString(),
                PrecioTexto = linea.PrecioUnitario.ToString("C"),
                SubtotalTexto = linea.Subtotal.ToString("C"),
                LoteTexto = string.IsNullOrWhiteSpace(linea.Lote) ? string.Empty : $"Lote {linea.Lote}"
            });
        }
    }

    public ObservableCollection<LineaComprobanteItem> Lineas { get; } = new();

    public string NumeroTexto => $"#{_comprobante.Id:D5}";
    public string FechaTexto => _comprobante.Fecha.ToString("dd/MM/yyyy · HH:mm");
    public string AtendioTexto => string.IsNullOrWhiteSpace(_comprobante.Atendio) ? "—" : _comprobante.Atendio;
    public string TotalTexto => _comprobante.Total.ToString("C");

    public string ResumenTexto =>
        $"{_comprobante.Unidades} artículo{(_comprobante.Unidades == 1 ? "" : "s")} · " +
        $"{Lineas.Count} línea{(Lineas.Count == 1 ? "" : "s")}";

    /// <summary>Nombre sugerido al guardar el PDF.</summary>
    public string NombreArchivo => ComprobantePdf.NombreArchivo(_comprobante);

    /// <summary>Construye el PDF del comprobante.</summary>
    public byte[] GenerarPdf() => ComprobantePdf.Generar(_comprobante);

    /// <summary>Muestra un mensaje de resultado bajo los botones de la ventana.</summary>
    public void MostrarEstado(string mensaje, bool esError = false)
    {
        Estado = mensaje;
        HayEstado = !string.IsNullOrWhiteSpace(mensaje);
        EstadoColor = esError ? "#FF8B8B" : "#5CE8B4";
    }
}
