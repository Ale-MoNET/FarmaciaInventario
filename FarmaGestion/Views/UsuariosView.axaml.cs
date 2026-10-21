using Avalonia.Controls;
using Avalonia.Interactivity;
using FarmaGestion.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace FarmaGestion.Views;

public partial class UsuariosView : UserControl
{
    public UsuariosView()
    {
        InitializeComponent();
        DataContext = App.Services.GetRequiredService<UsuariosViewModel>();
    }

    private async void BtnNuevoUsuario_Click(object? sender, RoutedEventArgs e)
    {
        var ventana = new UsuarioWindow();
        if (TopLevel.GetTopLevel(this) is Window mainWindow)
            await ventana.ShowDialog(mainWindow);

        if (DataContext is UsuariosViewModel vm)
            await vm.CargarUsuariosAsync();
    }

    private async void BtnEditar_Click(object? sender, RoutedEventArgs e)
    {
        // El botón de editar vive en la fila; su DataContext es el UsuarioListItem.
        if (sender is not Control { DataContext: UsuarioListItem seleccionado }) return;

        var ventana = new UsuarioWindow(seleccionado.Id);
        if (TopLevel.GetTopLevel(this) is Window mainWindow)
            await ventana.ShowDialog(mainWindow);

        if (DataContext is UsuariosViewModel vm)
            await vm.CargarUsuariosAsync();
    }

    private async void BtnToggleActivo_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not Control { DataContext: UsuarioListItem item }) return;
        if (DataContext is not UsuariosViewModel vm) return;

        var error = await vm.ToggleActivoAsync(item);
        if (error != null && TopLevel.GetTopLevel(this) is Window owner)
        {
            var aviso = new ConfirmWindow("Acción no permitida", error, "Entendido",
                peligro: false, mostrarCancelar: false);
            await aviso.ShowDialog<bool>(owner);
        }
    }

    private async void BtnEliminarUsuario_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not Control { DataContext: UsuarioListItem item }) return;
        if (TopLevel.GetTopLevel(this) is not Window owner) return;
        if (DataContext is not UsuariosViewModel vm) return;

        var confirm = new ConfirmWindow(
            "Eliminar usuario",
            $"¿Seguro que deseas eliminar al usuario «{item.NombreUsuario}»? Esta acción no se puede deshacer.\n" +
            "Sugerencia: si solo quieres impedir su acceso, mejor desactívalo.",
            "Eliminar");
        var confirmado = await confirm.ShowDialog<bool>(owner);
        if (!confirmado) return;

        var error = await vm.EliminarUsuarioAsync(item);
        if (error != null)
        {
            var aviso = new ConfirmWindow("No se puede eliminar", error, "Entendido",
                peligro: false, mostrarCancelar: false);
            await aviso.ShowDialog<bool>(owner);
        }
    }
}
