using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FarmaGestion.Models;
using FarmaGestion.Services;
using Microsoft.EntityFrameworkCore;

namespace FarmaGestion.Data.Repositories;

public class MedicamentoRepository : IMedicamentoRepository
{
    private readonly IDbContextFactory<InventarioContext> _factory;

    public MedicamentoRepository(IDbContextFactory<InventarioContext> factory)
    {
        _factory = factory;
    }

    private static IQueryable<Medicamentos> ConLotes(InventarioContext context)
        => context.Medicamentos.Include(m => m.Lotes);

    public async Task<List<Medicamentos>> ObtenerTodosAsync()
    {
        await using var context = await _factory.CreateDbContextAsync();
        return await ConLotes(context).AsNoTracking().OrderBy(m => m.Nombre).ToListAsync();
    }

    public async Task<Medicamentos?> ObtenerPorCodigoAsync(string codigoBarras)
    {
        await using var context = await _factory.CreateDbContextAsync();
        return await ConLotes(context)
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.CodigoBarras == codigoBarras);
    }

    public async Task<Medicamentos?> ObtenerPorIdAsync(int id)
    {
        await using var context = await _factory.CreateDbContextAsync();
        return await ConLotes(context).AsNoTracking().FirstOrDefaultAsync(m => m.Id == id);
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
        var ahora = DateTime.Now;

        medicamento.FechaUltimaActualizacion = ahora;
        foreach (var lote in medicamento.Lotes)
        {
            lote.NumeroLote = NormalizarLote(lote.NumeroLote);
            lote.FechaVencimiento = lote.FechaVencimiento?.Date;
            lote.FechaIngreso = ahora;
            lote.FechaUltimaActualizacion = ahora;
        }

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
        db.FechaUltimaActualizacion = DateTime.Now;

        await context.SaveChangesAsync();
    }

    public async Task<ResultadoIngreso> IngresarLoteAsync(int medicamentoId, string numeroLote, DateTime fechaVencimiento, int cantidad)
    {
        if (cantidad <= 0)
            return new ResultadoIngreso(false, "La cantidad debe ser mayor que cero.");

        await using var context = await _factory.CreateDbContextAsync();
        var producto = await ConLotes(context).FirstOrDefaultAsync(m => m.Id == medicamentoId);
        if (producto == null)
            return new ResultadoIngreso(false, "El producto ya no existe en el inventario.");

        var (nuevo, error, lote) = AplicarIngreso(producto, numeroLote, fechaVencimiento, cantidad, DateTime.Now);
        if (error != null)
            return new ResultadoIngreso(false, error);

        await context.SaveChangesAsync();

        LoggerService.Info($"Ingreso de {cantidad} u al lote {lote!.NumeroLote} de «{producto.Nombre}» (nuevo: {nuevo}).");
        var mensaje = nuevo
            ? $"Lote {lote.NumeroLote} creado con {cantidad} u de «{producto.Nombre}»."
            : $"{cantidad} u sumadas al lote {lote.NumeroLote} de «{producto.Nombre}» (ahora {lote.Existencias} u).";
        return new ResultadoIngreso(true, mensaje, nuevo);
    }

    public async Task<bool> DarDeBajaLoteAsync(int loteId)
    {
        await using var context = await _factory.CreateDbContextAsync();
        var lote = await context.Lotes.Include(l => l.Medicamento).FirstOrDefaultAsync(l => l.Id == loteId);
        if (lote == null) return false;

        var ahora = DateTime.Now;
        int retiradas = lote.Existencias;
        lote.Existencias = 0;
        lote.FechaUltimaActualizacion = ahora;
        lote.Medicamento.FechaUltimaActualizacion = ahora;

        await context.SaveChangesAsync();
        LoggerService.Info($"Baja del lote {lote.NumeroLote} de «{lote.Medicamento.Nombre}»: {retiradas} u retiradas.");
        return true;
    }

    public async Task<bool> EliminarAsync(int id)
    {
        await using var context = await _factory.CreateDbContextAsync();

        // No permitir borrar si el producto ya tiene despachos (rompería el historial).
        bool tieneMovimientos = await context.DetallesDespacho.AnyAsync(d => d.MedicamentoId == id);
        if (tieneMovimientos) return false;

        var db = await context.Medicamentos.FindAsync(id);
        if (db == null) return false;

        context.Medicamentos.Remove(db); // los lotes se eliminan en cascada
        await context.SaveChangesAsync();
        return true;
    }

    public async Task<ResultadoImportacion> ImportarRegistrosAsync(IEnumerable<RegistroMedicamentoCsv> registros)
    {
        var filas = registros.ToList();
        var rechazadas = new List<string>();
        if (filas.Count == 0)
            return new ResultadoImportacion(0, rechazadas);

        await using var context = await _factory.CreateDbContextAsync();

        // Diccionario en memoria para que varias filas del mismo producto (varios lotes)
        // trabajen sobre la misma instancia, aunque el producto se haya creado en este lote.
        var porCodigo = new Dictionary<string, Medicamentos>(StringComparer.Ordinal);
        foreach (var m in await ConLotes(context).ToListAsync())
            porCodigo[m.CodigoBarras] = m;

        var ahora = DateTime.Now;
        int procesadas = 0;

        foreach (var r in filas)
        {
            var codigo = r.CodigoBarras.Trim();
            string prefijo = r.Fila > 0 ? $"Fila {r.Fila}: " : string.Empty;

            if (!porCodigo.TryGetValue(codigo, out var producto))
            {
                producto = new Medicamentos
                {
                    CodigoBarras = codigo,
                    Nombre = r.Nombre.Trim(),
                    Precio = r.Precio,
                    FechaUltimaActualizacion = ahora
                };
                context.Medicamentos.Add(producto);
                porCodigo[codigo] = producto;
            }
            else
            {
                // Si ya existe, actualizamos el precio con el más reciente.
                producto.Precio = r.Precio;
            }

            var (_, error, _) = AplicarIngreso(producto, r.Lote, r.FechaVencimiento, r.Cantidad, ahora);
            if (error != null)
            {
                rechazadas.Add(prefijo + error);
                continue;
            }
            procesadas++;
        }

        await context.SaveChangesAsync();
        return new ResultadoImportacion(procesadas, rechazadas);
    }

    /// <summary>Número de lote tal como se guarda: sin espacios alrededor y en mayúsculas.</summary>
    public static string NormalizarLote(string? numeroLote)
        => (numeroLote ?? string.Empty).Trim().ToUpperInvariant();

    /// <summary>
    /// Suma unidades al lote indicado del producto (o lo crea). Trabaja sobre la instancia
    /// ya cargada con sus lotes; el llamador guarda los cambios.
    /// </summary>
    private static (bool Nuevo, string? Error, Lote? Lote) AplicarIngreso(
        Medicamentos producto, string numeroLote, DateTime fechaVencimiento, int cantidad, DateTime ahora)
    {
        var numero = NormalizarLote(numeroLote);
        if (numero.Length == 0)
            return (false, "El número de lote es obligatorio.", null);
        if (cantidad <= 0)
            return (false, "La cantidad debe ser mayor que cero.", null);

        var fecha = fechaVencimiento.Date;
        var existente = producto.Lotes.FirstOrDefault(l => l.NumeroLote == numero);

        if (existente != null)
        {
            if (existente.FechaVencimiento.HasValue && existente.FechaVencimiento.Value.Date != fecha)
            {
                return (false,
                    $"El lote {numero} de «{producto.Nombre}» ya existe con vencimiento " +
                    $"{existente.FechaVencimiento.Value:dd/MM/yyyy}; verifica la fecha.", null);
            }

            existente.FechaVencimiento ??= fecha;
            existente.Existencias += cantidad;
            existente.FechaUltimaActualizacion = ahora;
            producto.FechaUltimaActualizacion = ahora;
            return (false, null, existente);
        }

        var nuevo = new Lote
        {
            NumeroLote = numero,
            FechaVencimiento = fecha,
            Existencias = cantidad,
            FechaIngreso = ahora,
            FechaUltimaActualizacion = ahora
        };
        producto.Lotes.Add(nuevo);
        producto.FechaUltimaActualizacion = ahora;
        return (true, null, nuevo);
    }
}
