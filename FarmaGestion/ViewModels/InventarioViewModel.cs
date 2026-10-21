using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using FarmaGestion.Data.Repositories;
using FarmaGestion.Models;
using FarmaGestion.Services;

namespace FarmaGestion.ViewModels;

public partial class InventarioViewModel : ObservableObject
{
    private readonly IMedicamentoRepository _medicamentoRepo;

    [ObservableProperty]
    private ObservableCollection<Medicamentos> _medicamentos;

    // Estadísticas para las tarjetas del panel (se recalculan al cargar el inventario).
    [ObservableProperty] private int _totalProductos;
    [ObservableProperty] private int _conteoBajoStock;
    [ObservableProperty] private int _conteoPorVencer;
    [ObservableProperty] private decimal _valorInventario;

    // Búsqueda por código de barras
    [ObservableProperty] private string _terminoBusqueda = string.Empty;
    private List<Medicamentos> _medicamentosOriginales = new();

    private readonly IBarcodeService _barcodeService;

    // Umbrales de negocio (coinciden con el diseño).
    private const int UmbralBajoStock = 15;
    private const int DiasPorVencer = 90;

    public InventarioViewModel(IMedicamentoRepository medicamentoRepo, IBarcodeService barcodeService)
    {
        _medicamentoRepo = medicamentoRepo;
        _barcodeService = barcodeService;
        Medicamentos = new ObservableCollection<Medicamentos>();

        // Carga inicial (sin bloquear el constructor).
        _ = CargarInventarioAsync();

        // Suscribirse a cambios en el término de búsqueda
        PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(TerminoBusqueda))
            {
                AplicarFiltro(TerminoBusqueda);
            }
        };

        WeakReferenceMessenger.Default.Register<InventarioActualizadoMessage>(this, (recipient, message) =>
        {
            Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(async () => await CargarInventarioAsync());
        });
    }

    public async Task CargarInventarioAsync()
    {
        // Las migraciones se aplican una sola vez al arrancar la app (App.axaml.cs).
        try
        {
            var lista = await _medicamentoRepo.ObtenerTodosAsync();
            _medicamentosOriginales = lista;

            Medicamentos.Clear();
            foreach (var med in lista)
                Medicamentos.Add(med);

            // Resetear búsqueda
            TerminoBusqueda = string.Empty;

            RecalcularEstadisticas(lista);
        }
        catch (Exception ex)
        {
            LoggerService.Error("Error al cargar el inventario", ex);
        }
    }

    private void RecalcularEstadisticas(System.Collections.Generic.List<Medicamentos> lista)
    {
        var limitePorVencer = DateTime.Now.AddDays(DiasPorVencer);
        TotalProductos = lista.Count;
        ConteoBajoStock = lista.Count(m => m.Existencias < UmbralBajoStock);
        ConteoPorVencer = lista.Count(m => m.FechaVencimiento != null && m.FechaVencimiento <= limitePorVencer);
        ValorInventario = lista.Sum(m => m.Precio * m.Existencias);
    }

    /// <summary>Elimina un producto y recarga. Devuelve false si tiene despachos asociados.</summary>
    public async Task<bool> EliminarAsync(int id)
    {
        try
        {
            bool eliminado = await _medicamentoRepo.EliminarAsync(id);
            if (eliminado)
            {
                LoggerService.Info($"Medicamento eliminado (Id={id}).");
                await CargarInventarioAsync();
            }
            return eliminado;
        }
        catch (Exception ex)
        {
            LoggerService.Error("Error al eliminar medicamento", ex);
            return false;
        }
    }

    public async Task ImportarCsvAsync(string rutaArchivo)
    {
        var registros = new List<RegistroMedicamentoCsv>();

        try
        {
            var lineas = await File.ReadAllLinesAsync(rutaArchivo);

            // Skip(1) ignora la fila de encabezados: Codigo,Nombre,Precio,Existencias,Lote,Vencimiento
            foreach (var linea in lineas.Skip(1))
            {
                var columnas = linea.Split(',');
                if (columnas.Length < 4) continue;

                string codigo = columnas[0].Trim();
                if (string.IsNullOrWhiteSpace(codigo)) continue;

                string nombre = columnas[1].Trim();
                decimal precio = decimal.TryParse(columnas[2], out var p) ? p : 0;
                int cantidad = int.TryParse(columnas[3], out var c) ? c : 0;
                string? lote = columnas.Length > 4 ? columnas[4].Trim() : null;
                DateTime? fechaVenc = null;
                if (columnas.Length > 5 && DateTime.TryParse(columnas[5].Trim(), out var fv))
                    fechaVenc = fv;

                registros.Add(new RegistroMedicamentoCsv(codigo, nombre, precio, cantidad, lote, fechaVenc));
            }

            await _medicamentoRepo.ImportarRegistrosAsync(registros);
            LoggerService.Info($"Importación de CSV completada ({registros.Count} filas) desde '{rutaArchivo}'.");
        }
        catch (Exception ex)
        {
            LoggerService.Error("Error al importar el CSV", ex);
            throw new InvalidOperationException(
                "No se pudo importar el archivo CSV. Revisa el formato e inténtalo de nuevo.", ex);
        }
    }

    private void AplicarFiltro(string termino)
    {
        if (string.IsNullOrWhiteSpace(termino))
        {
            Medicamentos.Clear();
            foreach (var med in _medicamentosOriginales)
                Medicamentos.Add(med);
        }
        else
        {
            var resultados = _barcodeService.BuscarPorTermino(
                new ObservableCollection<Medicamentos>(_medicamentosOriginales), termino);

            Medicamentos.Clear();
            foreach (var med in resultados)
                Medicamentos.Add(med);
        }

        RecalcularEstadisticas(Medicamentos.ToList());
    }

    /// <summary>
    /// Maneja cuando se completa un escaneo (usuario presiona Enter).
    /// Si el código existe, muestra el medicamento. Si no existe, abre la ventana de crear nuevo.
    /// </summary>
    public async Task ManejarEscaneoCompletoAsync(string codigoEscaneado)
    {
        if (string.IsNullOrWhiteSpace(codigoEscaneado))
            return;

        var codigo = codigoEscaneado.Trim();

        // Buscar si el código existe
        var medicamento = _medicamentosOriginales.FirstOrDefault(m =>
            m.CodigoBarras?.Equals(codigo, StringComparison.OrdinalIgnoreCase) == true);

        if (medicamento != null)
        {
            // El medicamento existe: mostrar solo este en la tabla
            Medicamentos.Clear();
            Medicamentos.Add(medicamento);
            RecalcularEstadisticas(new List<Medicamentos> { medicamento });
            LoggerService.Info($"Medicamento encontrado: {medicamento.Nombre}");
        }
        else
        {
            // El medicamento NO existe: abrir ventana para crear uno nuevo
            LoggerService.Info($"Código no encontrado: {codigo}. Abriendo ventana de creación.");
            await AbrirVentanaCrearMedicamentoAsync(codigo);
        }
    }

    private async Task AbrirVentanaCrearMedicamentoAsync(string codigoEscaneado)
    {
        // Esta función será llamada desde la Vista (InventarioView.axaml.cs)
        // porque la Vista tiene acceso a la ventana principal
    }
}
