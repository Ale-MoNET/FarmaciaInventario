using System.Collections.Generic;
using System.Threading.Tasks;
using FarmaGestion.Models;

namespace FarmaGestion.Data.Repositories;

/// <summary>Registro plano para importar medicamentos desde un CSV.</summary>
public record RegistroMedicamentoCsv(
    string CodigoBarras,
    string Nombre,
    decimal Precio,
    int Cantidad,
    string? Lote,
    System.DateTime? FechaVencimiento);

public interface IMedicamentoRepository
{
    Task<List<Medicamentos>> ObtenerTodosAsync();
    Task<Medicamentos?> ObtenerPorCodigoAsync(string codigoBarras);
    Task<Medicamentos?> ObtenerPorIdAsync(int id);

    /// <summary>Indica si ya existe el código; opcionalmente excluye un id (para edición).</summary>
    Task<bool> ExisteCodigoAsync(string codigoBarras, int? excluirId = null);

    Task AgregarAsync(Medicamentos medicamento);
    Task ActualizarAsync(Medicamentos medicamento);

    /// <summary>
    /// Elimina un medicamento. Devuelve false si no se puede porque ya tiene
    /// despachos asociados (para no romper el historial de ventas).
    /// </summary>
    Task<bool> EliminarAsync(int id);

    /// <summary>
    /// Inserta o actualiza (sumando existencias) los registros del CSV.
    /// Devuelve cuántas filas se procesaron.
    /// </summary>
    Task<int> ImportarRegistrosAsync(IEnumerable<RegistroMedicamentoCsv> registros);
}
