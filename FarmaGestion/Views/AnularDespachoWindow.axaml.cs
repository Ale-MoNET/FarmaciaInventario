using System;
using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Interactivity;
using FarmaGestion.Data.Repositories;
using FarmaGestion.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace FarmaGestion.Views;

/// <summary>
/// Diálogo para deshacer un despacho. Al cerrarse devuelve el mensaje de éxito del
/// repositorio, o null si se canceló o no se pudo deshacer.
/// </summary>
public partial class AnularDespachoWindow : Window
{
    /// <summary>Constructor sin datos; lo usa el previsualizador de Avalonia.</summary>
    public AnularDespachoWindow()
        : this(new ComprobanteDespacho(0, DateTime.Now, 0m, new List<LineaComprobante>()))
    {
    }

    public AnularDespachoWindow(ComprobanteDespacho comprobante)
    {
        InitializeComponent();
        var viewModel = App.Services.GetRequiredService<AnularDespachoViewModel>();
        viewModel.Inicializar(comprobante);
        viewModel.OnDeshecho = mensaje => Close(mensaje);
        DataContext = viewModel;

        Opened += (_, _) => TxtMotivo.Focus();
    }

    private void Cancelar_Click(object? sender, RoutedEventArgs e) => Close(null);
}
