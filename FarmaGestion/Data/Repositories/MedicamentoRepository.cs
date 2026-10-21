using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FarmaGestion.Models;
using Microsoft.EntityFrameworkCore;

namespace FarmaGestion.Data.Repositories;

public class MedicamentoRepository : IMedicamentoRepository
{
    private readonly IDbContextFactory<InventarioContext> _factory;

    public MedicamentoRepository(IDbContextFactory<InventarioContext> factory)
    {
        _factory = factory;
    }

    public async Task<List<Medicamentos>> ObtenerTodosAsync()
    {
        await using var context = await _factory.CreateDbContextAsync();
        return await context.Medicamentos.AsNoTracking().ToListAsync();
    }

    public async Task<Medicamentos?> ObtenerPorCodigoAsync(string codigoBarras)
    {
        await using var context = await _factory.CreateDbContextAsync();
        return await context.Medicamentos
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.CodigoBarras == codigoBarras);
    }

    public async Task<Medicamentos?> ObtenerPorIdAsync(int id)
    {
        await using var context = await _factory.CreateDbContextAsync();
        return await context.Medicamentos.AsNoTracking().FirstOrDefaultAsync(m => m.Id == id);
    }

    public async Task<bool> ExisteCodigoAsync(string codigoBarras, int? excluirId = null)
    {
        await using var context = await _factory.CreateDbContextAsync();
        return await context.Medicamentos
            .AnyAsync(m => m.CodigoBarras == codigoBarras && (excluirId == null || m.Id != excluirId));
    }

    public async Task AgregarAsync(Medicamentos medicamento)
    {
        await using var context = await _factory.CreateDbContextAsync();
        context.Medicamentos.Add(medicamento);
        await context.SaveChangesAsync();
    }

    public async Task ActualizarAsync(Medicamentos medicamento)
    {
        await using var context = await _factory.CreateDbContextAsync();
        var db = await context.Medicamentos.FindAsync(medicamento.Id);
        if (db == null) return;

        db.CodigoBarras = medicamento.CodigoBarras;
        db.Nombre = medicamento.Nombre;
        db.Precio = medicamento.Precio;
        db.Existencias = medicamento.Existencias;
        db.Lote = medicamento.Lote;
        db.FechaVencimiento = medicamento.FechaVencimiento;
        db.FechaUltimaActualizacion = DateTime.Now;

        await context.SaveChangesAsync();
    }

    public async Task<bool> EliminarAsync(int id)
    {
        await using var context = await _factory.CreateDbContextAsync();

        // No permitir borrar si el producto ya tiene despachos (rompería el historial).
        bool tieneMovimientos = await context.DetallesDespacho.AnyAsync(d => d.MedicamentoId == id);
        if (tieneMovimientos) return false;

        var db = await context.Medicamentos.FindAsync(id);
        if (db == null) return false;

        context.Medicamentos.Remove(db);
        await context.SaveChangesAsync();
        return true;
    }

    public async Task<int> ImportarRegistrosAsync(IEnumerable<RegistroMedicamentoCsv> registros)
    {
        await using var context = await _factory.CreateDbContextAsync();
        int procesados = 0;

        foreach (var r in registros)
        {
            var existente = await context.Medicamentos
                .FirstOrDefaultAsync(m => m.CodigoBarras == r.CodigoBarras);

            if (existente != null)
            {
                // Si ya existe, sumamos existencias y actualizamos datos recientes.
                existente.Existencias += r.Cantidad;
                existente.Precio = r.Precio;
                existente.FechaUltimaActualizacion = DateTime.Now;
                if (r.Lote != null) existente.Lote = r.Lote;
                if (r.FechaVencimiento != null) existente.FechaVencimiento = r.FechaVencimiento;
            }
            else
            {
                context.Medicamentos.Add(new Medicamentos
                {
                    CodigoBarras = r.CodigoBarras,
                    Nombre = r.Nombre,
                    Precio = r.Precio,
                    Existencias = r.Cantidad,
                    FechaUltimaActualizacion = DateTime.Now,
                    Lote = r.Lote,
                    FechaVencimiento = r.FechaVencimiento
                });
            }
            procesados++;
        }

        await context.SaveChangesAsync();
        return procesados;
    }
}
