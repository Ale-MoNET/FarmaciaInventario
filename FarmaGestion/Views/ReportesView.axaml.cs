using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using FarmaGestion.ViewModels;

namespace FarmaGestion.Views;

public partial class ReportesView : UserControl
{
    public ReportesView()
    {
        InitializeComponent();
        DataContext = new ReportesViewModel();
    }
}