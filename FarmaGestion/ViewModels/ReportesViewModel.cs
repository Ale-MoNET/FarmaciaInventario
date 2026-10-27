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

/// <summary>Fila de la tabla de despachos del reporte.</summary>
public class DespachoReporteItem
{
    public int Id { get; set; }
    public string IdTexto { get; set; } = string.Empty;
    public string FechaTexto { get; set; } = string.Empty;
    public int Unidades { get; set; }
    public string TotalTexto { get; set; } = string.Empty;

    /// <summary>Resumen corto de productos, p. ej. "Paracetamol ×2 · Ibuprofeno ×1 · +2 más".</summary>
    public string ProductosTexto { get; set; } = string.Empty;

    /// <summary>True si el despacho fue deshecho: se muestra atenuado y sin botón.</summary>
    public bool Anulado { get; set; }
    public bool PuedeDeshacer => !Anulado;

    /// <summary>Arma el resumen de productos mostrando como máximo <paramref name="maximo"/> nombres.</summary>
    public static string ResumirProductos(IReadOnlyList<ProductoDespacho> productos, int maximo = 3)
    {
        if (productos.Count == 0) return "Sin detalle";
        var partes = productos.Take(maximo).Select(p => $"{p.Nombre} ×{p.Cantidad}").ToList();
        int resto = productos.Count - maximo;
        if (resto > 0) partes.Add($"+{resto} más");
        return string.Join(" · ", partes);
    }
}

/// <summary>Barra del gráfico "ventas por día".</summary>
public class BarraDia
{
    public string Etiqueta { get; set; } = string.Empty;
    public string ValorTexto { get; set; } = string.Empty;
    public double Altura { get; set; }
    public string Color { get; set; } = "#6634E0A1";
}

/// <summary>Fila del reporte de vencimientos (un lote con existencias).</summary>
public class LoteVencimientoItem
{
    public Lote Lote { get; init; } = null!;
    public string Producto { get; init; } = string.Empty;
    public string Codigo { get; init; } = string.Empty;
    public string NumeroLote { get; init; } = string.Empty;
    public string VenceTexto { get; init; } = string.Empty;
    public string DiasTexto { get; init; } = string.Empty;
    public int Existencias { get; init; }
    public EstadoLote Estado { get; init; }
    public string EstadoTexto { get; init; } = string.Empty;
}

/// <summary>Fila de la lista de últimas bajas.</summary>
public class BajaItem
{
    public string FechaTexto { get; init; } = string.Empty;
    public string Producto { get; init; } = string.Empty;
    public string NumeroLote { get; init; } = string.Empty;
    public int Cantidad { get; init; }
    public string Motivo { get; init; } = string.Empty;
    public string Usuario { get; init; } = string.Empty;
}

public partial class ReportesViewModel : ObservableObject
{
    private readonly IDespachoRepository _despachoRepo;
    private readonly IMedicamentoRepository _medicamentoRepo;
    private const double AlturaMaxBarra = 150;

    [ObservableProperty] private DateTime? _fechaInicio = DateTime.Today.AddDays(-7);
    [ObservableProperty] private DateTime? _fechaFin = DateTime.Today;

    // Tarjetas top / bottom
    [ObservableProperty] private string _productoMasVendido = "Calculando…";
    [ObservableProperty] private string _productoMasVendidoSub = "En el periodo seleccionado";
    [ObservableProperty] private string _productoMenosVendido = "Calculando…";
    [ObservableProperty] private string _productoMenosVendidoSub = "En el periodo seleccionado";

    // KPIs
    [ObservableProperty] private string _kVentas = "—";
    [ObservableProperty] private string _kDespachos = "0";
    [ObservableProperty] private string _kTicket = "—";
    [ObservableProperty] private string _kUnidades = "0";

    [ObservableProperty] private bool _sinDespachos = true;

    public ObservableCollection<DespachoReporteItem> DespachosRango { get; } = new();
    public ObservableCollection<BarraDia> Barras { get; } = new();

    // ----- Vencimientos de lotes -----
    private List<Lote> _lotesConStock = new();

    /// <summary>Horizonte activo como texto: "0" vencidos, "30"/"60"/"90" días, "-1" todos.</summary>
    [ObservableProperty] private string _horizonte = "90";

    [ObservableProperty] private string _cVencidos = "0";
    [ObservableProperty] private string _c30 = "0";
    [ObservableProperty] private string _c60 = "0";
    [ObservableProperty] private string _c90 = "0";
    [ObservableProperty] private string _tituloLotes = string.Empty;
    [ObservableProperty] private bool _sinLotes = true;
    [ObservableProperty] private bool _sinBajas = true;

    public ObservableCollection<LoteVencimientoItem> LotesVencimiento { get; } = new();
    public ObservableCollection<BajaItem> BajasRecientes { get; } = new();

    public ReportesViewModel(IDespachoRepository despachoRepo, IMedicamentoRepository medicamentoRepo)
    {
        _despachoRepo = despachoRepo;
        _medicamentoRepo = medicamentoRepo;
        _ = GenerarReporteAsync();
        _ = CargarVencimientosAsync();

        // Cualquier ingreso, despacho, baja o anulación refresca ambos reportes.
        WeakReferenceMessenger.Default.Register<InventarioActualizadoMessage>(this, (_, _) =>
        {
            Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(async () =>
            {
                await CargarVencimientosAsync();
                await GenerarReporteAsync();
            });
        });
    }

    // Reactivo: al cambiar cualquiera de las fechas se regenera el reporte.
    partial void OnFechaInicioChanged(DateTime? value) => _ = GenerarReporteAsync();
    partial void OnFechaFinChanged(DateTime? value) => _ = GenerarReporteAsync();
    partial void OnHorizonteChanged(string value) => AplicarFiltroVencimientos();

    [RelayCommand]
    private void SeleccionarHorizonte(string horizonte) => Horizonte = horizonte;

    [RelayCommand]
    public async Task CargarVencimientosAsync()
    {
        try
        {
            _lotesConStock = await _medicamentoRepo.ObtenerLotesConExistenciasAsync();

            var (vencidos, en30, en60, en90) = ReporteVencimientos.Contar(_lotesConStock, DateTime.Today);
            CVencidos = vencidos.ToString();
            C30 = en30.ToString();
            C60 = en60.ToString();
            C90 = en90.ToString();
            AplicarFiltroVencimientos();

            var bajas = await _medicamentoRepo.ObtenerBajasRecientesAsync(30);
            BajasRecientes.Clear();
            foreach (var b in bajas)
            {
                BajasRecientes.Add(new BajaItem
                {
                    FechaTexto = b.Fecha.ToString("dd/MM/yyyy · HH:mm"),
                    Producto = b.Medicamento?.Nombre ?? "—",
                    NumeroLote = b.Lote?.NumeroLote ?? "—",
                    Cantidad = b.Cantidad,
                    Motivo = string.IsNullOrWhiteSpace(b.Observacion) ? b.Motivo : $"{b.Motivo} · {b.Observacion}",
                    Usuario = string.IsNullOrWhiteSpace(b.Usuario) ? "—" : b.Usuario
                });
            }
            SinBajas = BajasRecientes.Count == 0;
        }
        catch (Exception ex)
        {
            LoggerService.Error("Error al cargar el reporte de vencimientos", ex);
        }
    }

    private void AplicarFiltroVencimientos()
    {
        int horizonte = int.TryParse(Horizonte, out var h) ? h : 90;
        var hoy = DateTime.Today;

        LotesVencimiento.Clear();
        foreach (var lote in ReporteVencimientos.Filtrar(_lotesConStock, horizonte, hoy))
        {
            LotesVencimiento.Add(new LoteVencimientoItem
            {
                Lote = lote,
                Producto = lote.Medicamento?.Nombre ?? "—",
                Codigo = lote.Medicamento?.CodigoBarras ?? "—",
                NumeroLote = lote.NumeroLote,
                VenceTexto = lote.FechaVencimiento?.ToString("dd/MM/yyyy") ?? "—",
                DiasTexto = ReporteVencimientos.TextoDias(lote, hoy),
                Existencias = lote.Existencias,
                Estado = lote.CalcularEstado(hoy),
                EstadoTexto = lote.EstadoTexto
            });
        }

        SinLotes = LotesVencimiento.Count == 0;
        int n = LotesVencimiento.Count;
        string s = n == 1 ? "" : "s";
        TituloLotes = horizonte switch
        {
            ReporteVencimientos.Vencidos => $"{n} lote{s} vencido{s} con existencias",
            ReporteVencimientos.Todos => $"{n} lote{s} con existencias",
            _ => $"{n} lote{s} que {(n == 1 ? "vence" : "vencen")} en los próximos {horizonte} días"
        };
    }

    /// <summary>Datos completos de un despacho de la tabla, para el diálogo de deshacer.</summary>
    public Task<ComprobanteDespacho?> ObtenerComprobanteAsync(int despachoId)
        => _despachoRepo.ObtenerComprobanteAsync(despachoId);

    [RelayCommand]
    public async Task GenerarReporteAsync()
    {
        try
        {
            var inicio = FechaInicio?.Date ?? DateTime.MinValue;
            var fin = FechaFin?.Date.AddDays(1).AddTicks(-1) ?? DateTime.MaxValue;

            var r = await _despachoRepo.GenerarReporteAsync(inicio, fin);

            KVentas = r.VentasTotales.ToString("C");
            KDespachos = r.NumDespachos.ToString();
            KTicket = r.Ticket.ToString("C");
            KUnidades = r.Unidades.ToString();

            ProductoMasVendido = r.Top?.Nombre ?? "Sin ventas registradas";
            ProductoMasVendidoSub = r.Top != null
                ? $"{r.Top.Unidades} unidades · {r.Top.Ingreso:C}"
                : "En el periodo seleccionado";

            ProductoMenosVendido = r.Bottom?.Nombre ?? "Sin ventas registradas";
            ProductoMenosVendidoSub = r.Bottom != null
                ? $"{r.Bottom.Unidades} unidad{(r.Bottom.Unidades == 1 ? "" : "es")} · {r.Bottom.Ingreso:C}"
                : "En el periodo seleccionado";

            DespachosRango.Clear();
            foreach (var d in r.Despachos)
            {
                DespachosRango.Add(new DespachoReporteItem
                {
                    Id = d.Id,
                    IdTexto = $"#{d.Id}",
                    FechaTexto = d.Fecha.ToString("dd/MM/yyyy · HH:mm"),
                    Unidades = d.Unidades,
                    TotalTexto = d.Total.ToString("C"),
                    ProductosTexto = DespachoReporteItem.ResumirProductos(d.Productos),
                    Anulado = d.Anulado
                });
            }
            SinDespachos = r.Despachos.Count == 0;

            ConstruirBarras(r);
        }
        catch (Exception ex)
        {
            LoggerService.Error("Error al generar el reporte", ex);
            ProductoMasVendido = "Error al calcular";
            ProductoMenosVendido = "Error al calcular";
        }
    }

    private void ConstruirBarras(ReporteVentas r)
    {
        Barras.Clear();
        var maxVal = r.PorDia.Count > 0 ? r.PorDia.Max(d => d.Total) : 0m;
        if (maxVal <= 0m) maxVal = 1m;

        foreach (var dia in r.PorDia)
        {
            bool esMax = dia.Total == r.PorDia.Max(d => d.Total) && dia.Total > 0m;
            Barras.Add(new BarraDia
            {
                Etiqueta = dia.Dia.ToString("dd/MM"),
                ValorTexto = dia.Total > 0m ? dia.Total.ToString("C0") : string.Empty,
                Altura = (double)(dia.Total / maxVal) * AlturaMaxBarra,
                Color = esMax ? "#34E0A1" : "#6634E0A1"
            });
        }
    }
}
