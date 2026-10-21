# Guía de Integración del Lector de Barras

## Descripción
Tu sistema ahora soporta lectores de código de barras USB que actúan como teclados. Cuando escanees un código, se capturará automáticamente en el campo de búsqueda y filtrará los medicamentos por:
- **Código de barras** (búsqueda exacta o parcial)
- **Nombre del medicamento** (búsqueda parcial)

## Cómo funciona

### 1. **Búsqueda en tiempo real**
- Abre la vista de **Inventario**
- El campo "Buscar o escanear código de barras…" está activo
- Cuando escanees un código o escribas manualmente, verás los resultados filtrados instantáneamente

### 2. **Tipos de búsqueda**
- **Código exacto**: Si tu lector escanea `7501001234567`, se buscará exactamente ese código
- **Búsqueda parcial**: Si escribes `7501` o parte del nombre, se mostrarán todos los medicamentos que coincidan
- **Sin resultados**: Si no hay coincidencias, la tabla se vacía y puedes intentar con otra búsqueda

### 3. **Limpiar búsqueda**
- Borra el campo de búsqueda para volver a ver todos los medicamentos

## Pruebas sin hardware físico

### Opción A: Simulación Manual
1. Abre la aplicación y ve a la vista **Inventario**
2. Haz clic en el campo de búsqueda
3. Escribe un código de barras de un medicamento conocido
4. Verifica que la tabla se filtre correctamente

### Opción B: Test Automático
Crea medicamentos de prueba con códigos de barras conocidos:

```
Código: TEST001
Nombre: Medicamento Prueba 1
Precio: 50.00
Existencias: 100

Código: ASPIRIN100
Nombre: Aspirina 100mg
Precio: 25.00
Existencias: 200
```

Luego busca por:
- `TEST001` - debe mostrar solo "Medicamento Prueba 1"
- `TEST` - debe mostrar todos los que comienzan con TEST
- `Aspir` - debe mostrar "Aspirina 100mg"

## Instrucciones por tipo de lector

### Lector USB tipo "Wedge" (más común)
1. Conecta el lector al puerto USB
2. Espera a que el sistema lo reconozca como teclado
3. Coloca el foco en el campo de búsqueda
4. Escanea un código de barras
5. El código se ingresará automáticamente y se filtrará

### Lector Serial (COM/RS232)
Si tu lector es serial, necesitaremos:
1. Saber el puerto COM (COM1, COM3, etc.)
2. La velocidad en bauds (típicamente 9600)
3. Implementar comunicación serial en el servicio

### Lector HID personalizado
Si requiere software específico, notificanos los detalles del dispositivo.

## Configuración avanzada

Si necesitas ajustar el comportamiento del escaneo:

**En `BarcodeService.cs`:**
- Puedes cambiar si la búsqueda es sensible a mayúsculas/minúsculas
- Puedes ajustar la lógica de búsqueda (exacta vs. parcial)
- Puedes agregar validación de formato de código

**En `InventarioView.axaml.cs`:**
- Podrías agregar un evento `PreviewKeyDown` para detectar el patrón de escaneo
- Algunos lectores terminan con Enter o una secuencia especial

## Troubleshooting

| Problema | Solución |
|----------|----------|
| El lector no ingresa nada | Verifica que el campo esté en foco (click en el TextBox) |
| Los resultados no aparecen | Comprueba que el código de barras existe en la BD |
| Búsqueda lenta | Normal si hay muchos medicamentos; considera índices en BD |
| Lector no reconocido | Instala drivers del fabricante, verifica en Gestor de Dispositivos |

## Próximos pasos

Para mayor funcionalidad, considera:
1. ✅ Búsqueda instantánea (ya implementado)
2. ⏳ Sonido de confirmación al escanear
3. ⏳ Historial de escaneos recientes
4. ⏳ Integración con módulo de despachos
5. ⏳ Validación de existencias al escanear

---

**Fecha**: 29/09/2026
**Última actualización**: Integración inicial de escaneo
