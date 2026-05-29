using System;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using FarmaGestion.Data; // Asegúrate de usar tu namespace correcto
using FarmaGestion.Models; 
using Microsoft.EntityFrameworkCore;
using System.IO;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Messaging;

namespace FarmaGestion.ViewModels;

public partial class InventarioViewModel : ObservableObject
{
    [ObservableProperty] 
    private ObservableCollection<Medicamentos> _medicamentos;
    
    public InventarioViewModel()
    {
        Medicamentos = new ObservableCollection<Medicamentos>();
        CargarInventario();
        WeakReferenceMessenger.Default.Register<InventarioActualizadoMessage>(this, (recipient, message) =>
        {
            Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
            {
                CargarInventario();
            });
        });
    }
    
    public void CargarInventario()
    {
        // Instanciamos nuestro contexto de base de datos
        using var context = new InventarioContext();
        
        // ¡ESTA ES LA LÍNEA CLAVE!
        // Revisa el archivo SQLite y aplica todas las migraciones pendientes automáticamente.
        // Si las tablas no existen, las crea en este instante.
        context.Database.Migrate();
        
        // Obtenemos todos los medicamentos de SQLite y los cargamos en memoria
        var listaDb = context.Medicamentos.ToList();
        
        // Limpiamos por si acaso y llenamos la colección observable
        Medicamentos.Clear();
        foreach (var med in listaDb)
        {
            Medicamentos.Add(med);
        }
    }
    
    public async Task ImportarCsvAsync(string rutaArchivo)
    {
        using var context = new InventarioContext();
        
        // Leemos todas las líneas del archivo de texto
        var lineas = await File.ReadAllLinesAsync(rutaArchivo);
        
        // Usamos Skip(1) para ignorar la primera línea (que suelen ser los encabezados: Codigo,Nombre,Precio,Existencias)
        foreach (var linea in lineas.Skip(1))
        {
            // Dividimos la línea por las comas
            var columnas = linea.Split(',');

            // Nos aseguramos de que la línea tenga al menos nuestras 4 columnas básicas
            if (columnas.Length >= 4)
            {
                string codigo = columnas[0].Trim();
                string nombre = columnas[1].Trim();
                decimal precio = decimal.TryParse(columnas[2], out var p) ? p : 0;
                int cantidad = int.TryParse(columnas[3], out var c) ? c : 0;

                // Buscamos si el medicamento ya está registrado en SQLite
                var medicamentoExistente = context.Medicamentos.FirstOrDefault(m => m.CodigoBarras == codigo);

                if (medicamentoExistente != null)
                {
                    // REQUERIMIENTO: Si ya existe, le sumamos las existencias nuevas y actualizamos la fecha
                    medicamentoExistente.Existencias += cantidad;
                    
                    // Opcional: Actualizar el precio al más reciente del CSV
                    medicamentoExistente.Precio = precio; 
                    medicamentoExistente.FechaUltimaActualizacion = DateTime.Now;
                }
                else
                {
                    // Si no existe, lo creamos desde cero
                    var nuevoMedicamento = new Medicamentos()
                    {
                        CodigoBarras = codigo,
                        Nombre = nombre,
                        Precio = precio,
                        Existencias = cantidad,
                        FechaUltimaActualizacion = DateTime.Now
                    };
                    context.Medicamentos.Add(nuevoMedicamento);
                }
            }
        }

        // Guardamos todos los cambios juntos. EF Core es muy eficiente haciendo esto.
        await context.SaveChangesAsync();
    }
}