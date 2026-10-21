using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using FarmaGestion.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace FarmaGestion.Views;

public partial class ProductoWindow : Window
{
    public ProductoWindow() : this(null, null) { }

    public ProductoWindow(int? medicamentoId) : this(medicamentoId, null) { }

    public ProductoWindow(int? medicamentoId, string? codigoEscaneado)
    {
        InitializeComponent();
        var viewModel = App.Services.GetRequiredService<ProductoViewModel>();

        // Si se pasa un id, cargar el medicamento para edición.
        if (medicamentoId.HasValue)
            _ = viewModel.CargarParaEdicionAsync(medicamentoId.Value);
        // Si se pasa un código escaneado, pre-rellenar para crear nuevo
        else if (!string.IsNullOrWhiteSpace(codigoEscaneado))
            viewModel.InicializarConCodigoEscaneado(codigoEscaneado);

        viewModel.OnGuardadoExitoso = () => this.Close();

        // El código ya existía: el usuario quiere sumar existencias, no editar el producto.
        viewModel.OnIrAIngresoMercaderia = async codigo =>
        {
            var ingreso = new IngresoWindow(codigo);
            await ingreso.ShowDialog(this);
            Close();
        };

        DataContext = viewModel;

        this.Opened += (s, e) => TxtCodigoBarras.Focus();
    }

    /// <summary>Enter en el código (el lector de barras lo envía solo): comprobar y pasar al nombre.</summary>
    private async void TxtCodigoBarras_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Return) return;
        e.Handled = true;

        await ComprobarCodigoAsync();
        TxtNombre.Focus();
    }

    private async void TxtCodigoBarras_LostFocus(object? sender, RoutedEventArgs e) => await ComprobarCodigoAsync();

    private Task ComprobarCodigoAsync()
        => DataContext is ProductoViewModel vm ? vm.ComprobarCodigoExistenteAsync() : Task.CompletedTask;
}
