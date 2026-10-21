using Avalonia.Controls;
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

        DataContext = viewModel;

        this.Opened += (s, e) => TxtCodigoBarras.Focus();
    }
}
