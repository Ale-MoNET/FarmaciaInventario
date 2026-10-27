using Avalonia.Controls;
using Avalonia.Interactivity;
using FarmaGestion.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace FarmaGestion.Views;

public partial class ReportesView : UserControl
{
    public ReportesView()
    {
        InitializeComponent();
        DataContext = App.Services.GetRequiredService<ReportesViewModel>();
    }

    private async void BtnBajaLote_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not Control { DataContext: LoteVencimientoItem item }) return;
        if (TopLevel.GetTopLevel(this) is not Window owner) return;

        var ventana = new BajaLoteWindow(item.Lote, item.Producto);
        await ventana.ShowDialog(owner);

        if (DataContext is ReportesViewModel vm)
            await vm.CargarVencimientosAsync();
    }

    private async void BtnVerDespacho_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not Control { DataContext: DespachoReporteItem item }) return;
        if (TopLevel.GetTopLevel(this) is not Window owner) return;
        if (DataContext is not ReportesViewModel vm) return;

        var comprobante = await vm.ObtenerComprobanteAsync(item.Id);
        if (comprobante is null) return;

        // Mismo comprobante que en caja, en modo consulta (permite descargar, imprimir o deshacer).
        var ventana = new ComprobanteWindow(comprobante, consulta: true);
        await ventana.ShowDialog(owner);
    }

    private async void BtnDeshacerDespacho_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not Control { DataContext: DespachoReporteItem item }) return;
        if (TopLevel.GetTopLevel(this) is not Window owner) return;
        if (DataContext is not ReportesViewModel vm) return;

        var comprobante = await vm.ObtenerComprobanteAsync(item.Id);
        if (comprobante is null) return;

        // Si se deshace, el ViewModel se refresca solo al recibir InventarioActualizadoMessage.
        var ventana = new AnularDespachoWindow(comprobante);
        await ventana.ShowDialog<string?>(owner);
    }
}
