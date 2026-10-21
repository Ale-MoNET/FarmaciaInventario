using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FarmaGestion.Data.Repositories;
using FarmaGestion.Models;
using FarmaGestion.Services;

namespace FarmaGestion.ViewModels;

public partial class LoginViewModel : ObservableObject
{
    private readonly IUsuarioRepository _usuarioRepo;

    [ObservableProperty] private string _nombreUsuario = string.Empty;
    [ObservableProperty] private string _password = string.Empty;
    [ObservableProperty] private string _mensajeError = string.Empty;
    [ObservableProperty] private bool _mostrarError;

    /// <summary>
    /// Callback que se ejecuta cuando el login es exitoso.
    /// Pasa el objeto Usuario autenticado.
    /// </summary>
    public Action<Usuario>? OnLoginExitoso;

    public LoginViewModel(IUsuarioRepository usuarioRepo)
    {
        _usuarioRepo = usuarioRepo;
    }

    [RelayCommand]
    private async Task IniciarSesionAsync()
    {
        // Limpiar mensaje de error previo
        MostrarError = false;
        MensajeError = string.Empty;

        // Validación básica de campos vacíos
        if (string.IsNullOrWhiteSpace(NombreUsuario) || string.IsNullOrWhiteSpace(Password))
        {
            MensajeError = "⚠️ Por favor, ingresa usuario y contraseña.";
            MostrarError = true;
            return;
        }

        Usuario? usuario;
        try
        {
            usuario = await _usuarioRepo.ObtenerActivoPorNombreAsync(NombreUsuario);
        }
        catch (Exception ex)
        {
            LoggerService.Error("Error al consultar usuario en el login", ex);
            MensajeError = "❌ No se pudo conectar con la base de datos.";
            MostrarError = true;
            return;
        }

        if (usuario == null)
        {
            MensajeError = "❌ Usuario no encontrado o desactivado.";
            MostrarError = true;
            return;
        }

        // Verificar la contraseña con PBKDF2
        if (!AuthService.VerificarPassword(Password, usuario.PasswordHash, usuario.PasswordSalt))
        {
            LoggerService.Advertencia($"Intento de login fallido para el usuario '{NombreUsuario}'.");
            MensajeError = "❌ Contraseña incorrecta.";
            MostrarError = true;
            return;
        }

        // Login exitoso: registrar la sesión y notificar con el usuario autenticado
        SesionActual.Establecer(usuario);
        LoggerService.Info($"Inicio de sesión: {usuario.NombreUsuario} ({usuario.Rol}).");
        OnLoginExitoso?.Invoke(usuario);
    }
}
