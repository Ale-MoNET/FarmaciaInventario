# ✨ Nueva Funcionalidad: Crear Producto desde Escaneo

## 📋 ¿Qué es?

Cuando escaneas un código de barras que **NO existe** en la base de datos, la aplicación **abrirá automáticamente** una ventana para crear un nuevo medicamento con ese código pre-rellenado.

---

## 🎯 Cómo Funciona

### Flujo de Trabajo

```
Usuario escanea código
        ↓
¿El código existe en la BD?
        ├─ SÍ → Muestra el medicamento en la tabla
        │       (El campo se limpia para el siguiente escaneo)
        │
        └─ NO → Abre ventana de "Crear nuevo medicamento"
                Con el código ya rellenado en el campo
                Usuario completa: Nombre, Precio, Existencias
                Presiona "Crear medicamento"
                La tabla se actualiza automáticamente
```

---

## 🚀 Ejemplo Paso a Paso

### 1. Escanea un código que **SÍ existe**
```
Escaneas: 7501001234567
↓
Aparece: "Paracetamol 500mg" en la tabla
↓
El campo se limpia automáticamente
```

### 2. Escanea un código que **NO existe**
```
Escaneas: 7501999888777 (nuevo código)
↓
Se abre popup: "Crear medicamento - Código: 7501999888777"
↓
El campo "Código de barras" ya tiene: 7501999888777
↓
Completas:
  - Nombre: "Ibuprofeno Plus 600mg"
  - Precio: 45.50
  - Existencias: 100
  - Lote: "A2024"
  - Vencimiento: 2026-12-31
↓
Presionas "Crear medicamento"
↓
Se guarda en la BD y se cierra el popup
↓
La tabla se actualiza automáticamente
```

---

## 🔑 Cambios Implementados

### 1. **ProductoViewModel.cs**
```csharp
// Nuevo método para pre-rellenar datos
public void InicializarConCodigoEscaneado(string codigoBarras)
{
    CodigoBarras = codigoBarras;
    Nombre = string.Empty;
    // ... otros campos vacíos
    TituloVentana = $"Crear medicamento - Código: {codigoBarras}";
}
```

### 2. **ProductoWindow.axaml.cs**
```csharp
// Nuevo constructor que acepta código
public ProductoWindow(int? medicamentoId, string? codigoEscaneado)
{
    // Si tiene código, inicializa para crear nuevo
    if (!string.IsNullOrWhiteSpace(codigoEscaneado))
        viewModel.InicializarConCodigoEscaneado(codigoEscaneado);
}
```

### 3. **InventarioView.axaml**
```xml
<!-- Nuevo evento KeyDown para detectar Enter -->
<TextBox ... KeyDown="TxtBusqueda_KeyDown" />
```

### 4. **InventarioView.axaml.cs**
```csharp
// Nuevo manejador para cuando se presiona Enter
private async void TxtBusqueda_KeyDown(object? sender, KeyEventArgs e)
{
    if (e.Key == Key.Return)
    {
        // Si no hay resultados, abre ProductoWindow
        // Si hay resultados, limpia el campo
    }
}
```

---

## 📝 Archivos Modificados

```
FarmaGestion/
├── ViewModels/
│   └── ProductoViewModel.cs          ← Nuevo método
├── Views/
│   ├── ProductoWindow.axaml.cs       ← Nuevo constructor
│   ├── InventarioView.axaml          ← Evento KeyDown
│   └── InventarioView.axaml.cs       ← Manejador de evento
```

---

## 💡 Casos de Uso

### Caso 1: Recepción de Medicamentos
```
Recibes un lote de medicamentos nuevos
Escaneas cada código de barras
Si el código ya existe, solo actualiza cantidad
Si es nuevo, creas el registro al instante
Sin tener que abrir manualmente el formulario
```

### Caso 2: Actualización de Inventario
```
Auditoría rápida de productos
Escanea un código
Si aparece en la tabla = existe ✅
Si abre popup = falta registrar en la BD ❌
```

### Caso 3: Integración con Proveedor
```
Proveedor te da lista de códigos de barras
Escaneas los códigos
Los que falten se crean automáticamente
Los que existen se muestran al instante
```

---

## ⚙️ Cómo Probar

### Test 1: Producto Existente
1. Abre FarmaGestión → Inventario
2. Haz clic en el campo de búsqueda
3. **Escanea un código que ya existe** (ej: 7501001234567)
4. **Presiona Enter**
5. ✅ Debería mostrar el medicamento y limpiar el campo

### Test 2: Producto Nuevo
1. Abre FarmaGestión → Inventario
2. Haz clic en el campo de búsqueda
3. **Escribe un código que NO existe** (ej: TEST999)
4. **Presiona Enter**
5. ✅ Debería abrirse popup de "Crear medicamento - Código: TEST999"
6. Completa los datos y presiona "Crear medicamento"
7. ✅ Se crea en la BD y vuelve a la tabla

### Test 3: Búsqueda Normal (sin Enter)
1. Abre FarmaGestión → Inventario
2. Haz clic en el campo de búsqueda
3. **Escribe (sin presionar Enter)**: `Para`
4. ✅ La tabla debería filtrarse mostrando medicamentos con "Para"
5. **Presiona Escape o borra el campo**
6. ✅ Debería volver a mostrar todos

---

## 🔧 Configuración Avanzada

Si necesitas cambiar el comportamiento:

### Solo crear productos si el búsqueda es exacta
En `InventarioView.axaml.cs`, modifica:
```csharp
// Cambiar esta línea:
if (medicamentoEncontrado == null && !string.IsNullOrWhiteSpace(codigoEscaneado))

// A una búsqueda más estricta:
if (medicamentoEncontrado == null && codigoEscaneado.Length >= 8)
```

### Pedir confirmación antes de crear
```csharp
var confirm = new ConfirmWindow(
    "¿Crear medicamento?",
    $"El código {codigoEscaneado} no existe. ¿Deseas crear uno nuevo?",
    "Crear");
var confirmado = await confirm.ShowDialog<bool>(mainWindow);
if (confirmado)
{
    // Abrir ProductoWindow
}
```

---

## ✅ Checklist de Funcionalidad

- [x] Detecta cuando se presiona Enter
- [x] Verifica si el código existe
- [x] Si NO existe, abre popup para crear
- [x] Pre-rellena el código en el formulario
- [x] Guarda el nuevo medicamento
- [x] Recarga automáticamente la tabla
- [x] Limpia el campo para el siguiente escaneo
- [x] Funcionamiento manual (sin lector también funciona)

---

## 🐛 Troubleshooting

| Problema | Solución |
|----------|----------|
| No abre popup al presionar Enter | Verifica que el TextBox está enfocado, no otro control |
| El código no aparece en el formulario | Revisa que el constructor recibe el parámetro correctamente |
| Se abre popup pero el código está vacío | El TextBox no pasó el código, revisa `TxtBusqueda_KeyDown` |
| El popup abre pero después sigue mostrando búsqueda | Verifícalos últimos cambios en `InventarioView.axaml.cs` |

---

## 🎉 Beneficios

✅ **Workflow más rápido** - No necesitas cambiar entre pantallas
✅ **Menos errores** - El código se pre-rellena automáticamente
✅ **Mejor UX** - Intuitivo y fluido
✅ **Flexible** - Funciona con búsqueda manual también
✅ **Sin interrupciones** - Continúas escaneando después

---

**Fecha de Implementación**: 29/09/2026
**Estado**: ✅ Completado
