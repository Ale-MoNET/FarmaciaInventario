using Avalonia.Controls;
using Avalonia.Interactivity;

namespace FarmaGestion.Views;

public partial class LoginWindow : Window
{
    public LoginWindow()
    {
        InitializeComponent();

        // Enfocar automáticamente el campo de usuario al abrir
        this.Opened += (s, e) =>
        {
            TxtUsuario.Focus();
        };
    }

    /// <summary>
    /// Alterna entre mostrar y ocultar el texto de la contraseña.
    /// </summary>
    private void TogglePw_Click(object? sender, RoutedEventArgs e)
    {
        bool revelar = TxtPassword.PasswordChar != '\0';
        TxtPassword.PasswordChar = revelar ? '\0' : '●';
        BtnTogglePw.Content = revelar ? "OCULTAR" : "MOSTRAR";
    }
}
