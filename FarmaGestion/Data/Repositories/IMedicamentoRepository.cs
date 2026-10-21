using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FarmaGestion.Models;

namespace FarmaGestion.Data.Repositories;

/// <summary>
/// Una fila del CSV de importación. Cada fila es un ingreso de lote: si el producto no
/// existe se crea; si el lote ya existe se le suman las unidades.
/// </summary>
public record RegistroMedicamentoCsv(
    string CodigoBarras,
    string Nombre,
    decimal Precio,
    int Cantidad,
    string Lote,
    DateTime FechaVencimiento,
    int Fila = 0);

/// <summary>Resultado de registrar un ingreso de mercadería en un lote.</summary>
public record ResultadoIngreso(bool Exito, string Mensaje, bool LoteNuevo = false);

/// <summary>Resultado de una importación: filas aplicadas y motivos de las rechazadas.</summary>
public record ResultadoImportacion(int Procesadas, IReadOnlyList<string> Rechazadas);

public interface IMedicamentoRepository
{
    /// <summary>Todos los productos con sus lotes, ordenados por nombre.</summary>
    Task<List<Medicamentos>> ObtenerTodosAsync();
    Task<Medicamentos?> ObtenerPorCodigoAsync(string codigoBarras);
    Task<Medicamentos?> ObtenerPorIdAsync(int id);

    /// <summary>Indica si ya existe el código; opcionalmente excluye un id (para edición).</summary>
    Task<bool> ExisteCodigoAsync(string codigoBarras, int? excluirId = null);

    /// <summary>Registra un producto nuevo; puede traer su primer lote en <see cref="Medicamentos.Lotes"/>.</summary>
    Task AgregarAsync(Medicamentos medicamento);

    /// <summary>Actualiza solo los datos maestros (código, nombre y precio). Las existencias se mueven por lote.</summary>
    Task ActualizarAsync(Medicamentos medicamento);

    /// <summary>
    /// Suma unidades a un lote del producto; si el lote no existe lo crea. Falla si el lote
    /// ya existe con otra fecha de vencimiento o si la cantidad no es positiva.
    /// </summary>
    Task<ResultadoIngreso> IngresarLoteAsync(int medicamentoId, string numeroLote, DateTime fechaVencimiento, int cantidad);

    /// <summary>Deja el lote en cero existencias (vencido, dañado, etc.). Devuelve false si no existe.</summary>
    Task<bool> DarDeBajaLoteAsync(int loteId);

    /// <summary>
    /// Elimina un medicamento con todos sus lotes. Devuelve false si no se puede porque
    /// ya tiene despachos asociados (para no romper el historial de ventas).
    /// </summary>
    Task<bool> EliminarAsync(int id);

    /// <summary>Aplica las filas del CSV como ingresos de lote (crea productos y lotes según haga falta).</summary>
    Task<ResultadoImportacion> ImportarRegistrosAsync(IEnumerable<RegistroMedicamentoCsv> registros);
}
