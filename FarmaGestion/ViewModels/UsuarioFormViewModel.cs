using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FarmaGestion.Data.Repositories;
using FarmaGestion.Models;
using FarmaGestion.Services;

namespace FarmaGestion.ViewModels;

public partial class UsuarioFormViewModel : ObservableObject
{
    private readonly IUsuarioRepository _usuarioRepo;

    [ObservableProperty] private string _nombreUsuario = string.Empty;
    [ObservableProperty] private string _password = string.Empty;
    [ObservableProperty] private string _confirmarPassword = string.Empty;
    [ObservableProperty] private string _rolSeleccionado = Rol.Empleado;
    [ObservableProperty] private bool _activo = true;
    [ObservableProperty] private string _mensajeError = string.Empty;
    [ObservableProperty] private bool _mostrarError;

    // Indica si estamos editando un usuario existente
    [ObservableProperty] private bool _esEdicion;
    [ObservableProperty] private string _tituloVentana = "Registrar Nuevo Usuario";

    // ID del usuario que se está editando (0 = nuevo)
    private int _usuarioId;

    /// <summary>
    /// Lista de roles disponibles para el ComboBox.
    /// </summary>
    public List<string> RolesDisponibles { get; } = new(Rol.Todos);

    /// <summary>
    /// Callback que se ejecuta al guardar exitosamente.
    /// </summary>
    public Action? OnGuardadoExitoso;

    public UsuarioFormViewModel(IUsuarioRepository usuarioRepo)
    {
        _usuarioRepo = usuarioRepo;
    }

    /// <summary>
    /// Configura el ViewModel en modo edición con los datos del usuario existente.
    /// </summary>
    public async Task CargarParaEdicionAsync(int id)
    {
        var usuario = await _usuarioRepo.ObtenerPorIdAsync(id);
        if (usuario == null) return;

        _usuarioId = usuario.Id;
        NombreUsuario = usuario.NombreUsuario;
        RolSeleccionado = usuario.Rol;
        Activo = usuario.Activo;
        EsEdicion = true;
        TituloVentana = $"Editar Usuario: {usuario.NombreUsuario}";

        // En modo edición, la contraseña queda vacía (opcional cambiarla)
        Password = string.Empty;
        ConfirmarPassword = string.Empty;
    }

    [RelayCommand]
    private async Task GuardarAsync()
    {
        MostrarError = false;
        MensajeError = string.Empty;

        // Validar nombre de usuario
        if (string.IsNullOrWhiteSpace(NombreUsuario))
        {
            MostrarErrorValidacion("⚠️ El nombre de usuario es obligatorio.");
            return;
        }

        if (NombreUsuario.Length < 3)
        {
            MostrarErrorValidacion("⚠️ El nombre de usuario debe tener al menos 3 caracteres.");
            return;
        }

        // Validar contraseña (obligatoria en creación, opcional en edición)
        bool cambiaPassword = !EsEdicion || !string.IsNullOrEmpty(Password);
        if (cambiaPassword)
        {
            if (string.IsNullOrWhiteSpace(Password))
            {
                MostrarErrorValidacion("⚠️ La contraseña es obligatoria.");
                return;
            }

            if (!EsPasswordSegura(Password, out var motivo))
            {
                MostrarErrorValidacion($"⚠️ {motivo}");
                return;
            }

            if (Password != ConfirmarPassword)
            {
                MostrarErrorValidacion("❌ Las contraseñas no coinciden.");
                return;
            }
        }

        try
        {
            if (EsEdicion)
            {
                // Verificar que no se duplique el nombre si lo cambió
                var actual = await _usuarioRepo.ObtenerPorIdAsync(_usuarioId);
                if (actual == null)
                {
                    MostrarErrorValidacion("❌ El usuario ya no existe.");
                    return;
                }

                if (actual.NombreUsuario != NombreUsuario &&
                    await _usuarioRepo.ExisteNombreAsync(NombreUsuario))
                {
                    MostrarErrorValidacion("❌ Ya existe un usuario con ese nombre.");
                    return;
                }

                string? hash = null, salt = null;
                if (cambiaPassword)
                {
                    salt = AuthService.GenerarSalt();
                    hash = AuthService.HashPassword(Password, salt);
                }

                await _usuarioRepo.ActualizarAsync(_usuarioId, NombreUsuario, RolSeleccionado, Activo, hash, salt);
            }
            else
            {
                if (await _usuarioRepo.ExisteNombreAsync(NombreUsuario))
                {
                    MostrarErrorValidacion("❌ Ya existe un usuario con ese nombre.");
                    return;
                }

                var salt = AuthService.GenerarSalt();
                await _usuarioRepo.CrearAsync(new Usuario
                {
                    NombreUsuario = NombreUsuario,
                    PasswordHash = AuthService.HashPassword(Password, salt),
                    PasswordSalt = salt,
                    Rol = RolSeleccionado,
                    Activo = Activo,
                    FechaCreacion = DateTime.Now
                });
            }
        }
        catch (Exception ex)
        {
            LoggerService.Error("Error al guardar usuario", ex);
            MostrarErrorValidacion("❌ No se pudo guardar el usuario. Inténtalo de nuevo.");
            return;
        }

        LoggerService.Info($"Usuario {(EsEdicion ? "actualizado" : "creado")}: {NombreUsuario} ({RolSeleccionado})");
        OnGuardadoExitoso?.Invoke();
    }

    private void MostrarErrorValidacion(string mensaje)
    {
        MensajeError = mensaje;
        MostrarError = true;
    }

    /// <summary>
    /// Valida la robustez de la contraseña según la política configurada (appsettings.json).
    /// </summary>
    private static bool EsPasswordSegura(string password, out string motivo)
    {
        var politica = AppConfig.PoliticaPassword;

        if (password.Length < politica.LongitudMinima)
        {
            motivo = $"La contraseña debe tener al menos {politica.LongitudMinima} caracteres.";
            return false;
        }
        if (politica.RequiereMayuscula && !password.Any(char.IsUpper))
        {
            motivo = "La contraseña debe incluir al menos una letra mayúscula.";
            return false;
        }
        if (politica.RequiereDigito && !password.Any(char.IsDigit))
        {
            motivo = "La contraseña debe incluir al menos un número.";
            return false;
        }
        motivo = string.Empty;
        return true;
    }
}
