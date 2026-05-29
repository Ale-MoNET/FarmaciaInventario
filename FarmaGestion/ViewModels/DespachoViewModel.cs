using System;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using FarmaGestion.Data;
using FarmaGestion.Models;

namespace FarmaGestion.ViewModels;

public partial class ItemCarrito : ObservableObject
{
    public Medicamentos Medicamento { get; set; }

    [ObservableProperty] [NotifyPropertyChangedFor(nameof(Subtotal))]
    private int _cantidad;
    
    public decimal Subtotal => Medicamento.Precio * Cantidad;
}

public partial class DespachoViewModel : ObservableObject
{
    [ObservableProperty] private string _codigoBusqueda = string.Empty;
    
    [ObservableProperty] private decimal _totalDespacho;
    
    [ObservableProperty] private string _mensajeAlerta = string.Empty;

    public ObservableCollection<ItemCarrito> Carritos { get; } = new();

    [RelayCommand]
    private void AgregarCarrito()
    {
        if (string.IsNullOrWhiteSpace(CodigoBusqueda)) return;

        using var context = new InventarioContext();
        
        var producto = context.Medicamentos.FirstOrDefault(m => m.CodigoBarras == CodigoBusqueda);

        if (producto != null)
        {
            var itemExistente = Carritos.FirstOrDefault(i => i.Medicamento.Id == producto.Id);
            if (itemExistente != null)
            {
                if (itemExistente.Cantidad + 1 <= producto.Existencias)
                {
                    itemExistente.Cantidad++;
                }
            }
            else
            {
                if (producto.Existencias > 0)
                {
                    Carritos.Add(new ItemCarrito { Medicamento = producto, Cantidad = 1 });
                }
            }
            
            RecalcularTotal();
        }
        else
        {
            MensajeAlerta = $"❌  El código '{CodigoBusqueda}' no existe en el inventario.";
        }
        
        CodigoBusqueda = string.Empty;

    }
    [RelayCommand]
    private void ConfirmarDespacho()
    {
        // 1. Validamos que haya algo en el carrito antes de guardar
        if (!Carritos.Any())
        {
            Console.WriteLine("⚠️ El carrito está vacío, no se puede despachar.");
            return; 
        }

        using var context = new InventarioContext();

        // 2. Creamos el "Encabezado" del despacho
        var nuevoDespacho = new Despacho
        {
            Fecha = DateTime.Now,
            Total = TotalDespacho
        };

        // 3. Recorremos el carrito para armar el detalle y rebajar el inventario
        foreach (var item in Carritos)
        {
            // Es vital buscar el medicamento en este 'context' nuevo para poder modificarlo
            var productoBd = context.Medicamentos.Find(item.Medicamento.Id);
        
            if (productoBd != null)
            {
                // ¡Aquí restamos el inventario!
                productoBd.Existencias -= item.Cantidad;

                // Creamos la línea de detalle
                var detalle = new DetalleDespacho
                {
                    MedicamentoId = productoBd.Id,
                    Cantidad = item.Cantidad,
                    PrecioHistorico = productoBd.Precio
                };
            
                // Lo agregamos a la lista del despacho
                nuevoDespacho.Detalles.Add(detalle);
            }
        }

        // 4. Agregamos el despacho a la base de datos
        context.Despachos.Add(nuevoDespacho);
    
        // Al hacer SaveChanges, EF Core guarda el Despacho, los Detalles y actualiza los Medicamentos de un solo golpe.
        context.SaveChanges(); 
    
        WeakReferenceMessenger.Default.Send(new InventarioActualizadoMessage());
        Carritos.Clear();
        RecalcularTotal();
        CodigoBusqueda = string.Empty;
    }
    private void RecalcularTotal()
    {
        TotalDespacho = Carritos.Sum(i => i.Subtotal);
    }
}