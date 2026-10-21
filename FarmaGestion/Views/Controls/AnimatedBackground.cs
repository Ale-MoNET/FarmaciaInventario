using System;
using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;

namespace FarmaGestion.Views.Controls;

/// <summary>
/// Fondo animado que replica el canvas del diseño del login: un "reflector" radial
/// que respira y se desplaza, más una rejilla de cruces que titilan y se iluminan
/// con el color de acento cerca del reflector. Se dibuja por frame (~30 fps).
/// </summary>
public class AnimatedBackground : Control
{
    private readonly DispatcherTimer _timer;
    private readonly Stopwatch _reloj = Stopwatch.StartNew();

    // Color de acento (verde menta) del diseño.
    private const byte AR = 52, AG = 224, AB = 161;
    private const double Gap = 48, Arm = 5;

    public AnimatedBackground()
    {
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(33) };
        _timer.Tick += (_, _) => InvalidateVisual();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _timer.Start();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _timer.Stop();
    }

    public override void Render(DrawingContext ctx)
    {
        double w = Bounds.Width, h = Bounds.Height;
        if (w <= 0 || h <= 0) return;

        double t = _reloj.Elapsed.TotalSeconds;

        // --- reflector que respira y se desplaza ---
        double gx = w * (0.5 + 0.30 * Math.Sin(t * 0.13));
        double gy = h * (0.46 + 0.30 * Math.Cos(t * 0.10));
        double radius = Math.Max(w, h) * 0.62;

        var spotlight = new RadialGradientBrush
        {
            Center = new RelativePoint(gx, gy, RelativeUnit.Absolute),
            GradientOrigin = new RelativePoint(gx, gy, RelativeUnit.Absolute),
            RadiusX = new RelativeScalar(radius / w, RelativeUnit.Relative),
            RadiusY = new RelativeScalar(radius / h, RelativeUnit.Relative),
            GradientStops =
            {
                new GradientStop(Color.FromArgb(43, AR, AG, AB), 0), // ~0.17 alpha
                new GradientStop(Color.FromArgb(0, AR, AG, AB), 1)
            }
        };
        ctx.DrawRectangle(spotlight, null, new Rect(0, 0, w, h));

        // --- rejilla de cruces que titilan ---
        double driftX = (t * 6) % Gap, driftY = (t * 4) % Gap;
        double reach = Math.Max(w, h) * 0.34;

        for (double y = -Gap; y < h + Gap; y += Gap)
        {
            for (double x = -Gap; x < w + Gap; x += Gap)
            {
                double px = x + driftX, py = y + driftY;
                double near = Math.Max(0, 1 - Distancia(px - gx, py - gy) / reach);
                double tw = 0.5 + 0.5 * Math.Sin(t * 1.1 + x * 0.05 + y * 0.04);
                double a = (0.09 + near * 0.5) * (0.6 + 0.4 * tw);

                Color color;
                if (near > 0.28 && tw > 0.72)
                    color = Color.FromArgb(Alpha(Math.Min(0.95, a + 0.22)), AR, AG, AB);
                else
                    color = Color.FromArgb(Alpha(a), 232, 232, 232);

                var pen = new Pen(new SolidColorBrush(color), 1.6) { LineCap = PenLineCap.Round };
                ctx.DrawLine(pen, new Point(px - Arm, py), new Point(px + Arm, py));
                ctx.DrawLine(pen, new Point(px, py - Arm), new Point(px, py + Arm));
            }
        }
    }

    private static double Distancia(double dx, double dy) => Math.Sqrt(dx * dx + dy * dy);

    private static byte Alpha(double a) => (byte)(Math.Clamp(a, 0, 1) * 255);
}
