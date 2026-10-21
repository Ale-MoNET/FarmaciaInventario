using System;
using System.Collections.Generic;
using System.Linq;
using FarmaGestion.Models;
using FarmaGestion.Services;
using Xunit;

namespace FarmaGestion.Tests;

public class AsignadorLotesTests
{
    private static readonly DateTime Hoy = new(2026, 10, 21);

    private static Lote L(int id, string numero, DateTime? vence, int existencias) => new()
    {
        Id = id, NumeroLote = numero, FechaVencimiento = vence, Existencias = existencias
    };

    [Fact]
    public void OrdenarFefo_VigentesPorFecha_LuegoSinFecha_LuegoVencidos()
    {
        var lotes = new List<Lote>
        {
            L(1, "VENCIDO", Hoy.AddDays(-1), 5),
            L(2, "LEJANO", Hoy.AddDays(300), 5),
            L(3, "SINFECHA", null, 5),
            L(4, "CERCANO", Hoy.AddDays(10), 5),
            L(5, "HOY", Hoy, 5),           // vence hoy: todavía vigente
        };

        var orden = AsignadorLotes.OrdenarFefo(lotes, Hoy).Select(l => l.NumeroLote).ToList();

        Assert.Equal(new[] { "HOY", "CERCANO", "LEJANO", "SINFECHA", "VENCIDO" }, orden);
    }

    [Fact]
    public void Asignar_RepartaEntreLotesEnOrdenFefo()
    {
        var lotes = new List<Lote>
        {
            L(1, "B", Hoy.AddDays(200), 10),
            L(2, "A", Hoy.AddDays(30), 3),
        };

        var r = AsignadorLotes.Asignar(lotes, 5, Hoy);

        Assert.True(r.Completa);
        Assert.Equal(2, r.Asignaciones.Count);
        Assert.Equal(("A", 3), (r.Asignaciones[0].Lote.NumeroLote, r.Asignaciones[0].Cantidad));
        Assert.Equal(("B", 2), (r.Asignaciones[1].Lote.NumeroLote, r.Asignaciones[1].Cantidad));
        Assert.Equal(0, r.UnidadesVencidas);
        Assert.Equal("A (3) + B (2)", r.Descripcion);
    }

    [Fact]
    public void Asignar_SinStockSuficiente_ReportaFaltante()
    {
        var lotes = new List<Lote> { L(1, "A", Hoy.AddDays(30), 2) };

        var r = AsignadorLotes.Asignar(lotes, 5, Hoy);

        Assert.False(r.Completa);
        Assert.Equal(3, r.Faltante);
        Assert.Single(r.Asignaciones);
    }

    [Fact]
    public void Asignar_IgnoraLotesEnCero()
    {
        var lotes = new List<Lote>
        {
            L(1, "VACIO", Hoy.AddDays(1), 0),
            L(2, "LLENO", Hoy.AddDays(60), 4),
        };

        var r = AsignadorLotes.Asignar(lotes, 2, Hoy);

        Assert.Equal("LLENO", Assert.Single(r.Asignaciones).Lote.NumeroLote);
    }

    [Fact]
    public void Asignar_UsaVencidosSoloCuandoNoQuedaOtraCosa_YLosCuenta()
    {
        var lotes = new List<Lote>
        {
            L(1, "VENCIDO", Hoy.AddDays(-10), 5),
            L(2, "VIGENTE", Hoy.AddDays(90), 2),
        };

        var r = AsignadorLotes.Asignar(lotes, 4, Hoy);

        Assert.True(r.Completa);
        Assert.Equal("VIGENTE", r.Asignaciones[0].Lote.NumeroLote);
        Assert.Equal(2, r.UnidadesVencidas);
        Assert.Equal(new[] { "VENCIDO" }, r.LotesVencidos.ToArray());
    }

    [Fact]
    public void Lote_CalculaEstadoSegunDias()
    {
        Assert.Equal(EstadoLote.Vencido, L(1, "X", Hoy.AddDays(-1), 1).CalcularEstado(Hoy));
        Assert.Equal(EstadoLote.PorVencer, L(1, "X", Hoy, 1).CalcularEstado(Hoy));
        Assert.Equal(EstadoLote.PorVencer, L(1, "X", Hoy.AddDays(Lote.DiasPorVencer), 1).CalcularEstado(Hoy));
        Assert.Equal(EstadoLote.Vigente, L(1, "X", Hoy.AddDays(Lote.DiasPorVencer + 1), 1).CalcularEstado(Hoy));
        Assert.Equal(EstadoLote.SinFecha, L(1, "X", null, 1).CalcularEstado(Hoy));
    }

    [Fact]
    public void Medicamento_CalculaTotalesDesdeLotes()
    {
        var m = new Medicamentos
        {
            Nombre = "Prueba",
            Lotes =
            {
                L(1, "A", DateTime.Today.AddDays(-5), 3),    // vencido
                L(2, "B", DateTime.Today.AddDays(400), 10),
                L(3, "C", DateTime.Today.AddDays(20), 0),    // agotado: no cuenta como lote activo
            }
        };

        Assert.Equal(13, m.Existencias);
        Assert.Equal(3, m.ExistenciasVencidas);
        Assert.Equal(2, m.NumeroLotes);
        Assert.Equal("A", m.ProximoLote!.NumeroLote);
        Assert.Equal(EstadoLote.Vencido, m.EstadoProximo);
    }
}
