using System;
using System.Linq;
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

            // 2. Preparar la base de datos: comprobar versión, aplicar migraciones pendientes
            //    y crear el super usuario (una sola vez). Si algo falla, se informa en vez de cerrar sin más.
            var factory = Services.GetRequiredService<IDbContextFactory<InventarioContext>>();
            string? problema = PrepararBaseDeDatos(factory);
            if (problema != null)
            {
                var aviso = new ConfirmWindow("No se pudo iniciar FarmaGestión", problema,
                    "Cerrar", peligro: false, mostrarCancelar: false);
                aviso.Closed += (_, _) => desktop.Shutdown();
                desktop.MainWindow = aviso;
                base.OnFrameworkInitializationCompleted();
                return;
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

    /// <summary>
    /// Aplica las migraciones y siembra el admin. Devuelve un mensaje para el usuario si no
    /// se puede continuar: base inaccesible, o base ya migrada por una versión más nueva de
    /// la app (ocurre con la base compartida cuando una PC se actualiza antes que las demás).
    /// </summary>
    private static string? PrepararBaseDeDatos(IDbContextFactory<InventarioContext> factory)
    {
        try
        {
            using var context = factory.CreateDbContext();

            var conocidas = context.Database.GetMigrations().ToHashSet();
            var desconocidas = context.Database.GetAppliedMigrations()
                .Where(m => !conocidas.Contains(m))
                .ToList();

            if (desconocidas.Count > 0)
            {
                LoggerService.Error("La base de datos fue migrada por una versión más nueva: " +
                                    string.Join(", ", desconocidas), new InvalidOperationException("Versión de base de datos más nueva que la app"));
                return "La base de datos ya fue actualizada por una versión más nueva de FarmaGestión. " +
                       "Instala la versión nueva en esta PC para seguir trabajando.";
            }

            context.Database.Migrate();
            AuthService.SeedSuperUsuario(context);
            return null;
        }
        catch (Exception ex)
        {
            LoggerService.Error("No se pudo preparar la base de datos al iniciar", ex);
            return "No se pudo abrir la base de datos en:\n" + AppConfig.ConnectionString +
                   "\n\nRevisa que la carpeta exista y tengas permiso de escritura (si es una carpeta compartida, que el servidor esté encendido).\n\nDetalle: " + ex.Message;
        }
    }
}
