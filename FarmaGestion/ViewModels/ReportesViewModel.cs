using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FarmaGestion.Data.Repositories;
using FarmaGestion.Services;

namespace FarmaGestion.ViewModels;

/// <summary>Fila de la tabla de despachos del reporte.</summary>
public class DespachoReporteItem
{
    public string IdTexto { get; set; } = string.Empty;
    public string FechaTexto { get; set; } = string.Empty;
    public int Unidades { get; set; }
    public string TotalTexto { get; set; } = string.Empty;
}

/// <summary>Barra del gráfico "ventas por día".</summary>
public class BarraDia
{
    public string Etiqueta { get; set; } = string.Empty;
    public string ValorTexto { get; set; } = string.Empty;
    public double Altura { get; set; }
    public string Color { get; set; } = "#6634E0A1";
}

public partial class ReportesViewModel : ObservableObject
{
    private readonly IDespachoRepository _despachoRepo;
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

    public ReportesViewModel(IDespachoRepository despachoRepo)
    {
        _despachoRepo = despachoRepo;
        _ = GenerarReporteAsync();
    }

    // Reactivo: al cambiar cualquiera de las fechas se regenera el reporte.
    partial void OnFechaInicioChanged(DateTime? value) => _ = GenerarReporteAsync();
    partial void OnFechaFinChanged(DateTime? value) => _ = GenerarReporteAsync();

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
                    IdTexto = $"#{d.Id}",
                    FechaTexto = d.Fecha.ToString("dd/MM/yyyy · HH:mm"),
                    Unidades = d.Unidades,
                    TotalTexto = d.Total.ToString("C")
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
