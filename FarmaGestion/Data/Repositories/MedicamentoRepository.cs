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

    public async Task<ResultadoOperacion> DarDeBajaLoteAsync(int loteId, int cantidad, string motivo, string? observacion, string usuario)
    {
        if (cantidad <= 0)
            return new ResultadoOperacion(false, "La cantidad a retirar debe ser mayor que cero.");
        if (string.IsNullOrWhiteSpace(motivo))
            return new ResultadoOperacion(false, "Indica el motivo de la baja.");

        await using var context = await _factory.CreateDbContextAsync();
        var lote = await context.Lotes.Include(l => l.Medicamento).FirstOrDefaultAsync(l => l.Id == loteId);
        if (lote == null)
            return new ResultadoOperacion(false, "El lote ya no existe.");
        if (cantidad > lote.Existencias)
            return new ResultadoOperacion(false, $"El lote {lote.NumeroLote} solo tiene {lote.Existencias} u; no se pueden retirar {cantidad}.");

        var ahora = DateTime.Now;
        lote.Existencias -= cantidad;
        lote.FechaUltimaActualizacion = ahora;
        lote.Medicamento.FechaUltimaActualizacion = ahora;

        context.BajasLote.Add(new BajaLote
        {
            LoteId = lote.Id,
            MedicamentoId = lote.MedicamentoId,
            Cantidad = cantidad,
            Motivo = motivo.Trim(),
            Observacion = string.IsNullOrWhiteSpace(observacion) ? null : observacion.Trim(),
            Fecha = ahora,
            Usuario = usuario.Trim()
        });

        await context.SaveChangesAsync();
        LoggerService.Info($"Baja de {cantidad} u del lote {lote.NumeroLote} de «{lote.Medicamento.Nombre}» ({motivo}) por {usuario}.");
        return new ResultadoOperacion(true,
            $"Se retiraron {cantidad} u del lote {lote.NumeroLote} de «{lote.Medicamento.Nombre}» ({motivo.Trim()}).");
    }

    public async Task<ResultadoOperacion> ActualizarLoteAsync(int loteId, string numeroLote, DateTime fechaVencimiento)
    {
        var numero = NormalizarLote(numeroLote);
        if (numero.Length == 0)
            return new ResultadoOperacion(false, "El número de lote es obligatorio.");

        await using var context = await _factory.CreateDbContextAsync();
        var lote = await context.Lotes.Include(l => l.Medicamento).FirstOrDefaultAsync(l => l.Id == loteId);
        if (lote == null)
            return new ResultadoOperacion(false, "El lote ya no existe.");

        bool duplicado = await context.Lotes.AnyAsync(l =>
            l.MedicamentoId == lote.MedicamentoId && l.Id != loteId && l.NumeroLote == numero);
        if (duplicado)
        {
            return new ResultadoOperacion(false,
                $"«{lote.Medicamento.Nombre}» ya tiene otro lote {numero}. Si es el mismo, súmale las unidades con " +
                "«Ingresar mercadería» y da de baja este.");
        }

        var ahora = DateTime.Now;
        lote.NumeroLote = numero;
        lote.FechaVencimiento = fechaVencimiento.Date;
        lote.FechaUltimaActualizacion = ahora;
        lote.Medicamento.FechaUltimaActualizacion = ahora;

        await context.SaveChangesAsync();
        LoggerService.Info($"Lote corregido: «{lote.Medicamento.Nombre}» → {numero}, vence {fechaVencimiento:dd/MM/yyyy}.");
        return new ResultadoOperacion(true, $"Lote actualizado: {numero}, vence {fechaVencimiento:dd/MM/yyyy}.");
    }

    public async Task<List<Lote>> ObtenerLotesConExistenciasAsync()
    {
        await using var context = await _factory.CreateDbContextAsync();
        return await context.Lotes
            .AsNoTracking()
            .Include(l => l.Medicamento)
            .Where(l => l.Existencias > 0)
            .OrderBy(l => l.FechaVencimiento == null)
            .ThenBy(l => l.FechaVencimiento)
            .ThenBy(l => l.Medicamento.Nombre)
            .ToListAsync();
    }

    public async Task<List<BajaLote>> ObtenerBajasRecientesAsync(int maximo = 50)
    {
        await using var context = await _factory.CreateDbContextAsync();
        return await context.BajasLote
            .AsNoTracking()
            .Include(b => b.Lote)
            .Include(b => b.Medicamento)
            .OrderByDescending(b => b.Fecha)
            .ThenByDescending(b => b.Id)
            .Take(maximo)
            .ToListAsync();
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
