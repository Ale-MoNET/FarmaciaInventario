using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using FarmaGestion.Models;
using FarmaGestion.Services;
using FarmaGestion.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace FarmaGestion.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Muestra el nombre del usuario autenticado en la barra superior.
    /// </summary>
    public void MostrarUsuario(string nombreUsuario, string rol)
    {
        TxtUsuarioNombre.Text = nombreUsuario;
        TxtUsuarioRol.Text = rol.ToUpperInvariant();
        TxtAvatar.Text = string.IsNullOrEmpty(nombreUsuario)
            ? "?"
            : nombreUsuario[..1].ToUpperInvariant();

        // Control de acceso por rol (RBAC): solo el Admin gestiona usuarios.
        TabUsuarios.IsVisible = Rol.EsAdmin(rol);
    }

    private void BtnCerrarSesion_Click(object? sender, RoutedEventArgs e)
    {
        if (Avalonia.Application.Current?.ApplicationLifetime
            is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Limpiar la sesión actual antes de volver al login
            SesionActual.Limpiar();

            // Crear nueva ventana de login
            var loginWindow = new LoginWindow();
            var loginViewModel = App.Services.GetRequiredService<LoginViewModel>();

            loginViewModel.OnLoginExitoso = (usuario) =>
            {
                var mainWindow = new MainWindow
                {
                    DataContext = new MainWindowViewModel(),
                };
                mainWindow.MostrarUsuario(usuario.NombreUsuario, usuario.Rol);

                desktop.MainWindow = mainWindow;
                mainWindow.Show();
                loginWindow.Close();
            };

            loginWindow.DataContext = loginViewModel;
            desktop.MainWindow = loginWindow;
            loginWindow.Show();

            // Cerrar la ventana actual
            this.Close();
        }
    }
}