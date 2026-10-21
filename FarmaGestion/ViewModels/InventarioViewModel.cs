using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using FarmaGestion.Data.Repositories;
using FarmaGestion.Models;
using FarmaGestion.Services;

namespace FarmaGestion.ViewModels;

/// <summary>Resultado de una importación CSV para mostrarlo al usuario.</summary>
public record ResumenImportacion(int Importadas, IReadOnlyList<string> Rechazadas)
{
    public bool HuboRechazos => Rechazadas.Count > 0;
}

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
    private const int DiasPorVencer = Lote.DiasPorVencer;

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

    private void RecalcularEstadisticas(List<Medicamentos> lista)
    {
        var limitePorVencer = DateTime.Today.AddDays(DiasPorVencer);
        TotalProductos = lista.Count;
        ConteoBajoStock = lista.Count(m => m.Existencias < UmbralBajoStock);
        // Se cuentan lotes con existencias que vencen en los próximos días (o ya vencieron).
        ConteoPorVencer = lista
            .SelectMany(m => m.Lotes)
            .Count(l => l.Existencias > 0 && l.FechaVencimiento != null && l.FechaVencimiento.Value.Date <= limitePorVencer);
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

    /// <summary>
    /// Importa un CSV con el formato Codigo,Nombre,Precio,Existencias,Lote,Vencimiento.
    /// Cada fila es un ingreso de lote; las filas incompletas se rechazan y se informan.
    /// </summary>
    public async Task<ResumenImportacion> ImportarCsvAsync(string rutaArchivo)
    {
        var registros = new List<RegistroMedicamentoCsv>();
        var rechazadas = new List<string>();

        try
        {
            var lineas = await File.ReadAllLinesAsync(rutaArchivo);

            // La fila 1 es el encabezado.
            for (int i = 1; i < lineas.Length; i++)
            {
                int fila = i + 1;
                var linea = lineas[i];
                if (string.IsNullOrWhiteSpace(linea)) continue;

                var col = linea.Split(',');
                if (col.Length < 6)
                {
                    rechazadas.Add($"Fila {fila}: faltan columnas (se esperan Codigo,Nombre,Precio,Existencias,Lote,Vencimiento).");
                    continue;
                }

                string codigo = col[0].Trim();
                if (codigo.Length == 0) { rechazadas.Add($"Fila {fila}: sin código de barras."); continue; }

                string nombre = col[1].Trim();
                if (nombre.Length == 0) { rechazadas.Add($"Fila {fila}: sin nombre."); continue; }

                if (!TryParsePrecio(col[2], out var precio) || precio < 0)
                { rechazadas.Add($"Fila {fila}: precio inválido «{col[2].Trim()}»."); continue; }

                if (!int.TryParse(col[3].Trim(), out var cantidad) || cantidad <= 0)
                { rechazadas.Add($"Fila {fila}: cantidad inválida «{col[3].Trim()}» (debe ser mayor que 0)."); continue; }

                string lote = col[4].Trim();
                if (lote.Length == 0) { rechazadas.Add($"Fila {fila}: sin lote (es obligatorio)."); continue; }

                if (!TryParseFecha(col[5], out var fecha))
                { rechazadas.Add($"Fila {fila}: fecha de vencimiento inválida «{col[5].Trim()}» (usa AAAA-MM-DD)."); continue; }

                registros.Add(new RegistroMedicamentoCsv(codigo, nombre, precio, cantidad, lote, fecha, fila));
            }

            var resultado = await _medicamentoRepo.ImportarRegistrosAsync(registros);
            rechazadas.AddRange(resultado.Rechazadas);

            LoggerService.Info($"Importación de CSV completada: {resultado.Procesadas} filas aplicadas, " +
                               $"{rechazadas.Count} rechazadas, desde '{rutaArchivo}'.");
            return new ResumenImportacion(resultado.Procesadas, rechazadas);
        }
        catch (Exception ex)
        {
            LoggerService.Error("Error al importar el CSV", ex);
            throw new InvalidOperationException(
                "No se pudo importar el archivo CSV. Revisa el formato e inténtalo de nuevo.", ex);
        }
    }

    private static bool TryParsePrecio(string texto, out decimal valor)
    {
        var s = texto.Trim();
        // Acepta "15.50" y "15,50"; no acepta separadores de miles.
        if (s.Contains(',') && !s.Contains('.')) s = s.Replace(',', '.');
        return decimal.TryParse(s, NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign,
            CultureInfo.InvariantCulture, out valor);
    }

    private static readonly string[] FormatosFecha =
        { "yyyy-MM-dd", "dd/MM/yyyy", "d/M/yyyy", "dd-MM-yyyy", "yyyy/MM/dd" };

    private static bool TryParseFecha(string texto, out DateTime fecha)
    {
        var s = texto.Trim();
        return DateTime.TryParseExact(s, FormatosFecha, CultureInfo.InvariantCulture, DateTimeStyles.None, out fecha)
               || DateTime.TryParse(s, CultureInfo.CurrentCulture, DateTimeStyles.None, out fecha);
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
