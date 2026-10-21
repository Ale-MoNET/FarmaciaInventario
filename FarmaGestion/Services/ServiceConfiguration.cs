using System;
using FarmaGestion.Data;
using FarmaGestion.Data.Repositories;
using FarmaGestion.ViewModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FarmaGestion.Services;

/// <summary>
/// Configura el contenedor de inyección de dependencias: fábrica de DbContext,
/// repositorios y ViewModels. Sustituye la instanciación manual de contextos y
/// ViewModels que había antes repartida por el código.
/// </summary>
public static class ServiceConfiguration
{
    public static IServiceProvider Construir()
    {
        var services = new ServiceCollection();

        // Fábrica de contextos: cada operación crea un contexto de vida corta,
        // igual que antes, pero ahora es inyectable y testeable.
        // El interceptor ajusta SQLite para funcionar de forma segura en carpeta de red.
        services.AddDbContextFactory<InventarioContext>(options =>
            options.UseSqlite(AppConfig.ConnectionString)
                   .AddInterceptors(new Data.SqlitePragmaInterceptor()));

        // Repositorios (sin estado salvo la fábrica → singleton)
        services.AddSingleton<IMedicamentoRepository, MedicamentoRepository>();
        services.AddSingleton<IUsuarioRepository, UsuarioRepository>();
        services.AddSingleton<IDespachoRepository, DespachoRepository>();

        // Servicios
        services.AddSingleton<IBarcodeService, BarcodeService>();

        // ViewModels
        services.AddTransient<LoginViewModel>();
        services.AddTransient<InventarioViewModel>();
        services.AddTransient<DespachoViewModel>();
        services.AddTransient<ReportesViewModel>();
        services.AddTransient<UsuariosViewModel>();
        services.AddTransient<ProductoViewModel>();
        services.AddTransient<IngresoMercaderiaViewModel>();
        services.AddTransient<BajaLoteViewModel>();
        services.AddTransient<EditarLoteViewModel>();
        services.AddTransient<UsuarioFormViewModel>();

        return services.BuildServiceProvider();
    }
}
