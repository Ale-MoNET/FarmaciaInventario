using Microsoft.EntityFrameworkCore;
using System;
using System.IO;
using FarmaGestion.Models;
using FarmaGestion.Services;

namespace FarmaGestion.Data;

public class InventarioContext : DbContext
{
    public DbSet<Medicamentos> Medicamentos { get; set; }
    public DbSet<Despacho> Despachos { get; set; }
    public DbSet<DetalleDespacho> DetallesDespacho { get; set; }
    public DbSet<Usuario> Usuarios { get; set; }

    /// <summary>Constructor por defecto (usado por las herramientas de EF y como respaldo).</summary>
    public InventarioContext()
    {
    }

    /// <summary>
    /// Constructor con opciones inyectadas. Lo usan el <c>IDbContextFactory</c> registrado
    /// en el contenedor de DI y las pruebas (que inyectan SQLite en memoria).
    /// </summary>
    public InventarioContext(DbContextOptions<InventarioContext> options) : base(options)
    {
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        // Solo configuramos aquí si nadie inyectó opciones (p. ej. al usar el constructor
        // por defecto o las herramientas de EF). La base de datos vive en la carpeta de
        // datos del usuario, no en el directorio de trabajo.
        if (!optionsBuilder.IsConfigured)
        {
            optionsBuilder.UseSqlite($"Data Source={AppPaths.RutaBaseDatos}");
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Fluent API: Reglas adicionales de la base de datos
            
        // 1. Aseguramos que el Código de Barras sea único (no queremos dos medicamentos distintos con el mismo código)
        modelBuilder.Entity<Medicamentos>()
            .HasIndex(m => m.CodigoBarras)
            .IsUnique();

        // 2. EF Core + SQLite a veces dan advertencias con los tipos 'decimal'. 
        // Esta configuración le dice a SQLite cómo almacenarlos internamente sin perder precisión.
        modelBuilder.Entity<Medicamentos>()
            .Property(m => m.Precio)
            .HasConversion<double>();

        modelBuilder.Entity<DetalleDespacho>()
            .Property(d => d.PrecioHistorico)
            .HasConversion<double>();

        modelBuilder.Entity<Despacho>()
            .Property(d => d.Total)
            .HasConversion<double>();

        // 3. Aseguramos que el nombre de usuario sea único
        modelBuilder.Entity<Usuario>()
            .HasIndex(u => u.NombreUsuario)
            .IsUnique();
    }   
}