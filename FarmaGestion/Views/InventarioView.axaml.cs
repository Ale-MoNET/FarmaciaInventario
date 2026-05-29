using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using FarmaGestion.ViewModels;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;

namespace FarmaGestion.Views;

public partial class InventarioView : UserControl
{
    public InventarioView()
    {
        InitializeComponent();
        DataContext = new InventarioViewModel();
    }

    private async void BtnRegistrar_Click(object? sender, RoutedEventArgs e)
    {
        var ventanaRegistro = new ProductoWindow();

        var mainWindow = (Window)TopLevel.GetTopLevel(this);
        
        await ventanaRegistro.ShowDialog(mainWindow);

        if (DataContext is InventarioViewModel vm)
        {
            vm.CargarInventario();
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
                // 3. Llamamos a nuestro método del ViewModel
                await viewModel.ImportarCsvAsync(rutaArchivo);
            
                // 4. Recargamos la tabla para ver los resultados inmediatamente
                viewModel.CargarInventario();
            }
        }
    }
}