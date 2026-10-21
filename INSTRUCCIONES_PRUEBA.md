# 🧪 Instrucciones para Probar el Lector de Barras

## Opción 1: Ejecutar con el Script (Recomendado)

### En tu Mac, abre Terminal y ejecuta:

```bash
cd ~/Documents/GitHub/FarmaciaInventario
./run-app.sh
```

El script hará:
1. ✅ Compilar el proyecto con los cambios nuevos
2. ✅ Ejecutar la aplicación
3. ✅ Mostrar instrucciones en la pantalla

---

## Opción 2: Usar Visual Studio o Rider

1. Abre el proyecto en Visual Studio for Mac o JetBrains Rider
2. Selecciona la configuración **Release** (esquina superior)
3. Presiona el botón **▶ Run** (o Ctrl+R en Rider)

---

## Opción 3: Compilar desde Terminal manualmente

```bash
cd ~/Documents/GitHub/FarmaciaInventario

# Restaurar dependencias
dotnet restore

# Compilar
dotnet build FarmaGestion/FarmaGestion.csproj

# Ejecutar
dotnet run --project FarmaGestion/FarmaGestion.csproj
```

---

## 🔐 Credenciales de Prueba

Cuando se abra la aplicación, usa estas credenciales:

| Campo | Valor |
|-------|-------|
| **Usuario** | admin |
| **Contraseña** | admin |

(O la credencial que hayas creado para tu primer usuario)

---

## 🎮 Pasos para Probar

### 1. Inicia Sesión
- Introduce el usuario y contraseña
- Presiona "Inicia Sesión"

### 2. Navega a Inventario
- En el menú principal, selecciona "Inventario"
- Deberías ver una tabla con los medicamentos

### 3. Prueba la Búsqueda Manual
- **Haz clic en el campo** "Buscar o escanear código de barras…"
- Escribe algo para probar (ejemplos abajo)
- Observa cómo se filtra la tabla en **tiempo real**

### 4. Prueba con el Lector (si lo tienes conectado)
- Conecta tu lector de barras USB al Mac
- El SO lo reconocerá automáticamente
- Coloca el foco en el campo de búsqueda
- Escanea un medicamento
- ¡La tabla se actualiza automáticamente! 🎉

---

## 📝 Ejemplos de Búsqueda

Prueba estos términos en el campo de búsqueda:

| Búsqueda | Resultado Esperado |
|----------|-------------------|
| `7501` | Muestra productos con código que empieza en 7501 |
| `Para` | Muestra medicamentos con "Para" en el nombre (ej: Paracetamol) |
| `100` | Muestra productos con 100 en código o nombre |
| (dejar vacío) | Muestra todos los medicamentos |
| `XXXX` | Lista vacía (no existe) |

---

## ✅ Verificación de que Funciona

Si ves esto, ¡todo está funcionando correctamente! ✨

- [ ] La tabla se filtra mientras escribes
- [ ] La búsqueda es inmediata (sin delay)
- [ ] Los resultados son precisos
- [ ] El campo se limpia al presionar borrar todo
- [ ] Funciona con el lector de barras

---

## 🔧 Troubleshooting

### La aplicación no compila

```bash
# 1. Limpia la compilación anterior
dotnet clean FarmaGestion/FarmaGestion.csproj

# 2. Restaura y recompila
dotnet restore
dotnet build FarmaGestion/FarmaGestion.csproj -v d
```

### El campo de búsqueda no funciona

1. **Verifica que está enfocado** - Haz clic en el campo y deberías ver un cursor
2. **Comprueba que escribes texto** - Escribe algo como "prueba"
3. **Revisa la consola** - Mira si hay mensajes de error

### El lector no transmite nada

1. Verifica que está **conectado al USB**
2. Abre **Configuración > Seguridad** y verifica que el lector está reconocido
3. Intenta **reiniciar** el lector desconectando y reconectando
4. Prueba primero manualmente (escribiendo) antes de usar el lector

### Aplicación muy lenta

- Normal si tienes muchos medicamentos en la BD
- La búsqueda es rápida, pero cargar muchos datos toma tiempo
- Intenta filtrar primero manualmente

---

## 📊 Archivos Clave Modificados

Si necesitas revisar los cambios:

```
FarmaGestion/
├── Services/
│   ├── BarcodeService.cs           ← NUEVO: Lógica de búsqueda
│   └── ServiceConfiguration.cs      ← Registración del servicio
├── ViewModels/
│   └── InventarioViewModel.cs       ← Lógica de filtrado
└── Views/
    └── InventarioView.axaml         ← TextBox vinculado
```

---

## 🎯 Próximos Pasos

Después de confirmar que funciona:

1. ✅ Prueba con tu lector de barras real
2. ⏳ Agrega más medicamentos si necesitas
3. ⏳ Configura opciones avanzadas (si las necesitas)
4. ⏳ Desploy a producción

---

## 📞 ¿Problemas?

Si algo no funciona:
1. Revisa los mensajes de error en la consola
2. Verifica que los cambios están en los archivos (lee BarcodeService.cs)
3. Intenta limpiar y recompilar: `dotnet clean && dotnet build`
4. Reinicia el IDE si lo estás usando

---

**¡Buena suerte con tu lector de barras!** 🎉

Fecha: 29/09/2026
