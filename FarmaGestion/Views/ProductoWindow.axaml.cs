using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using FarmaGestion.ViewModels;

namespace FarmaGestion.Views;

public partial class ProductoWindow : Window
{
    public ProductoWindow()
    {
        InitializeComponent();
        var viewModal = new ProductoViewModel();

        viewModal.OnGuardadoExitoso = () =>
        {
            this.Close();
        };
        
        DataContext = viewModal;

        this.Opened += (s, e) =>
        {
            TxtCodigoBarras.Focus();
        };
    }
}