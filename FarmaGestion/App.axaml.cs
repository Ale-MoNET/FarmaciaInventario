using System;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using FarmaGestion.Data;
using FarmaGestion.Services;
using FarmaGestion.ViewModels;
using FarmaGestion.Views;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FarmaGestion;

public partial class App : Application
{
    /// <summary>
    /// Proveedor de servicios global. Las vistas resuelven sus ViewModels desde aquí
    /// en lugar de instanciarlos manualmente.
    /// </summary>
    public static IServiceProvider Services { get; private set; } = null!;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // 1. Cargar configuración y construir el contenedor de dependencias
            AppConfig.Inicializar();
            Services = ServiceConfiguration.Construir();

            // 2. Aplicar migraciones pendientes y crear el super usuario (una sola vez)
            var factory = Services.GetRequiredService<IDbContextFactory<InventarioContext>>();
            using (var context = factory.CreateDbContext())
            {
                context.Database.Migrate();
                AuthService.SeedSuperUsuario(context);
            }

            // 3. Crear la ventana de login con su ViewModel inyectado
            var loginWindow = new LoginWindow();
            var loginViewModel = Services.GetRequiredService<LoginViewModel>();

            // 4. Cuando el login sea exitoso, abrir la ventana principal
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
        }

        base.OnFrameworkInitializationCompleted();
    }
}
