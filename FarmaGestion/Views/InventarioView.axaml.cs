using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;
using FarmaGestion.Models;
using FarmaGestion.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace FarmaGestion.Views;

public partial class InventarioView : UserControl
{
    public InventarioView()
    {
        InitializeComponent();
        DataContext = App.Services.GetRequiredService<InventarioViewModel>();

        // Cuando se carga la vista, el foco va al campo de búsqueda
        this.Loaded += (s, e) => TxtBusqueda.Focus();
    }

    private Window? Propietaria => TopLevel.GetTopLevel(this) as Window;

    private async void BtnRegistrar_Click(object? sender, RoutedEventArgs e)
    {
        var ventanaRegistro = new ProductoWindow();

        if (Propietaria is { } mainWindow)
            await ventanaRegistro.ShowDialog(mainWindow);

        if (DataContext is InventarioViewModel vm)
            await vm.CargarInventarioAsync();
    }

    private async void BtnIngresar_Click(object? sender, RoutedEventArgs e)
    {
        await AbrirIngresoAsync(null);
    }

    private async void BtnIngresarProducto_Click(object? sender, RoutedEventArgs e)
    {
        // El botón vive en la fila; su DataContext es el Medicamentos de esa fila.
        if (sender is not Control { DataContext: Medicamentos med }) return;
        await AbrirIngresoAsync(med.CodigoBarras);
    }

    private async System.Threading.Tasks.Task AbrirIngresoAsync(string? codigo)
    {
        var ventana = new IngresoWindow(codigo);

        if (Propietaria is { } mainWindow)
            await ventana.ShowDialog(mainWindow);

        if (DataContext is InventarioViewModel vm)
            await vm.CargarInventarioAsync();
    }

    private async void BtnEditarProducto_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not Control { DataContext: Medicamentos med }) return;

        var ventana = new ProductoWindow(med.Id);
        if (Propietaria is { } mainWindow)
            await ventana.ShowDialog(mainWindow);

        if (DataContext is InventarioViewModel vm)
            await vm.CargarInventarioAsync();
    }

    private async void BtnEliminarProducto_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not Control { DataContext: Medicamentos med }) return;
        if (Propietaria is not { } owner) return;
        if (DataContext is not InventarioViewModel vm) return;

        // 1. Confirmación
        var confirm = new ConfirmWindow(
            "Eliminar medicamento",
            $"¿Seguro que deseas eliminar «{med.Nombre}» ({med.CodigoBarras}) con todos sus lotes? Esta acción no se puede deshacer.",
            "Eliminar");
        var confirmado = await confirm.ShowDialog<bool>(owner);
        if (!confirmado) return;

        // 2. Eliminar (o avisar si tiene despachos)
        bool eliminado = await vm.EliminarAsync(med.Id);
        if (!eliminado)
        {
            var aviso = new ConfirmWindow(
                "No se puede eliminar",
                $"«{med.Nombre}» tiene despachos asociados, por lo que no se puede eliminar (se perdería el historial de ventas). Puedes dar de baja sus lotes para dejarlo sin existencias.",
                "Entendido", peligro: false, mostrarCancelar: false);
            await aviso.ShowDialog<bool>(owner);
        }
    }

    private async void BtnDarDeBajaLote_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not Control { DataContext: Lote lote } control) return;
        if (Propietaria is not { } owner) return;

        var ventana = new BajaLoteWindow(lote, NombreProductoDeFila(control, lote));
        await ventana.ShowDialog(owner);

        if (DataContext is InventarioViewModel vm)
            await vm.CargarInventarioAsync();
    }

    private async void BtnEditarLote_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not Control { DataContext: Lote lote } control) return;
        if (Propietaria is not { } owner) return;

        var ventana = new EditarLoteWindow(lote, NombreProductoDeFila(control, lote));
        await ventana.ShowDialog(owner);

        if (DataContext is InventarioViewModel vm)
            await vm.CargarInventarioAsync();
    }

    /// <summary>El nombre del producto viene de la fila que contiene el detalle de lotes.</summary>
    private static string NombreProductoDeFila(Control control, Lote lote)
    {
        var producto = control.FindAncestorOfType<DataGridRow>()?.DataContext as Medicamentos ?? lote.Medicamento;
        return producto?.Nombre ?? "este producto";
    }
    
    private async void BtnImportarCSV_Click(object? sender, RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);

        // 1. Configuramos y abrimos el selector de archivos del sistema
        var archivos = await topLevel!.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Seleccionar archivo de inventario (CSV)",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                // Solo permitimos seleccionar archivos .csv
                new FilePickerFileType("Archivos CSV") { Patterns = new[] { "*.csv" } }
            }
        });

        // 2. Si el usuario seleccionó un archivo (y no canceló la ventana)
        if (archivos.Count < 1) return;

        var rutaArchivo = archivos[0].TryGetLocalPath();
        if (string.IsNullOrEmpty(rutaArchivo) || DataContext is not InventarioViewModel viewModel) return;

        string titulo;
        string mensaje;
        try
        {
            // 3. Importamos y recargamos la tabla para ver los resultados inmediatamente
            var resumen = await viewModel.ImportarCsvAsync(rutaArchivo);
            await viewModel.CargarInventarioAsync();

            titulo = resumen.HuboRechazos ? "Importación con observaciones" : "Importación completada";
            mensaje = $"Se aplicaron {resumen.Importadas} fila{(resumen.Importadas == 1 ? "" : "s")} como ingresos de lote.";
            if (resumen.HuboRechazos)
            {
                const int maxLineas = 12;
                var lineas = resumen.Rechazadas.Take(maxLineas);
                mensaje += $"\n\nFilas rechazadas ({resumen.Rechazadas.Count}):\n• " + string.Join("\n• ", lineas);
                if (resumen.Rechazadas.Count > maxLineas)
                    mensaje += $"\n… y {resumen.Rechazadas.Count - maxLineas} más.";
                mensaje += "\n\nCorrige esas filas en el archivo y vuelve a importar solo esas.";
            }
        }
        catch (Exception ex)
        {
            // El detalle ya quedó registrado en el log dentro del ViewModel.
            titulo = "No se pudo importar";
            mensaje = ex.Message;
        }

        if (Propietaria is { } owner)
        {
            var aviso = new ConfirmWindow(titulo, mensaje, "Entendido", peligro: false, mostrarCancelar: false);
            await aviso.ShowDialog<bool>(owner);
        }
    }

    private async void TxtBusqueda_KeyDown(object? sender, Avalonia.Input.KeyEventArgs e)
    {
        // Cuando el usuario presiona Enter, asumir que completó el escaneo
        if (e.Key == Avalonia.Input.Key.Return)
        {
            e.Handled = true;

            if (DataContext is InventarioViewModel viewModel && sender is TextBox txt)
            {
                var codigoEscaneado = txt.Text;

                // Obtener el código del medicamento encontrado o None si no existe
                var medicamentoEncontrado = viewModel.Medicamentos.FirstOrDefault();

                if (medicamentoEncontrado == null && !string.IsNullOrWhiteSpace(codigoEscaneado))
                {
                    // El código NO existe → abrir ventana para crear nuevo medicamento
                    var ventanaRegistro = new ProductoWindow(null, codigoEscaneado);

                    if (Propietaria is { } mainWindow)
                        await ventanaRegistro.ShowDialog(mainWindow);

                    // Después de crear el medicamento, recargar inventario
                    await viewModel.CargarInventarioAsync();

                    // Limpiar el campo de búsqueda
                    txt.Text = string.Empty;
                }
                // Si existe, la búsqueda ya se mostró, solo limpiar en el siguiente escaneo
                else if (medicamentoEncontrado != null)
                {
                    // El medicamento existe, limpiar para el siguiente escaneo
                    txt.Text = string.Empty;
                }
            }
        }
    }
}
