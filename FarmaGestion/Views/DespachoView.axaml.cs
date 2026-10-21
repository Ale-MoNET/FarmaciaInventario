using System.Threading.Tasks;
using Avalonia.Controls;
using FarmaGestion.Data.Repositories;
using FarmaGestion.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace FarmaGestion.Views;

public partial class DespachoView : UserControl
{
    public DespachoView()
    {
        InitializeComponent();

        var viewModel = App.Services.GetRequiredService<DespachoViewModel>();

        // Al confirmar un despacho abrimos el comprobante para descargarlo o imprimirlo.
        viewModel.OnDespachoConfirmado = MostrarComprobanteAsync;

        DataContext = viewModel;

        this.Loaded += (sender, args) =>
        {
            TxtCodigoBusqueda.Focus();
        };
    }

    private async Task MostrarComprobanteAsync(ComprobanteDespacho comprobante)
    {
        var ventana = new ComprobanteWindow(comprobante);

        if (TopLevel.GetTopLevel(this) is Window propietaria)
            await ventana.ShowDialog(propietaria);
        else
            ventana.Show();

        // Dejamos el cursor listo para el siguiente despacho.
        TxtCodigoBusqueda.Focus();
    }
}
