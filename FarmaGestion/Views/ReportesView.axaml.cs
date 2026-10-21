using Avalonia.Controls;
using FarmaGestion.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace FarmaGestion.Views;

public partial class ReportesView : UserControl
{
    public ReportesView()
    {
        InitializeComponent();
        DataContext = App.Services.GetRequiredService<ReportesViewModel>();
    }
}