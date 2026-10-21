using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using FarmaGestion.Data.Repositories;
using FarmaGestion.Services;
using Xunit;

namespace FarmaGestion.Tests;

public class ComprobantePdfTests
{
    private static ComprobanteDespacho Comprobante(int lineas = 3) =>
        new(12, new DateTime(2026, 8, 1, 14, 35, 0), 66m,
            Enumerable.Range(1, lineas)
                .Select(i => new LineaComprobante($"750100{i}", $"Producto {i} 500 mg", i, 12.5m))
                .ToList())
        { Atendio = "admin" };

    /// <summary>
    /// Recorre la tabla xref y comprueba que cada desplazamiento apunte realmente
    /// al objeto correspondiente: es lo que hace que un visor pueda abrir el PDF.
    /// </summary>
    private static void ValidarEstructura(byte[] pdf)
    {
        // Latin-1 mapea 1 byte = 1 carácter, así que las posiciones coinciden con los bytes.
        var texto = Encoding.Latin1.GetString(pdf);

        Assert.StartsWith("%PDF-1.4", texto);
        Assert.EndsWith("%%EOF\n", texto);
        Assert.Contains("/Type /Catalog", texto);

        int marca = texto.LastIndexOf("startxref", StringComparison.Ordinal);
        Assert.True(marca > 0, "El PDF debe declarar startxref.");

        int inicioXref = int.Parse(texto[(marca + "startxref".Length)..].Trim().Split('\n')[0]);
        Assert.Equal("xref", texto.Substring(inicioXref, 4));

        var renglones = texto[inicioXref..].Split('\n');
        int totalObjetos = int.Parse(renglones[1].Split(' ')[1]);

        for (int i = 1; i < totalObjetos; i++)
        {
            int offset = int.Parse(renglones[2 + i][..10]);
            Assert.StartsWith($"{i} 0 obj", texto[offset..]);
        }
    }

    [Fact]
    public void Generar_ProduceUnPdfConEstructuraValida()
    {
        var pdf = ComprobantePdf.Generar(Comprobante());

        ValidarEstructura(pdf);

        var texto = Encoding.Latin1.GetString(pdf);
        Assert.Contains("Producto 1 500 mg", texto);
        Assert.Contains("#00012", texto);
        Assert.Contains("admin", texto);
    }

    [Fact]
    public void Generar_ConMuchasLineas_ReparteEnVariasPaginas()
    {
        var pdf = ComprobantePdf.Generar(Comprobante(lineas: 90));

        ValidarEstructura(pdf);

        var texto = Encoding.Latin1.GetString(pdf);
        int paginas = int.Parse(texto.Split("/Count ")[1].Split(' ')[0].Split('>')[0].Trim());
        Assert.True(paginas > 1, $"90 líneas deberían ocupar más de una página (fueron {paginas}).");
        Assert.Contains("continuación", texto);
    }

    [Fact]
    public void Generar_ImprimeElLoteDeCadaLinea()
    {
        var comprobante = new ComprobanteDespacho(3, DateTime.Now, 10m, new[]
        {
            new LineaComprobante("001", "Paracetamol 500 mg", 2, 5m, "L-77A"),
            new LineaComprobante("002", "Producto sin lote (histórico)", 1, 0m)
        });

        var pdf = ComprobantePdf.Generar(comprobante);

        ValidarEstructura(pdf);
        var texto = Encoding.Latin1.GetString(pdf);
        Assert.Contains("Lote L-77A", texto);
        Assert.Contains("Paracetamol 500 mg", texto);
    }

    [Fact]
    public void Generar_SinLineas_NoFalla()
    {
        var comprobante = new ComprobanteDespacho(1, DateTime.Now, 0m, new List<LineaComprobante>());

        ValidarEstructura(ComprobantePdf.Generar(comprobante));
    }

    [Fact]
    public void Texto_ConCaracteresFueraDeLatin1_NoRompeElDocumento()
    {
        // Emojis y comillas tipográficas: deben sanearse, no colarse crudos al PDF.
        var comprobante = new ComprobanteDespacho(7, DateTime.Now, 1m, new[]
        {
            new LineaComprobante("✅001", "Jarabe “niños” — 120 ml 💊", 1, 1m)
        });

        var pdf = ComprobantePdf.Generar(comprobante);

        ValidarEstructura(pdf);
        var texto = Encoding.Latin1.GetString(pdf);
        Assert.Contains("Jarabe \"ni", texto);   // las comillas curvas se normalizan
        Assert.DoesNotContain("💊", texto);
    }

    [Fact]
    public void NombreArchivo_IncluyeNumeroYFecha()
    {
        var nombre = ComprobantePdf.NombreArchivo(Comprobante());

        Assert.Equal("despacho-00012-20260801-1435.pdf", nombre);
    }

    [Fact]
    public void AnchoTexto_CreceConElTamañoYRespetaElRecorte()
    {
        Assert.True(PdfDocumento.AnchoTexto("Paracetamol", 14) > PdfDocumento.AnchoTexto("Paracetamol", 10));

        var recortado = PdfDocumento.Recortar("Paracetamol 500 mg caja con 20 tabletas", 60, 10);

        Assert.EndsWith("...", recortado);
        Assert.True(PdfDocumento.AnchoTexto(recortado, 10) <= 60);
    }
}
