using System;
using System.Collections.Generic;
using System.Linq;
using FarmaGestion.Models;
using FarmaGestion.Services;
using Xunit;

namespace FarmaGestion.Tests;

public class ReporteVencimientosTests
{
    private static readonly DateTime Hoy = new(2026, 10, 21);

    private static Lote L(string numero, DateTime? vence, int existencias, string producto = "P") => new()
    {
        NumeroLote = numero, FechaVencimiento = vence, Existencias = existencias,
        Medicamento = new Medicamentos { Nombre = producto }
    };

    private static List<Lote> Muestra() => new()
    {
        L("VENCIDO", Hoy.AddDays(-3), 4),
        L("HOY", Hoy, 1),
        L("D20", Hoy.AddDays(20), 2),
        L("D45", Hoy.AddDays(45), 3),
        L("D80", Hoy.AddDays(80), 5),
        L("D200", Hoy.AddDays(200), 6),
        L("SINFECHA", null, 7),
        L("AGOTADO", Hoy.AddDays(1), 0),      // sin existencias: nunca cuenta
    };

    [Fact]
    public void Contar_AcumulaPorRangos()
    {
        var (vencidos, en30, en60, en90) = ReporteVencimientos.Contar(Muestra(), Hoy);

        Assert.Equal(1, vencidos);
        Assert.Equal(2, en30);   // HOY, D20
        Assert.Equal(3, en60);   // + D45
        Assert.Equal(4, en90);   // + D80
    }

    [Fact]
    public void Filtrar_PorHorizonte()
    {
        var lotes = Muestra();

        Assert.Equal(new[] { "VENCIDO" }, ReporteVencimientos.Filtrar(lotes, ReporteVencimientos.Vencidos, Hoy).Select(l => l.NumeroLote));
        Assert.Equal(new[] { "HOY", "D20" }, ReporteVencimientos.Filtrar(lotes, 30, Hoy).Select(l => l.NumeroLote));
        Assert.Equal(new[] { "HOY", "D20", "D45", "D80" }, ReporteVencimientos.Filtrar(lotes, 90, Hoy).Select(l => l.NumeroLote));

        var todos = ReporteVencimientos.Filtrar(lotes, ReporteVencimientos.Todos, Hoy).Select(l => l.NumeroLote).ToList();
        Assert.Equal(7, todos.Count);                          // el agotado queda fuera
        Assert.Equal("VENCIDO", todos.First());                // ordenado por fecha
        Assert.Equal("SINFECHA", todos.Last());                // sin fecha al final
    }

    [Fact]
    public void TextoDias_DescribeElPlazo()
    {
        Assert.Equal("Hace 3 días", ReporteVencimientos.TextoDias(L("a", Hoy.AddDays(-3), 1), Hoy));
        Assert.Equal("Hace 1 día", ReporteVencimientos.TextoDias(L("a", Hoy.AddDays(-1), 1), Hoy));
        Assert.Equal("Hoy", ReporteVencimientos.TextoDias(L("a", Hoy, 1), Hoy));
        Assert.Equal("1 día", ReporteVencimientos.TextoDias(L("a", Hoy.AddDays(1), 1), Hoy));
        Assert.Equal("20 días", ReporteVencimientos.TextoDias(L("a", Hoy.AddDays(20), 1), Hoy));
        Assert.Equal("Sin fecha", ReporteVencimientos.TextoDias(L("a", null, 1), Hoy));
    }
}
