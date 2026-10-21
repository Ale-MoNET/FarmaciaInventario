using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FarmaGestion.Data.Repositories;
using FarmaGestion.Models;
using FarmaGestion.Services;

namespace FarmaGestion.ViewModels;

public partial class ProductoViewModel : ObservableObject
{
    private readonly IMedicamentoRepository _medicamentoRepo;

    [ObservableProperty] private string _codigoBarras = string.Empty;
    [ObservableProperty] private string _nombre = string.Empty;
    [ObservableProperty] private string _precio = "0";

    // Primer lote del producto (solo al crear). Después, las existencias entran por
    // "Ingresar mercadería" y salen por despachos o bajas de lote.
    [ObservableProperty] private string _existencias = "0";
    [ObservableProperty] private string? _lote;
    [ObservableProperty] private DateTime? _fechaVencimiento;

    [ObservableProperty] private string _mensajeError = string.Empty;
    [ObservableProperty] private bool _mostrarError;

    // Aviso cuando el código escaneado ya pertenece a un producto registrado.
    [ObservableProperty] private string _aviso = string.Empty;
    [ObservableProperty] private bool _mostrarAviso;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MostrarPrimerLote))]
    private bool _esEdicion;

    /// <summary>La sección de lote inicial solo aplica al crear un producto.</summary>
    public bool MostrarPrimerLote => !EsEdicion;

    [ObservableProperty] private string _tituloVentana = "Registrar medicamento";
    [ObservableProperty] private string _textoBoton = "Guardar medicamento";

    private int _medicamentoId;

    public Action? OnGuardadoExitoso;

    /// <summary>La vista abre "Ingresar mercadería" con el código cuando el producto ya existe.</summary>
    public Action<string>? OnIrAIngresoMercaderia;

    public ProductoViewModel(IMedicamentoRepository medicamentoRepo)
    {
        _medicamentoRepo = medicamentoRepo;
    }

    /// <summary>Carga un medicamento existente y pone el formulario en modo edición.</summary>
    public async Task CargarParaEdicionAsync(int id)
    {
        var m = await _medicamentoRepo.ObtenerPorIdAsync(id);
        if (m == null) return;
        CargarDesde(m);
    }

    private void CargarDesde(Medicamentos m)
    {
        _medicamentoId = m.Id;
        CodigoBarras = m.CodigoBarras;
        Nombre = m.Nombre;
        Precio = m.Precio.ToString(System.Globalization.CultureInfo.InvariantCulture);

        EsEdicion = true;
        TituloVentana = "Editar medicamento";
        TextoBoton = "Guardar cambios";
    }

    /// <summary>
    /// Al terminar de escribir o escanear el código: si ya pertenece a un producto, el
    /// formulario se llena con sus datos y pasa a modo edición (así no se crean duplicados
    /// ni se pierde lo escaneado). Devuelve true si cambió a un producto existente.
    /// </summary>
    public async Task<bool> ComprobarCodigoExistenteAsync()
    {
        var codigo = CodigoBarras.Trim();
        if (codigo.Length == 0) return false;

        Medicamentos? existente;
        try
        {
            existente = await _medicamentoRepo.ObtenerPorCodigoAsync(codigo);
        }
        catch (Exception ex)
        {
            LoggerService.Error("Error al comprobar el código de barras", ex);
            return false;
        }

        if (existente == null)
        {
            MostrarAviso = false;
            return false;
        }

        if (EsEdicion)
        {
            if (existente.Id == _medicamentoId) return false;
            MostrarErrorValidacion($"❌ El código '{codigo}' ya pertenece a «{existente.Nombre}».");
            return false;
        }

        MostrarExistente(existente);
        return true;
    }

    private void MostrarExistente(Medicamentos existente)
    {
        CargarDesde(existente);
        MostrarError = false;
        MensajeError = string.Empty;
        Aviso = $"Este código ya está registrado como «{existente.Nombre}» ({existente.Existencias} u en existencia). " +
                "Aquí puedes corregir su nombre o precio; para sumar existencias usa «Ingresar mercadería».";
        MostrarAviso = true;
    }

    [RelayCommand]
    private void IrAIngresoMercaderia() => OnIrAIngresoMercaderia?.Invoke(CodigoBarras.Trim());

    /// <summary>Pre-rellena el código de barras para crear un nuevo medicamento desde escaneo.</summary>
    public void InicializarConCodigoEscaneado(string codigoBarras)
    {
        CodigoBarras = codigoBarras.Trim();
        Nombre = string.Empty;
        Precio = "0";
        Existencias = "0";
        Lote = null;
        FechaVencimiento = null;

        EsEdicion = false;
        TituloVentana = $"Crear medicamento - Código: {codigoBarras}";
        TextoBoton = "Crear medicamento";
    }

    [RelayCommand]
    private async Task GuardarAsync()
    {
        MostrarError = false;
        MensajeError = string.Empty;

        // 1. Validación de campos obligatorios
        if (string.IsNullOrWhiteSpace(CodigoBarras))
        {
            MostrarErrorValidacion("⚠️ El código de barras es obligatorio.");
            return;
        }
        if (string.IsNullOrWhiteSpace(Nombre))
        {
            MostrarErrorValidacion("⚠️ El nombre del medicamento es obligatorio.");
            return;
        }

        // 2. Validación numérica
        if (!decimal.TryParse(Precio, out var precio) || precio < 0)
        {
            MostrarErrorValidacion("⚠️ El precio debe ser un número válido y no negativo.");
            return;
        }

        // 3. Primer lote (solo al crear): si entran unidades, lote y vencimiento son obligatorios.
        Lote? loteInicial = null;
        if (!EsEdicion)
        {
            if (!int.TryParse(Existencias, out var existencias) || existencias < 0)
            {
                MostrarErrorValidacion("⚠️ Las existencias deben ser un número entero no negativo.");
                return;
            }

            bool hayLote = !string.IsNullOrWhiteSpace(Lote);
            if (existencias > 0 || hayLote)
            {
                if (!hayLote)
                {
                    MostrarErrorValidacion("⚠️ Indica el número de lote de las existencias iniciales.");
                    return;
                }
                if (existencias == 0)
                {
                    MostrarErrorValidacion("⚠️ Indica cuántas unidades entran del lote (o deja el lote vacío para crear el producto sin existencias).");
                    return;
                }
                if (FechaVencimiento is null)
                {
                    MostrarErrorValidacion("⚠️ Indica la fecha de vencimiento del lote.");
                    return;
                }
                if (FechaVencimiento.Value.Date < DateTime.Today)
                {
                    MostrarErrorValidacion("⚠️ La fecha de vencimiento ya pasó; revisa el año.");
                    return;
                }

                loteInicial = new Lote
                {
                    NumeroLote = Lote!.Trim(),
                    FechaVencimiento = FechaVencimiento.Value.Date,
                    Existencias = existencias
                };
            }
        }

        var codigo = CodigoBarras.Trim();

        try
        {
            // 4. Unicidad del código. Al crear, si ya existe se carga ese producto en vez de
            //    duplicarlo; al editar, no se permite tomar el código de otro producto.
            if (!EsEdicion)
            {
                var existente = await _medicamentoRepo.ObtenerPorCodigoAsync(codigo);
                if (existente != null)
                {
                    MostrarExistente(existente);
                    return;
                }
            }
            else if (await _medicamentoRepo.ExisteCodigoAsync(codigo, _medicamentoId))
            {
                MostrarErrorValidacion($"❌ Ya existe otro medicamento con el código '{codigo}'.");
                return;
            }

            var medicamento = new Medicamentos
            {
                Id = _medicamentoId,
                CodigoBarras = codigo,
                Nombre = Nombre.Trim(),
                Precio = precio,
                FechaUltimaActualizacion = DateTime.Now
            };

            if (EsEdicion)
            {
                await _medicamentoRepo.ActualizarAsync(medicamento);
                LoggerService.Info($"Medicamento actualizado: {codigo} - {Nombre.Trim()}");
            }
            else
            {
                if (loteInicial != null)
                    medicamento.Lotes.Add(loteInicial);

                await _medicamentoRepo.AgregarAsync(medicamento);
                LoggerService.Info($"Medicamento registrado: {codigo} - {Nombre.Trim()}" +
                                   (loteInicial != null ? $" (lote {loteInicial.NumeroLote}, {loteInicial.Existencias} u)" : string.Empty));
            }
        }
        catch (Exception ex)
        {
            LoggerService.Error("Error al guardar medicamento", ex);
            MostrarErrorValidacion("❌ No se pudo guardar el medicamento. Inténtalo de nuevo.");
            return;
        }

        OnGuardadoExitoso?.Invoke();
    }

    private void MostrarErrorValidacion(string mensaje)
    {
        MensajeError = mensaje;
        MostrarError = true;
    }
}
