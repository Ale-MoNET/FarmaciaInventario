using System;
using System.Collections.ObjectModel;
using System.Linq;
using FarmaGestion.Models;

namespace FarmaGestion.Services;

public interface IBarcodeService
{
    /// <summary>
    /// Busca un medicamento por código de barras.
    /// </summary>
    Medicamentos? BuscarPorCodigoBarras(string codigoBarras);

    /// <summary>
    /// Filtra medicamentos que coincidan con el término de búsqueda (código o nombre).
    /// </summary>
    System.Collections.Generic.List<Medicamentos> BuscarPorTermino(
        ObservableCollection<Medicamentos> inventario, string termino);
}

public class BarcodeService : IBarcodeService
{
    public Medicamentos? BuscarPorCodigoBarras(string codigoBarras)
    {
        if (string.IsNullOrWhiteSpace(codigoBarras))
            return null;

        var codigo = codigoBarras.Trim();
        return null;
    }

    public System.Collections.Generic.List<Medicamentos> BuscarPorTermino(
        ObservableCollection<Medicamentos> inventario, string termino)
    {
        if (string.IsNullOrWhiteSpace(termino))
            return new System.Collections.Generic.List<Medicamentos>();

        var terminoBusqueda = termino.Trim().ToUpperInvariant();

        var resultados = inventario
            .Where(m =>
                m.CodigoBarras?.ToUpperInvariant().Contains(terminoBusqueda) == true ||
                m.Nombre?.ToUpperInvariant().Contains(terminoBusqueda) == true)
            .ToList();

        return resultados;
    }
}
