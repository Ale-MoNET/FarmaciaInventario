using System;
using System.ComponentModel.DataAnnotations;

namespace FarmaGestion.Models;

public class Usuario
{
    public int Id { get; set; }

    [Required]
    [StringLength(50, MinimumLength = 3)]
    public string NombreUsuario { get; set; } = string.Empty;

    public string PasswordHash { get; set; } = string.Empty;
    public string PasswordSalt { get; set; } = string.Empty;

    [Required]
    public string Rol { get; set; } = Models.Rol.Empleado; // Ver Models/Rol.cs

    public bool Activo { get; set; } = true;
    public DateTime FechaCreacion { get; set; } = DateTime.Now;
}
