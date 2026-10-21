using Avalonia.Controls;
using Avalonia.Interactivity;
using FarmaGestion.Models;
using FarmaGestion.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace FarmaGestion.Views;

public partial class EditarLoteWindow : Window
{
    /// <summary>Constructor sin datos; lo usa el previsualizador de Avalonia.</summary>
    public EditarLoteWindow() : this(new Lote { NumeroLote = "—" }, string.Empty) { }

    public EditarLoteWindow(Lote lote, string nombreProducto)
    {
        InitializeComponent();
        var viewModel = App.Services.GetRequiredService<EditarLoteViewModel>();
        viewModel.Inicializar(lote, nombreProducto);
        viewModel.OnGuardado = () => Close(true);
        DataContext = viewModel;

        Opened += (_, _) => TxtLote.Focus();
    }

    private void Cancelar_Click(object? sender, RoutedEventArgs e) => Close(false);
}
