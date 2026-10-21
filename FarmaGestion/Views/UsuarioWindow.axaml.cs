using Avalonia.Controls;
using FarmaGestion.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace FarmaGestion.Views;

public partial class UsuarioWindow : Window
{
    public UsuarioWindow() : this(null) { }

    public UsuarioWindow(int? usuarioId)
    {
        InitializeComponent();

        var viewModel = App.Services.GetRequiredService<UsuarioFormViewModel>();

        // Si se pasa un ID, cargar el usuario para edición
        if (usuarioId.HasValue)
        {
            _ = viewModel.CargarParaEdicionAsync(usuarioId.Value);
        }

        viewModel.OnGuardadoExitoso = () =>
        {
            this.Close();
        };

        DataContext = viewModel;

        this.Opened += (s, e) =>
        {
            TxtNombreUsuario.Focus();
        };
    }
}
