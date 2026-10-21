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

/// <summary>Resultado genérico de una operación sobre lotes (baja, edición).</summary>
public record ResultadoOperacion(bool Exito, string Mensaje);

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

    /// <summary>
    /// Retira unidades de un lote (vencido, dañado, pérdida…) dejando registro de la baja con su
    /// motivo y quién la hizo. La cantidad debe ser positiva y no mayor que las existencias del lote.
    /// </summary>
    Task<ResultadoOperacion> DarDeBajaLoteAsync(int loteId, int cantidad, string motivo, string? observacion, string usuario);

    /// <summary>Corrige el número o la fecha de vencimiento de un lote (no toca existencias).</summary>
    Task<ResultadoOperacion> ActualizarLoteAsync(int loteId, string numeroLote, DateTime fechaVencimiento);

    /// <summary>Lotes con existencias, con su producto, para el reporte de vencimientos.</summary>
    Task<List<Lote>> ObtenerLotesConExistenciasAsync();

    /// <summary>Últimas bajas registradas (más recientes primero), con lote y producto.</summary>
    Task<List<BajaLote>> ObtenerBajasRecientesAsync(int maximo = 50);

    /// <summary>
    /// Elimina un medicamento con todos sus lotes. Devuelve false si no se puede porque
    /// ya tiene despachos asociados (para no romper el historial de ventas).
    /// </summary>
    Task<bool> EliminarAsync(int id);

    /// <summary>Aplica las filas del CSV como ingresos de lote (crea productos y lotes según haga falta).</summary>
    Task<ResultadoImportacion> ImportarRegistrosAsync(IEnumerable<RegistroMedicamentoCsv> registros);
}
