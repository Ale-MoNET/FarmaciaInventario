using System;
using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using FarmaGestion.Data.Repositories;
using FarmaGestion.Services;
using FarmaGestion.ViewModels;

namespace FarmaGestion.Views;

/// <summary>
/// Ventana emergente que aparece al confirmar un despacho: muestra el detalle y
/// ofrece descargarlo en PDF o mandarlo a imprimir.
/// </summary>
public partial class ComprobanteWindow : Window
{
    /// <summary>Constructor sin datos; lo usa el previsualizador de Avalonia.</summary>
    public ComprobanteWindow()
        : this(new ComprobanteDespacho(0, DateTime.Now, 0m, new List<LineaComprobante>()))
    {
    }

    /// <param name="consulta">True al abrirlo desde Reportes: el despacho no es reciente, solo se consulta.</param>
    public ComprobanteWindow(ComprobanteDespacho comprobante, bool consulta = false)
    {
        InitializeComponent();
        DataContext = new ComprobanteViewModel(comprobante, consulta);
        if (consulta) Title = "Comprobante de despacho";

        Opened += (_, _) => BtnDescargar.Focus();
    }

    private async void Descargar_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not ComprobanteViewModel vm) return;

        try
        {
            var archivo = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Guardar comprobante de despacho",
                SuggestedFileName = vm.NombreArchivo,
                DefaultExtension = "pdf",
                ShowOverwritePrompt = true,
                FileTypeChoices = new[]
                {
                    new FilePickerFileType("Documento PDF")
                    {
                        Patterns = new[] { "*.pdf" },
                        MimeTypes = new[] { "application/pdf" },
                        AppleUniformTypeIdentifiers = new[] { "com.adobe.pdf" }
                    }
                }
            });

            if (archivo is null) return; // el usuario canceló

            var pdf = vm.GenerarPdf();
            await using (var stream = await archivo.OpenWriteAsync())
            {
                await stream.WriteAsync(pdf);
            }

            LoggerService.Info($"Comprobante de despacho guardado en PDF: {archivo.Name}");
            vm.MostrarEstado($"PDF guardado como «{archivo.Name}».");
        }
        catch (Exception ex)
        {
            LoggerService.Error("No se pudo guardar el comprobante en PDF", ex);
            vm.MostrarEstado("No se pudo guardar el PDF. Intenta con otra carpeta.", esError: true);
        }
    }

    private void Imprimir_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not ComprobanteViewModel vm) return;

        try
        {
            var (exito, mensaje) = ImpresionService.Imprimir(vm.GenerarPdf(), vm.NombreArchivo);
            vm.MostrarEstado(mensaje, esError: !exito);
        }
        catch (Exception ex)
        {
            LoggerService.Error("No se pudo preparar el comprobante para imprimir", ex);
            vm.MostrarEstado("No se pudo preparar la impresión del comprobante.", esError: true);
        }
    }

    private async void Deshacer_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not ComprobanteViewModel vm) return;

        var dialogo = new AnularDespachoWindow(vm.Comprobante);
        var mensaje = await dialogo.ShowDialog<string?>(this);
        if (mensaje is null) return; // canceló o no se pudo deshacer

        var motivo = (dialogo.DataContext as AnularDespachoViewModel)?.Motivo.Trim() ?? string.Empty;
        vm.MarcarAnulado(motivo, SesionActual.Usuario?.NombreUsuario ?? string.Empty);
        vm.MostrarEstado(mensaje);
    }

    private void Cerrar_Click(object? sender, RoutedEventArgs e) => Close();
}
