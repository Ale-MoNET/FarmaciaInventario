using Avalonia.Controls;
using FarmaGestion.ViewModels;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Microsoft.Extensions.DependencyInjection;
using System.Linq;

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

    private async void BtnRegistrar_Click(object? sender, RoutedEventArgs e)
    {
        var ventanaRegistro = new ProductoWindow();

        if (TopLevel.GetTopLevel(this) is Window mainWindow)
            await ventanaRegistro.ShowDialog(mainWindow);

        if (DataContext is InventarioViewModel vm)
        {
            await vm.CargarInventarioAsync();
        }
    }

    private async void BtnEditarProducto_Click(object? sender, RoutedEventArgs e)
    {
        // El botón vive en la fila; su DataContext es el Medicamentos de esa fila.
        if (sender is not Control { DataContext: Models.Medicamentos med }) return;

        var ventana = new ProductoWindow(med.Id);
        if (TopLevel.GetTopLevel(this) is Window mainWindow)
            await ventana.ShowDialog(mainWindow);

        if (DataContext is InventarioViewModel vm)
            await vm.CargarInventarioAsync();
    }

    private async void BtnEliminarProducto_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not Control { DataContext: Models.Medicamentos med }) return;
        if (TopLevel.GetTopLevel(this) is not Window owner) return;
        if (DataContext is not InventarioViewModel vm) return;

        // 1. Confirmación
        var confirm = new ConfirmWindow(
            "Eliminar medicamento",
            $"¿Seguro que deseas eliminar «{med.Nombre}» ({med.CodigoBarras})? Esta acción no se puede deshacer.",
            "Eliminar");
        var confirmado = await confirm.ShowDialog<bool>(owner);
        if (!confirmado) return;

        // 2. Eliminar (o avisar si tiene despachos)
        bool eliminado = await vm.EliminarAsync(med.Id);
        if (!eliminado)
        {
            var aviso = new ConfirmWindow(
                "No se puede eliminar",
                $"«{med.Nombre}» tiene despachos asociados, por lo que no se puede eliminar (se perdería el historial de ventas). Puedes marcarlo con 0 existencias.",
                "Entendido", peligro: false, mostrarCancelar: false);
            await aviso.ShowDialog<bool>(owner);
        }
    }
    
    private async void BtnImportarCSV_Click(object? sender, RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);

        // 1. Configuramos y abrimos la ventana del Finder de Mac
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
        if (archivos.Count >= 1)
        {
            // Extraemos la ruta real en tu disco duro
            var rutaArchivo = archivos[0].TryGetLocalPath();

            if (!string.IsNullOrEmpty(rutaArchivo) && DataContext is InventarioViewModel viewModel)
            {
                try
                {
                    // 3. Llamamos a nuestro método del ViewModel
                    await viewModel.ImportarCsvAsync(rutaArchivo);

                    // 4. Recargamos la tabla para ver los resultados inmediatamente
                    await viewModel.CargarInventarioAsync();
                }
                catch (System.Exception)
                {
                    // El error ya quedó registrado en el log dentro del ViewModel.
                }
            }
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

                    if (TopLevel.GetTopLevel(this) is Window mainWindow)
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