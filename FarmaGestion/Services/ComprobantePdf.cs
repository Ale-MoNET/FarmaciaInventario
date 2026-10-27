using System;
using System.Globalization;
using FarmaGestion.Data.Repositories;

namespace FarmaGestion.Services;

/// <summary>
/// Arma el PDF del comprobante de despacho (tamaño carta, con paginación
/// automática cuando el detalle no cabe en una sola hoja).
/// </summary>
public static class ComprobantePdf
{
    // Paleta pensada para papel: fondo blanco y acento verde oscuro legible al imprimir.
    private const string ColorTexto = "#151A21";
    private const string ColorSuave = "#4A5560";
    private const string ColorTenue = "#8A94A0";
    private const string ColorAcento = "#0E8F63";
    private const string ColorLinea = "#DCE2E9";
    private const string ColorFondoFila = "#F2F5F8";
    private const string ColorAnulado = "#C0392B";

    private const double Margen = 54;
    private const double Derecha = 558;   // 612 - margen
    private const double LimiteInferior = 706;
    private const double AltoFila = 20;

    // Columnas de la tabla de detalle.
    private const double XCodigo = Margen;
    private const double AnchoCodigo = 92;
    private const double XProducto = 156;
    private const double AnchoProducto = 230;
    private const double XCantidad = 424;   // alineada a la derecha
    private const double XPrecio = 490;     // alineada a la derecha
    private const double XSubtotal = Derecha;

    /// <summary>Genera el documento completo y devuelve los bytes del PDF.</summary>
    public static byte[] Generar(ComprobanteDespacho comprobante)
    {
        var doc = new PdfDocumento();

        double y = DibujarEncabezado(doc, comprobante);
        y = DibujarCabeceraTabla(doc, y);

        bool alterna = false;
        foreach (var linea in comprobante.Lineas)
        {
            if (y + AltoFila > LimiteInferior)
            {
                doc.NuevaPagina();
                y = DibujarEncabezadoContinuacion(doc, comprobante);
                y = DibujarCabeceraTabla(doc, y);
                alterna = false;
            }

            DibujarFila(doc, linea, y, alterna);
            alterna = !alterna;
            y += AltoFila;
        }

        const double AltoBloqueTotales = 96;
        if (y + AltoBloqueTotales > LimiteInferior)
        {
            doc.NuevaPagina();
            y = DibujarEncabezadoContinuacion(doc, comprobante);
        }

        DibujarTotales(doc, comprobante, y);
        DibujarPies(doc, comprobante);

        return doc.Generar();
    }

    /// <summary>
    /// Nombre de archivo sugerido al descargar o imprimir el comprobante. Se arma con
    /// cultura invariante para que el nombre sea siempre el mismo en cualquier equipo.
    /// </summary>
    public static string NombreArchivo(ComprobanteDespacho comprobante) =>
        string.Format(CultureInfo.InvariantCulture, "despacho-{0:D5}-{1:yyyyMMdd-HHmm}.pdf",
            comprobante.Id, comprobante.Fecha);

    // ── Secciones del documento ────────────────────────────────────────────

    private static double DibujarEncabezado(PdfDocumento doc, ComprobanteDespacho c)
    {
        doc.Rectangulo(0, 0, doc.Ancho, 6, ColorAcento);

        doc.Texto("FarmaGestión", Margen, 46, 22, negrita: true, ColorTexto);
        doc.Texto("Comprobante de despacho", Margen, 74, 11, negrita: false, ColorSuave);

        doc.TextoDerecha("COMPROBANTE No.", Derecha, 46, 8.5, negrita: true, ColorTenue);
        doc.TextoDerecha($"#{c.Id:D5}", Derecha, 58, 22, negrita: true, ColorAcento);

        if (c.Anulado)
        {
            // Sello de anulación: el documento sigue siendo válido como registro, no como venta.
            doc.TextoDerecha("ANULADO", Derecha, 88, 11, negrita: true, ColorAnulado);
        }

        doc.Linea(Margen, 104, Derecha, 104, 0.8, ColorLinea);

        // Datos generales en cuatro columnas.
        double[] columnas = { Margen, 180, 306, 432 };
        var campos = new (string Etiqueta, string Valor)[]
        {
            ("FECHA", c.Fecha.ToString("dd/MM/yyyy", CultureInfo.CurrentCulture)),
            ("HORA", c.Fecha.ToString("HH:mm", CultureInfo.CurrentCulture)),
            ("ATENDIÓ", string.IsNullOrWhiteSpace(c.Atendio) ? "-" : c.Atendio),
            ("ARTÍCULOS", $"{c.Unidades} en {c.Lineas.Count} línea{(c.Lineas.Count == 1 ? "" : "s")}")
        };

        for (int i = 0; i < campos.Length; i++)
        {
            doc.Texto(campos[i].Etiqueta, columnas[i], 120, 8, negrita: true, ColorTenue);
            doc.Texto(PdfDocumento.Recortar(campos[i].Valor, 120, 10.5), columnas[i], 133, 10.5,
                negrita: false, ColorTexto);
        }

        return 166;
    }

    private static double DibujarEncabezadoContinuacion(PdfDocumento doc, ComprobanteDespacho c)
    {
        doc.Rectangulo(0, 0, doc.Ancho, 6, ColorAcento);
        doc.Texto($"Comprobante de despacho #{c.Id:D5} (continuación)", Margen, 46, 12,
            negrita: true, ColorTexto);
        doc.Linea(Margen, 70, Derecha, 70, 0.8, ColorLinea);
        return 88;
    }

    private static double DibujarCabeceraTabla(PdfDocumento doc, double y)
    {
        doc.Rectangulo(Margen, y, Derecha - Margen, 22, ColorFondoFila);

        doc.Texto("CÓDIGO", XCodigo + 8, y + 7, 8, negrita: true, ColorSuave);
        doc.Texto("PRODUCTO", XProducto, y + 7, 8, negrita: true, ColorSuave);
        doc.TextoDerecha("CANT.", XCantidad, y + 7, 8, negrita: true, ColorSuave);
        doc.TextoDerecha("P. UNIT.", XPrecio, y + 7, 8, negrita: true, ColorSuave);
        doc.TextoDerecha("SUBTOTAL", XSubtotal - 8, y + 7, 8, negrita: true, ColorSuave);

        return y + 22;
    }

    private static void DibujarFila(PdfDocumento doc, LineaComprobante linea, double y, bool alterna)
    {
        if (alterna)
            doc.Rectangulo(Margen, y, Derecha - Margen, AltoFila, "#FAFBFC");

        doc.Texto(PdfDocumento.Recortar(linea.Codigo, AnchoCodigo - 8, 9), XCodigo + 8, y + 6, 9,
            negrita: false, ColorSuave);
        if (!string.IsNullOrWhiteSpace(linea.Lote))
        {
            // El lote va en gris a la derecha del nombre, para la trazabilidad de la venta.
            string textoLote = $"Lote {linea.Lote}";
            double anchoLote = PdfDocumento.AnchoTexto(textoLote, 8);
            doc.Texto(PdfDocumento.Recortar(linea.Nombre, AnchoProducto - anchoLote - 8, 10), XProducto, y + 5.5, 10,
                negrita: false, ColorTexto);
            doc.TextoDerecha(textoLote, XProducto + AnchoProducto, y + 7, 8, negrita: false, ColorTenue);
        }
        else
        {
            doc.Texto(PdfDocumento.Recortar(linea.Nombre, AnchoProducto, 10), XProducto, y + 5.5, 10,
                negrita: false, ColorTexto);
        }
        doc.TextoDerecha(linea.Cantidad.ToString(CultureInfo.CurrentCulture), XCantidad, y + 5.5, 10,
            negrita: false, ColorTexto);
        doc.TextoDerecha(Moneda(linea.PrecioUnitario), XPrecio, y + 5.5, 10, negrita: false, ColorSuave);
        doc.TextoDerecha(Moneda(linea.Subtotal), XSubtotal - 8, y + 5.5, 10, negrita: true, ColorTexto);

        doc.Linea(Margen, y + AltoFila, Derecha, y + AltoFila, 0.5, ColorLinea);
    }

    private static void DibujarTotales(PdfDocumento doc, ComprobanteDespacho c, double y)
    {
        y += 14;

        doc.Rectangulo(330, y, Derecha - 330, 40, ColorFondoFila);
        doc.Texto("TOTAL DESPACHADO", 344, y + 9, 9, negrita: true, ColorSuave);
        doc.Texto($"{c.Unidades} unidad{(c.Unidades == 1 ? "" : "es")}", 344, y + 23, 9,
            negrita: false, ColorTenue);
        doc.TextoDerecha(Moneda(c.Total), Derecha - 14, y + 12, 17, negrita: true, ColorAcento);

        y += 62;
        if (c.Anulado)
        {
            string quien = string.IsNullOrWhiteSpace(c.UsuarioAnulacion) ? "" : $" por {c.UsuarioAnulacion}";
            doc.Texto($"DESPACHO ANULADO el {c.FechaAnulacion:dd/MM/yyyy HH:mm}{quien}. Las unidades volvieron al inventario.",
                Margen, y, 9, negrita: true, ColorAnulado);
            if (!string.IsNullOrWhiteSpace(c.MotivoAnulacion))
                doc.Texto(PdfDocumento.Recortar($"Motivo: {c.MotivoAnulacion}", Derecha - Margen, 9), Margen, y + 14, 9,
                    negrita: false, ColorSuave);
        }
        else
        {
            doc.Texto("Este comprobante respalda la salida de inventario registrada en el sistema.",
                Margen, y, 9, negrita: false, ColorTenue);
        }
    }

    private static void DibujarPies(PdfDocumento doc, ComprobanteDespacho c)
    {
        string generado = DateTime.Now.ToString("dd/MM/yyyy HH:mm", CultureInfo.CurrentCulture);

        for (int i = 0; i < doc.TotalPaginas; i++)
        {
            doc.SeleccionarPagina(i);
            doc.Linea(Margen, 742, Derecha, 742, 0.5, ColorLinea);
            doc.Texto($"FarmaGestión · Despacho #{c.Id:D5} · Generado el {generado}",
                Margen, 750, 8, negrita: false, ColorTenue);
            doc.TextoDerecha($"Página {i + 1} de {doc.TotalPaginas}", Derecha, 750, 8,
                negrita: false, ColorTenue);
        }
    }

    private static string Moneda(decimal valor) => valor.ToString("C", CultureInfo.CurrentCulture);
}
