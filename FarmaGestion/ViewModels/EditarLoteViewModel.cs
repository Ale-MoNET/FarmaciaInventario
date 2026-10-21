using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using FarmaGestion.Data.Repositories;
using FarmaGestion.Models;
using FarmaGestion.Services;

namespace FarmaGestion.ViewModels;

/// <summary>Ventana "Editar lote": corrige número de lote y fecha de vencimiento (no existencias).</summary>
public partial class EditarLoteViewModel : ObservableObject
{
    private readonly IMedicamentoRepository _medicamentoRepo;
    private Lote _lote = null!;

    [ObservableProperty] private string _encabezado = string.Empty;
    [ObservableProperty] private string _numeroLote = string.Empty;
    [ObservableProperty] private DateTime? _fechaVencimiento;

    [ObservableProperty] private string _mensajeError = string.Empty;
    [ObservableProperty] private bool _mostrarError;

    public Action? OnGuardado;

    public EditarLoteViewModel(IMedicamentoRepository medicamentoRepo)
    {
        _medicamentoRepo = medicamentoRepo;
    }

    public void Inicializar(Lote lote, string nombreProducto)
    {
        _lote = lote;
        Encabezado = $"{nombreProducto} · {lote.Existencias} u en existencia";
        NumeroLote = lote.NumeroLote;
        FechaVencimiento = lote.FechaVencimiento;
    }

    [RelayCommand]
    private async Task GuardarAsync()
    {
        MostrarError = false;
        MensajeError = string.Empty;

        if (string.IsNullOrWhiteSpace(NumeroLote))
        {
            Error("El número de lote es obligatorio.");
            return;
        }
        if (FechaVencimiento is null)
        {
            Error("Indica la fecha de vencimiento.");
            return;
        }

        try
        {
            var resultado = await _medicamentoRepo.ActualizarLoteAsync(_lote.Id, NumeroLote, FechaVencimiento.Value);
            if (!resultado.Exito)
            {
                Error(resultado.Mensaje);
                return;
            }

            WeakReferenceMessenger.Default.Send(new InventarioActualizadoMessage());
            OnGuardado?.Invoke();
        }
        catch (Exception ex)
        {
            LoggerService.Error("Error al actualizar el lote", ex);
            Error("No se pudo guardar el lote. Inténtalo de nuevo.");
        }
    }

    private void Error(string mensaje)
    {
        MensajeError = mensaje;
        MostrarError = true;
    }
}
