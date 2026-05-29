using Microsoft.EntityFrameworkCore;
using System;
using System.IO;
using FarmaGestion.Models;

namespace FarmaGestion.Data;

public class InventarioContext : DbContext
{
    public DbSet<Medicamentos> Medicamentos { get; set; }
    public DbSet<Despacho> Despachos { get; set; }
    public DbSet<DetalleDespacho> DetallesDespacho { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        // Para desarrollo en la Mac, crearemos el archivo .db directamente en la carpeta donde se ejecuta la app.
        // SQLite creará este archivo automáticamente si no existe.
        string dbPath = "inventario_medicamentos.db";
            
        optionsBuilder.UseSqlite($"Data Source={dbPath}");
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
    }   
}