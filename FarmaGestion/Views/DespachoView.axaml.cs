using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using FarmaGestion.Data;
using FarmaGestion.ViewModels;

namespace FarmaGestion.Views;

public partial class DespachoView : UserControl
{
    public DespachoView()
    {
        InitializeComponent();
        DataContext = new DespachoViewModel();

        this.Loaded += (sender, args) =>
        {
            TxtCodigoBusqueda.Focus();
        };       
    }
}