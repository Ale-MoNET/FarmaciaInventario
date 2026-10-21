using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using FarmaGestion.Data.Repositories;
using FarmaGestion.Models;
using FarmaGestion.Services;

namespace FarmaGestion.ViewModels;

/// <summary>Ventana "Dar de baja": retira unidades de un lote dejando registro del motivo.</summary>
public partial class BajaLoteViewModel : ObservableObject
{
    private readonly IMedicamentoRepository _medicamentoRepo;
    private Lote _lote = null!;

    [ObservableProperty] private string _encabezado = string.Empty;
    [ObservableProperty] private string _detalle = string.Empty;
    [ObservableProperty] private string _cantidad = string.Empty;
    [ObservableProperty] private string? _motivo;
    [ObservableProperty] private string _observacion = string.Empty;

    [ObservableProperty] private string _mensajeError = string.Empty;
    [ObservableProperty] private bool _mostrarError;

    public string[] Motivos => MotivosBaja.Todos;

    public Action? OnGuardado;

    public BajaLoteViewModel(IMedicamentoRepository medicamentoRepo)
    {
        _medicamentoRepo = medicamentoRepo;
    }

    public void Inicializar(Lote lote, string nombreProducto)
    {
        _lote = lote;
        Encabezado = $"{nombreProducto} · Lote {lote.NumeroLote}";
        string vence = lote.FechaVencimiento?.ToString("dd/MM/yyyy") ?? "sin fecha";
        Detalle = $"{lote.Existencias} u en existencia · vence {vence} · {lote.EstadoTexto}";
        Cantidad = lote.Existencias.ToString();
        Motivo = lote.Vencido ? MotivosBaja.Vencido : null;
    }

    [RelayCommand]
    private async Task RegistrarAsync()
    {
        MostrarError = false;
        MensajeError = string.Empty;

        if (!int.TryParse(Cantidad.Trim(), out var cantidad) || cantidad <= 0)
        {
            Error("La cantidad a retirar debe ser un número entero mayor que cero.");
            return;
        }
        if (cantidad > _lote.Existencias)
        {
            Error($"El lote solo tiene {_lote.Existencias} u.");
            return;
        }
        if (string.IsNullOrWhiteSpace(Motivo))
        {
            Error("Elige el motivo de la baja.");
            return;
        }

        try
        {
            var usuario = SesionActual.Usuario?.NombreUsuario ?? string.Empty;
            var resultado = await _medicamentoRepo.DarDeBajaLoteAsync(_lote.Id, cantidad, Motivo!, Observacion, usuario);
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
            LoggerService.Error("Error al registrar la baja del lote", ex);
            Error("No se pudo registrar la baja. Inténtalo de nuevo.");
        }
    }

    private void Error(string mensaje)
    {
        MensajeError = mensaje;
        MostrarError = true;
    }
}
