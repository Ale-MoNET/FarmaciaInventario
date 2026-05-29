using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FarmaGestion.Data;
using FarmaGestion.Models;

namespace FarmaGestion.ViewModels;

public partial class ProductoViewModel : ObservableObject
{
    [ObservableProperty] private string _codigoBarras = string.Empty;
    [ObservableProperty] private string _nombre = string.Empty;
    [ObservableProperty] private string _precio = "0"; 
    [ObservableProperty] private string _existencias = "0";

    public Action? OnGuardadoExitoso;

    [RelayCommand]
    private void Guardar()
    {
        var nuevoMedicamento = new Medicamentos()
        {
            CodigoBarras = CodigoBarras,
            Nombre = Nombre,
            Precio = decimal.TryParse(Precio, out var p) ? p : 0,
            Existencias = int.TryParse(Existencias, out var e) ? e : 0,
            FechaUltimaActualizacion = DateTime.Now
        };

        using var context = new InventarioContext();
        context.Medicamentos.Add(nuevoMedicamento);
        context.SaveChanges();
        
        OnGuardadoExitoso?.Invoke();
    }
}