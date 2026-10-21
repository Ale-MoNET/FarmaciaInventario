using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FarmaGestion.Data.Repositories;
using FarmaGestion.Models;
using FarmaGestion.Services;

namespace FarmaGestion.ViewModels;

public partial class ProductoViewModel : ObservableObject
{
    private readonly IMedicamentoRepository _medicamentoRepo;

    [ObservableProperty] private string _codigoBarras = string.Empty;
    [ObservableProperty] private string _nombre = string.Empty;
    [ObservableProperty] private string _precio = "0";
    [ObservableProperty] private string _existencias = "0";
    [ObservableProperty] private string? _lote;
    [ObservableProperty] private DateTime? _fechaVencimiento = DateTime.Today;

    [ObservableProperty] private string _mensajeError = string.Empty;
    [ObservableProperty] private bool _mostrarError;

    [ObservableProperty] private bool _esEdicion;
    [ObservableProperty] private string _tituloVentana = "Registrar medicamento";
    [ObservableProperty] private string _textoBoton = "Guardar medicamento";

    private int _medicamentoId;

    public Action? OnGuardadoExitoso;

    public ProductoViewModel(IMedicamentoRepository medicamentoRepo)
    {
        _medicamentoRepo = medicamentoRepo;
    }

    /// <summary>Carga un medicamento existente y pone el formulario en modo edición.</summary>
    public async Task CargarParaEdicionAsync(int id)
    {
        var m = await _medicamentoRepo.ObtenerPorIdAsync(id);
        if (m == null) return;

        _medicamentoId = m.Id;
        CodigoBarras = m.CodigoBarras;
        Nombre = m.Nombre;
        Precio = m.Precio.ToString(System.Globalization.CultureInfo.InvariantCulture);
        Existencias = m.Existencias.ToString();
        Lote = m.Lote;
        FechaVencimiento = m.FechaVencimiento ?? DateTime.Today;

        EsEdicion = true;
        TituloVentana = "Editar medicamento";
        TextoBoton = "Guardar cambios";
    }

    /// <summary>Pre-rellena el código de barras para crear un nuevo medicamento desde escaneo.</summary>
    public void InicializarConCodigoEscaneado(string codigoBarras)
    {
        CodigoBarras = codigoBarras.Trim();
        Nombre = string.Empty;
        Precio = "0";
        Existencias = "0";
        Lote = null;
        FechaVencimiento = DateTime.Today;

        EsEdicion = false;
        TituloVentana = $"Crear medicamento - Código: {codigoBarras}";
        TextoBoton = "Crear medicamento";
    }

    [RelayCommand]
    private async Task GuardarAsync()
    {
        MostrarError = false;
        MensajeError = string.Empty;

        // 1. Validación de campos obligatorios
        if (string.IsNullOrWhiteSpace(CodigoBarras))
        {
            MostrarErrorValidacion("⚠️ El código de barras es obligatorio.");
            return;
        }
        if (string.IsNullOrWhiteSpace(Nombre))
        {
            MostrarErrorValidacion("⚠️ El nombre del medicamento es obligatorio.");
            return;
        }

        // 2. Validación numérica
        if (!decimal.TryParse(Precio, out var precio) || precio < 0)
        {
            MostrarErrorValidacion("⚠️ El precio debe ser un número válido y no negativo.");
            return;
        }
        if (!int.TryParse(Existencias, out var existencias) || existencias < 0)
        {
            MostrarErrorValidacion("⚠️ Las existencias deben ser un número entero no negativo.");
            return;
        }

        var codigo = CodigoBarras.Trim();

        try
        {
            // 3. Validación de unicidad del código (excluyendo el propio en edición)
            if (await _medicamentoRepo.ExisteCodigoAsync(codigo, EsEdicion ? _medicamentoId : null))
            {
                MostrarErrorValidacion($"❌ Ya existe un medicamento con el código '{codigo}'.");
                return;
            }

            var medicamento = new Medicamentos
            {
                Id = _medicamentoId,
                CodigoBarras = codigo,
                Nombre = Nombre.Trim(),
                Precio = precio,
                Existencias = existencias,
                FechaUltimaActualizacion = DateTime.Now,
                Lote = string.IsNullOrWhiteSpace(Lote) ? null : Lote.Trim(),
                FechaVencimiento = FechaVencimiento
            };

            if (EsEdicion)
            {
                await _medicamentoRepo.ActualizarAsync(medicamento);
                LoggerService.Info($"Medicamento actualizado: {codigo} - {Nombre.Trim()}");
            }
            else
            {
                await _medicamentoRepo.AgregarAsync(medicamento);
                LoggerService.Info($"Medicamento registrado: {codigo} - {Nombre.Trim()}");
            }
        }
        catch (Exception ex)
        {
            LoggerService.Error("Error al guardar medicamento", ex);
            MostrarErrorValidacion("❌ No se pudo guardar el medicamento. Inténtalo de nuevo.");
            return;
        }

        OnGuardadoExitoso?.Invoke();
    }

    private void MostrarErrorValidacion(string mensaje)
    {
        MensajeError = mensaje;
        MostrarError = true;
    }
}
