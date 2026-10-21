using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FarmaGestion.Data.Repositories;
using FarmaGestion.Models;
using FarmaGestion.Services;

namespace FarmaGestion.ViewModels;

public partial class UsuariosViewModel : ObservableObject
{
    private readonly IUsuarioRepository _usuarioRepo;
    private readonly List<UsuarioListItem> _todos = new();

    [ObservableProperty] private ObservableCollection<UsuarioListItem> _usuarios = new();

    [ObservableProperty] private string _usrQuery = string.Empty;
    [ObservableProperty] private string _rolFiltro = "Todos";

    // KPIs
    [ObservableProperty] private int _total;
    [ObservableProperty] private int _activos;
    [ObservableProperty] private int _administradores;
    [ObservableProperty] private string _countLabel = "0 usuarios";

    public UsuariosViewModel(IUsuarioRepository usuarioRepo)
    {
        _usuarioRepo = usuarioRepo;
        _ = CargarUsuariosAsync();
    }

    partial void OnUsrQueryChanged(string value) => AplicarFiltro();
    partial void OnRolFiltroChanged(string value) => AplicarFiltro();

    [RelayCommand]
    private void FiltrarRol(string rol) => RolFiltro = rol;

    public async Task CargarUsuariosAsync()
    {
        try
        {
            var lista = await _usuarioRepo.ObtenerTodosAsync();
            _todos.Clear();
            foreach (var u in lista)
            {
                _todos.Add(new UsuarioListItem
                {
                    Id = u.Id,
                    NombreUsuario = u.NombreUsuario,
                    Rol = u.Rol,
                    Activo = u.Activo,
                    FechaCreacion = u.FechaCreacion
                });
            }

            Total = _todos.Count;
            Activos = _todos.Count(u => u.Activo);
            Administradores = _todos.Count(u => Rol.EsAdmin(u.Rol));

            AplicarFiltro();
        }
        catch (Exception ex)
        {
            LoggerService.Error("Error al cargar la lista de usuarios", ex);
        }
    }

    /// <summary>Activa/desactiva un usuario. Devuelve null si tuvo éxito, o un mensaje de bloqueo.</summary>
    public async Task<string?> ToggleActivoAsync(UsuarioListItem item)
    {
        bool nuevoEstado = !item.Activo;

        if (!nuevoEstado) // se va a desactivar
        {
            if (item.Id == SesionActual.Usuario?.Id)
                return "No puedes desactivar tu propio usuario.";

            if (Rol.EsAdmin(item.Rol) && _todos.Count(u => Rol.EsAdmin(u.Rol) && u.Activo) <= 1)
                return "Debe quedar al menos un administrador activo.";
        }

        await _usuarioRepo.CambiarActivoAsync(item.Id, nuevoEstado);
        LoggerService.Info($"Usuario {(nuevoEstado ? "activado" : "desactivado")}: {item.NombreUsuario}.");
        await CargarUsuariosAsync();
        return null;
    }

    /// <summary>Elimina un usuario. Devuelve null si tuvo éxito, o un mensaje de bloqueo.</summary>
    public async Task<string?> EliminarUsuarioAsync(UsuarioListItem item)
    {
        if (item.Id == SesionActual.Usuario?.Id)
            return "No puedes eliminar tu propio usuario.";

        if (Rol.EsAdmin(item.Rol) && _todos.Count(u => Rol.EsAdmin(u.Rol)) <= 1)
            return "Debe existir al menos un administrador.";

        await _usuarioRepo.EliminarAsync(item.Id);
        LoggerService.Info($"Usuario eliminado: {item.NombreUsuario}.");
        await CargarUsuariosAsync();
        return null;
    }

    private void AplicarFiltro()
    {
        var q = (UsrQuery ?? string.Empty).Trim().ToLowerInvariant();

        var filtrados = _todos.Where(u =>
            (RolFiltro == "Todos" || string.Equals(u.Rol, RolFiltro, StringComparison.Ordinal)) &&
            (q.Length == 0 || u.NombreUsuario.ToLowerInvariant().Contains(q)));

        Usuarios = new ObservableCollection<UsuarioListItem>(filtrados);
        CountLabel = $"{Usuarios.Count} de {_todos.Count} usuarios";
    }
}

/// <summary>
/// DTO para mostrar usuarios en la tabla sin exponer hash/salt, con propiedades de presentación.
/// </summary>
public class UsuarioListItem
{
    public int Id { get; set; }
    public string NombreUsuario { get; set; } = string.Empty;
    public string Rol { get; set; } = string.Empty;
    public bool Activo { get; set; }
    public DateTime FechaCreacion { get; set; }

    private bool EsAdmin => Models.Rol.EsAdmin(Rol);

    /// <summary>Iniciales para el avatar (hasta 2 letras del nombre de usuario).</summary>
    public string Iniciales =>
        string.IsNullOrWhiteSpace(NombreUsuario)
            ? "?"
            : new string(NombreUsuario.Where(char.IsLetterOrDigit).Take(2).ToArray()).ToUpperInvariant();

    public string AvatarBg => EsAdmin ? "#34E0A1" : "#5B6473";

    public string RolBg => EsAdmin ? "#1F34E0A1" : "#14FFFFFF";
    public string RolColor => EsAdmin ? "#5CE8B4" : "#C2CCD6";

    public string EstadoTexto => Activo ? "Activo" : "Inactivo";
    public string EstadoDot => Activo ? "#34E0A1" : "#6B7684";
    public string EstadoColor => Activo ? "#C2CCD6" : "#8B96A4";

    /// <summary>Texto de la acción de alternar estado (para el tooltip del botón).</summary>
    public string AccionEstado => Activo ? "Desactivar" : "Activar";

    public string FechaTexto => FechaCreacion.ToString("dd/MM/yyyy HH:mm");
}
