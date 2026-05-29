using System;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FarmaGestion.Data;
using Microsoft.EntityFrameworkCore;

namespace FarmaGestion.ViewModels;

public class DespachoReporteItem
{
    public int Id { get; set; }
    public DateTime Fecha { get; set; }
    public decimal Total { get; set; }
}

public partial class ReportesViewModel : ObservableObject
{
    [ObservableProperty] private DateTime? _fechaInicio = DateTime.Today.AddDays(-7);
    [ObservableProperty] private DateTime? _fechaFin = DateTime.Today;
    
    [ObservableProperty] private string _productoMasVendido = "Calculando...";
    [ObservableProperty] private string _productoMenosVendido = "Calculando...";
    [ObservableProperty] private decimal _totalVentasRango = 0;
    
    public ObservableCollection<DespachoReporteItem> DespachosRango { get; } = new();

    public ReportesViewModel()
    {
        GenerarReporte();
    }

    [RelayCommand]
    public void GenerarReporte()
    {
        using var context = new InventarioContext();
        
        var rankingVentas = context.Medicamentos
            .Select(m => new 
            { 
                Nombre = m.Nombre,
                CantidadTotal = context.DetallesDespacho
                    .Where(d => d.MedicamentoId == m.Id)
                    .Sum(d => (int?)d.Cantidad) ?? 0 
            })
            .ToList();
        
        if(rankingVentas.Any())
        {
            var maxVentas = rankingVentas.Max(x => x.CantidadTotal);
            var minVentas = rankingVentas.Min(x => x.CantidadTotal);
            
            var masVendidos = rankingVentas
                .Where(x => x.CantidadTotal == maxVentas)
                .Select(x => x.Nombre)
                .ToList();
            var menosVendidos = rankingVentas
                .Where(x => x.CantidadTotal == minVentas)
                .Select(x => x.Nombre)
                .ToList();
            
            string textoMasVendidos = masVendidos.Count > 3 
                ? $"{string.Join(", ", masVendidos.Take(3))} y {masVendidos.Count - 3} más" 
                : string.Join(", ", masVendidos);
            
            string textoMenosVendidos = menosVendidos.Count > 3 
                ? $"{string.Join(", ", menosVendidos.Take(3))} y {menosVendidos.Count - 3} más" 
                : string.Join(", ", menosVendidos);
            
            
            ProductoMasVendido = $"{textoMasVendidos} ({maxVentas} unds)";
            ProductoMenosVendido = $"{textoMenosVendidos} ({minVentas} unds)";
        }
        else
        {
            ProductoMasVendido = "Sin ventas registradas";
            ProductoMenosVendido = "Sin ventas registradas";
        }
        
        var inicio = FechaInicio.HasValue ? FechaInicio.Value.Date : DateTime.MinValue;
        var fin = FechaFin.HasValue ? FechaFin.Value.Date.AddDays(1).AddTicks(-1) : DateTime.MaxValue;
        
        var despachosFiltrados = context.Despachos
            .Where(d => d.Fecha >= inicio && d.Fecha <= fin)    
            .OrderByDescending(d => d.Fecha)
            .ToList();
        
        DespachosRango.Clear();
        decimal acumuladorSuma = 0;
        
        foreach (var despacho in despachosFiltrados)
        {
            DespachosRango.Add(new DespachoReporteItem 
            { 
                Id = despacho.Id, 
                Fecha = despacho.Fecha, 
                Total = despacho.Total 
            });
            acumuladorSuma += despacho.Total;
        }
        
        TotalVentasRango = acumuladorSuma;
    }
}