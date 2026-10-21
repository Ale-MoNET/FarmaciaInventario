using System;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using FarmaGestion.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace FarmaGestion.Views;

public partial class IngresoWindow : Window
{
    private readonly IngresoMercaderiaViewModel _viewModel;

    public IngresoWindow() : this(null) { }

    /// <param name="codigoInicial">Código ya conocido (p. ej. desde la fila del inventario).</param>
    public IngresoWindow(string? codigoInicial)
    {
        InitializeComponent();
        _viewModel = App.Services.GetRequiredService<IngresoMercaderiaViewModel>();

        // Si el código no existe, ofrecemos registrar el producto sin salir del flujo de ingreso.
        _viewModel.OnProductoNoEncontrado = async codigo =>
        {
            var pregunta = new ConfirmWindow(
                "Producto no registrado",
                $"No existe un producto con el código {codigo}. ¿Quieres registrarlo ahora?",
                "Registrar producto", peligro: false);
            if (!await pregunta.ShowDialog<bool>(this)) return;

            var registro = new ProductoWindow(null, codigo);
            await registro.ShowDialog(this);
        };

        // Tras un ingreso correcto el cursor vuelve al código para el siguiente escaneo.
        // El CalendarDatePicker conserva el texto cuando SelectedDate pasa a null desde el
        // binding. Asignar una fecha y, en un ciclo posterior del despachador, volver a null
        // sí limpia el cuadro (comprobado con la prueba de humo headless).
        _viewModel.OnIngresoRegistrado = () =>
        {
            DpVencimiento.SelectedDate = DateTime.Today;
            Dispatcher.UIThread.Post(() => DpVencimiento.SelectedDate = null, DispatcherPriority.Background);
            TxtCodigo.Focus();
        };

        DataContext = _viewModel;

        Opened += async (_, _) =>
        {
            if (!string.IsNullOrWhiteSpace(codigoInicial))
            {
                _viewModel.CodigoBarras = codigoInicial;
                if (await _viewModel.BuscarProductoAsync())
                    TxtLote.Focus();
                else
                    TxtCodigo.Focus();
            }
            else
            {
                TxtCodigo.Focus();
            }
        };
    }

    private async void TxtCodigo_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Return) return;
        e.Handled = true;

        if (await _viewModel.BuscarProductoAsync())
            TxtLote.Focus();
    }

    private void Cerrar_Click(object? sender, RoutedEventArgs e) => Close();
}
