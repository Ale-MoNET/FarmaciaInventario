using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;

namespace FarmaGestion.Views;

public partial class ConfirmWindow : Window
{
    private bool _resultado;

    public ConfirmWindow() : this("Confirmar", "", "Aceptar", true, true) { }

    /// <param name="peligro">Si es true, el botón de acción va en rojo (acción destructiva).</param>
    /// <param name="mostrarCancelar">Si es false, solo se muestra el botón de acción (modo informativo).</param>
    public ConfirmWindow(string titulo, string mensaje, string textoAceptar = "Aceptar",
        bool peligro = true, bool mostrarCancelar = true)
    {
        InitializeComponent();

        Title = titulo;
        TxtTitulo.Text = titulo;
        TxtMensaje.Text = mensaje;
        BtnAceptar.Content = textoAceptar;
        BtnCancelar.IsVisible = mostrarCancelar;

        if (!peligro)
        {
            BtnAceptar.Background = SolidColorBrush.Parse("#34E0A1");
            BtnAceptar.Foreground = SolidColorBrush.Parse("#04160E");
        }
    }

    private void Aceptar_Click(object? sender, RoutedEventArgs e)
    {
        _resultado = true;
        Close(_resultado);
    }

    private void Cancelar_Click(object? sender, RoutedEventArgs e)
    {
        _resultado = false;
        Close(_resultado);
    }
}
