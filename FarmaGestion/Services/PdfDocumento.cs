using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace FarmaGestion.Services;

/// <summary>
/// Generador de PDF mínimo y autocontenido (sin dependencias externas).
/// Escribe documentos de una o varias páginas con texto, líneas y rectángulos
/// usando las fuentes base Helvetica / Helvetica-Bold, suficientes para los
/// comprobantes de despacho.
///
/// El sistema de coordenadas que expone es "de arriba hacia abajo" (Y=0 es el
/// borde superior de la hoja), que es como se piensa un documento; internamente
/// se convierte al sistema del PDF, cuyo origen está abajo a la izquierda.
/// </summary>
public sealed class PdfDocumento
{
    // Tamaño carta en puntos (1/72 de pulgada).
    public const double AnchoCarta = 612;
    public const double AltoCarta = 792;

    private const string ColorPorDefecto = "#111111";

    private readonly List<StringBuilder> _paginas = new();
    private int _paginaActual;

    public double Ancho { get; }
    public double Alto { get; }

    public PdfDocumento(double ancho = AnchoCarta, double alto = AltoCarta)
    {
        Ancho = ancho;
        Alto = alto;
        NuevaPagina();
    }

    /// <summary>Número de páginas creadas hasta ahora (la primera se crea sola).</summary>
    public int TotalPaginas => _paginas.Count;

    private StringBuilder Actual => _paginas[_paginaActual];

    /// <summary>Agrega una página en blanco y la deja como página activa.</summary>
    public void NuevaPagina()
    {
        _paginas.Add(new StringBuilder());
        _paginaActual = _paginas.Count - 1;
    }

    /// <summary>
    /// Cambia la página sobre la que se dibuja (base 0). Sirve para volver atrás y
    /// completar elementos que dependen del total de páginas, como los pies.
    /// </summary>
    public void SeleccionarPagina(int indice)
    {
        if (indice < 0 || indice >= _paginas.Count)
            throw new ArgumentOutOfRangeException(nameof(indice));
        _paginaActual = indice;
    }

    /// <summary>Dibuja texto con la esquina superior izquierda del renglón en (x, y).</summary>
    public void Texto(string texto, double x, double y, double tam = 10,
        bool negrita = false, string color = ColorPorDefecto)
    {
        if (string.IsNullOrEmpty(texto)) return;

        // La línea base va debajo de la posición indicada (aprox. 80% del tamaño).
        double baseY = Alto - (y + tam * 0.8);

        Actual.Append("BT\n")
              .Append(ColorRelleno(color))
              .Append('/').Append(negrita ? "F2" : "F1").Append(' ').Append(Num(tam)).Append(" Tf\n")
              .Append("1 0 0 1 ").Append(Num(x)).Append(' ').Append(Num(baseY)).Append(" Tm\n")
              .Append('(').Append(Escapar(Sanear(texto))).Append(") Tj\n")
              .Append("ET\n");
    }

    /// <summary>Dibuja texto alineado a la derecha: (xDerecha, y) es el extremo derecho.</summary>
    public void TextoDerecha(string texto, double xDerecha, double y, double tam = 10,
        bool negrita = false, string color = ColorPorDefecto)
    {
        if (string.IsNullOrEmpty(texto)) return;
        Texto(texto, xDerecha - AnchoTexto(texto, tam, negrita), y, tam, negrita, color);
    }

    /// <summary>Dibuja texto centrado respecto a xCentro.</summary>
    public void TextoCentrado(string texto, double xCentro, double y, double tam = 10,
        bool negrita = false, string color = ColorPorDefecto)
    {
        if (string.IsNullOrEmpty(texto)) return;
        Texto(texto, xCentro - AnchoTexto(texto, tam, negrita) / 2, y, tam, negrita, color);
    }

    /// <summary>Traza una línea recta entre dos puntos.</summary>
    public void Linea(double x1, double y1, double x2, double y2,
        double grosor = 0.7, string color = "#D8DEE6")
    {
        Actual.Append(ColorTrazo(color))
              .Append(Num(grosor)).Append(" w\n")
              .Append(Num(x1)).Append(' ').Append(Num(Alto - y1)).Append(" m\n")
              .Append(Num(x2)).Append(' ').Append(Num(Alto - y2)).Append(" l\nS\n");
    }

    /// <summary>Dibuja un rectángulo relleno; (x, y) es su esquina superior izquierda.</summary>
    public void Rectangulo(double x, double y, double ancho, double alto, string color)
    {
        Actual.Append(ColorRelleno(color))
              .Append(Num(x)).Append(' ').Append(Num(Alto - (y + alto))).Append(' ')
              .Append(Num(ancho)).Append(' ').Append(Num(alto)).Append(" re\nf\n");
    }

    /// <summary>Ancho en puntos que ocupará el texto con la fuente y tamaño dados.</summary>
    public static double AnchoTexto(string texto, double tam, bool negrita = false)
    {
        if (string.IsNullOrEmpty(texto)) return 0;

        double milesimas = 0;
        foreach (char c in Sanear(texto))
            milesimas += AnchoGlifo(c, negrita);

        return milesimas * tam / 1000.0;
    }

    /// <summary>Recorta el texto (agregando "...") para que quepa en el ancho indicado.</summary>
    public static string Recortar(string texto, double anchoMax, double tam, bool negrita = false)
    {
        if (string.IsNullOrEmpty(texto) || AnchoTexto(texto, tam, negrita) <= anchoMax)
            return texto;

        const string puntos = "...";
        double disponible = anchoMax - AnchoTexto(puntos, tam, negrita);
        if (disponible <= 0) return puntos;

        var sb = new StringBuilder();
        double acumulado = 0;
        foreach (char c in Sanear(texto))
        {
            double ancho = AnchoGlifo(c, negrita) * tam / 1000.0;
            if (acumulado + ancho > disponible) break;
            acumulado += ancho;
            sb.Append(c);
        }
        return sb.ToString().TrimEnd() + puntos;
    }

    /// <summary>Serializa el documento completo a bytes listos para escribir en disco.</summary>
    public byte[] Generar()
    {
        // Objetos fijos: 1 catálogo, 2 árbol de páginas, 3 Helvetica, 4 Helvetica-Bold.
        // Luego, por cada página: el objeto página y su flujo de contenido.
        const int primerObjetoPagina = 5;
        int totalPaginas = _paginas.Count;

        var kids = string.Join(" ",
            Enumerable.Range(0, totalPaginas).Select(i => $"{primerObjetoPagina + i * 2} 0 R"));

        var objetos = new List<byte[]>
        {
            Bytes("<< /Type /Catalog /Pages 2 0 R >>"),
            Bytes($"<< /Type /Pages /Kids [{kids}] /Count {totalPaginas} >>"),
            Bytes("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>"),
            Bytes("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Bold /Encoding /WinAnsiEncoding >>")
        };

        for (int i = 0; i < totalPaginas; i++)
        {
            int idContenido = primerObjetoPagina + i * 2 + 1;

            objetos.Add(Bytes(
                $"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 {Num(Ancho)} {Num(Alto)}] " +
                $"/Resources << /Font << /F1 3 0 R /F2 4 0 R >> >> /Contents {idContenido} 0 R >>"));

            var contenido = Bytes(_paginas[i].ToString());
            var flujo = new List<byte>();
            flujo.AddRange(Bytes($"<< /Length {contenido.Length} >>\nstream\n"));
            flujo.AddRange(contenido);
            flujo.AddRange(Bytes("\nendstream"));
            objetos.Add(flujo.ToArray());
        }

        var salida = new List<byte>();
        salida.AddRange(Bytes("%PDF-1.4\n"));
        salida.AddRange(Bytes("%âãÏÓ\n")); // marca de archivo binario

        var offsets = new int[objetos.Count + 1];
        for (int i = 0; i < objetos.Count; i++)
        {
            offsets[i + 1] = salida.Count;
            salida.AddRange(Bytes($"{i + 1} 0 obj\n"));
            salida.AddRange(objetos[i]);
            salida.AddRange(Bytes("\nendobj\n"));
        }

        int inicioXref = salida.Count;
        var xref = new StringBuilder();
        xref.Append("xref\n0 ").Append(objetos.Count + 1).Append('\n');
        xref.Append("0000000000 65535 f \n");
        for (int i = 1; i <= objetos.Count; i++)
            xref.Append(offsets[i].ToString("D10", CultureInfo.InvariantCulture)).Append(" 00000 n \n");

        xref.Append("trailer\n<< /Size ").Append(objetos.Count + 1).Append(" /Root 1 0 R >>\n")
            .Append("startxref\n").Append(inicioXref).Append("\n%%EOF\n");

        salida.AddRange(Bytes(xref.ToString()));
        return salida.ToArray();
    }

    // ── Utilidades internas ────────────────────────────────────────────────

    private static byte[] Bytes(string texto) => Encoding.Latin1.GetBytes(texto);

    private static string Num(double valor) => valor.ToString("0.###", CultureInfo.InvariantCulture);

    /// <summary>Convierte "#RRGGBB" al operador de color de relleno del PDF.</summary>
    private static string ColorRelleno(string hex)
    {
        var (r, g, b) = Componentes(hex);
        return $"{Num(r)} {Num(g)} {Num(b)} rg\n";
    }

    /// <summary>Convierte "#RRGGBB" al operador de color de trazo del PDF.</summary>
    private static string ColorTrazo(string hex)
    {
        var (r, g, b) = Componentes(hex);
        return $"{Num(r)} {Num(g)} {Num(b)} RG\n";
    }

    private static (double R, double G, double B) Componentes(string hex)
    {
        var limpio = hex.TrimStart('#');
        if (limpio.Length == 3)
            limpio = string.Concat(limpio.Select(c => new string(c, 2)));

        if (limpio.Length != 6 ||
            !int.TryParse(limpio, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int valor))
            return (0, 0, 0);

        return (((valor >> 16) & 0xFF) / 255.0, ((valor >> 8) & 0xFF) / 255.0, (valor & 0xFF) / 255.0);
    }

    /// <summary>Escapa los caracteres con significado especial dentro de una cadena PDF.</summary>
    private static string Escapar(string texto) =>
        texto.Replace("\\", "\\\\").Replace("(", "\\(").Replace(")", "\\)");

    /// <summary>
    /// Deja el texto en caracteres representables por WinAnsi/Latin-1: normaliza comillas
    /// tipográficas, guiones largos y elimina saltos de línea y símbolos fuera de rango
    /// (emojis, por ejemplo), que en un PDF de fuentes base no se pueden dibujar.
    /// </summary>
    private static string Sanear(string texto)
    {
        var sb = new StringBuilder(texto.Length);
        foreach (char c in texto)
        {
            switch (c)
            {
                case '“':
                case '”':
                case '«':
                case '»': sb.Append('"'); break;
                case '‘':
                case '’': sb.Append('\''); break;
                case '–':
                case '—': sb.Append('-'); break;
                case '…': sb.Append("..."); break;
                case ' ': sb.Append(' '); break;
                case '\r':
                case '\n':
                case '\t': sb.Append(' '); break;
                default:
                    if (c < 32) break;                       // controles: se descartan
                    if (c > 0xFF) { sb.Append('?'); break; } // fuera de Latin-1
                    if (c is >= (char)0x7F and <= (char)0x9F) break;
                    sb.Append(c);
                    break;
            }
        }
        return sb.ToString();
    }

    // Anchos (en milésimas de em) de Helvetica y Helvetica-Bold para los códigos 32..126,
    // tomados de las métricas AFM de Adobe. Permiten alinear a la derecha y recortar textos.
    private static readonly short[] AnchosNormal =
    {
        278,278,355,556,556,889,667,191,333,333,389,584,278,333,278,278,
        556,556,556,556,556,556,556,556,556,556,278,278,584,584,584,556,
        1015,667,667,722,722,667,611,778,722,278,500,667,556,833,722,778,
        667,778,722,667,611,722,667,944,667,667,611,278,278,278,469,556,
        333,556,556,500,556,556,278,556,556,222,222,500,222,833,556,556,
        556,556,333,500,278,556,500,722,500,500,500,334,260,334,584
    };

    private static readonly short[] AnchosNegrita =
    {
        278,333,474,556,556,889,722,238,333,333,389,584,278,333,278,278,
        556,556,556,556,556,556,556,556,556,556,333,333,584,584,584,611,
        975,722,722,722,722,667,611,778,722,278,556,722,611,833,722,778,
        667,778,722,667,611,722,667,944,667,667,611,333,278,333,584,556,
        333,556,611,556,611,556,333,611,611,278,278,556,278,889,611,611,
        611,611,389,556,333,611,556,778,556,556,500,389,280,389,584
    };

    // Acentuadas y signos de uso frecuente en español (el resto usa un ancho promedio).
    private static readonly Dictionary<char, (short Normal, short Negrita)> AnchosEspeciales = new()
    {
        ['¡'] = (333, 333), ['¿'] = (611, 611), ['°'] = (400, 400),
        ['á'] = (556, 556), ['é'] = (556, 556), ['í'] = (278, 278),
        ['ó'] = (556, 611), ['ú'] = (556, 611), ['ñ'] = (556, 611),
        ['ü'] = (556, 611),
        ['Á'] = (667, 722), ['É'] = (667, 667), ['Í'] = (278, 278),
        ['Ó'] = (778, 778), ['Ú'] = (722, 722), ['Ñ'] = (722, 722),
        ['Ü'] = (722, 722)
    };

    private static short AnchoGlifo(char c, bool negrita)
    {
        if (c is >= (char)32 and <= (char)126)
            return negrita ? AnchosNegrita[c - 32] : AnchosNormal[c - 32];

        if (AnchosEspeciales.TryGetValue(c, out var especial))
            return negrita ? especial.Negrita : especial.Normal;

        return negrita ? (short)611 : (short)556;
    }
}
