using Avalonia.Controls;
using Avalonia.Interactivity;
using FarmaGestion.Models;
using FarmaGestion.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace FarmaGestion.Views;

public partial class BajaLoteWindow : Window
{
    /// <summary>Constructor sin datos; lo usa el previsualizador de Avalonia.</summary>
    public BajaLoteWindow() : this(new Lote { NumeroLote = "—" }, string.Empty) { }

    public BajaLoteWindow(Lote lote, string nombreProducto)
    {
        InitializeComponent();
        var viewModel = App.Services.GetRequiredService<BajaLoteViewModel>();
        viewModel.Inicializar(lote, nombreProducto);
        viewModel.OnGuardado = () => Close(true);
        DataContext = viewModel;

        Opened += (_, _) => TxtCantidad.Focus();
    }

    private void Cancelar_Click(object? sender, RoutedEventArgs e) => Close(false);
}
