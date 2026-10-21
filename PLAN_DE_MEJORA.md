# Plan de Mejora — FarmaGestion (Inventario de Farmacia)

> Documento de análisis y hoja de ruta para evolucionar la aplicación de escritorio
> **FarmaGestion** hacia un estado más seguro, robusto y mantenible.
>
> **Fecha:** 2026-06-20
> **Stack:** .NET 8.0 · Avalonia 12 · CommunityToolkit.Mvvm 8.4 · EF Core 9 (SQLite)

---

## 0. Estado de implementación (2026-06-20)

Se aplicó la **Fase 1 completa**, todos los **quick wins** y varios ítems de mantenibilidad.
El proyecto **compila sin errores**.

| ✅ Implementado | Detalle |
|---|---|
| **C1** | `inventario_medicamentos.db` quitado de Git; `.gitignore` ignora `*.db`, logs y credenciales. |
| **C2** | Contraseña de admin **aleatoria** en el primer arranque, escrita en `credenciales_admin.txt` (en la carpeta de datos). Se eliminó `Admin123!`. |
| **C3** | Despacho envuelto en **transacción** con rollback + **revalidación de stock** + try-catch. |
| **C4** | **RBAC**: la pestaña *Usuarios* solo es visible para `Admin` (nuevo `SesionActual`). |
| **C5** | Reporte de ventas reescrito con un **único `GroupJoin`** (sin N+1). |
| **A2** | try-catch en `SaveChanges` de login, usuarios, producto, despacho e importación CSV. |
| **A3** | Nuevo `LoggerService` a archivo; se eliminó el `Console.WriteLine` de producción. |
| **A4** | `ProductoViewModel` valida campos, números y **unicidad** del código de barras (con mensaje en la vista). |
| **A5** | BD y logs reubicados a la **carpeta de datos del usuario** (`AppPaths`). |
| **M1** | Política de contraseñas: mínimo 8, mayúscula y número. |
| **M4** | `Database.Migrate()` solo en el arranque (se quitó de `CargarInventario`). |
| **M6** | Roles centralizados en `Models/Rol.cs` (sin *magic strings*). |
| **B1** | DataAnnotations en `Medicamentos` y `Usuario`. |

**Archivos nuevos (fase 1):** `Models/Rol.cs`, `Services/AppPaths.cs`, `Services/LoggerService.cs`, `Services/SesionActual.cs`.

### Fase 2 — Arquitectura (también implementada)

| ✅ Implementado | Detalle |
|---|---|
| **M5** | **Inyección de dependencias** (`Microsoft.Extensions.DependencyInjection`) + **capa de repositorio**. `App.Services` resuelve los ViewModels; los repositorios usan `IDbContextFactory`. |
| **A1** | Operaciones de BD **async/await** end-to-end (repositorios `…Async`, comandos `[RelayCommand]` asíncronos). |
| **A6** | **`appsettings.json`** + `AppConfig` (cadena de conexión y política de contraseñas configurables). |
| **M3** | **Proyecto de pruebas xUnit** (`FarmaGestion.Tests`): **16 pruebas en verde** sobre SQLite en memoria (auth, repositorios de medicamentos/usuarios y transacción de despacho). |
| **B3** | Reportes **reactivos**: se regeneran al cambiar las fechas. |

**Archivos nuevos (fase 2):** `Data/Repositories/*` (Medicamento, Usuario, Despacho + interfaces),
`Services/AppConfig.cs`, `Services/ServiceConfiguration.cs`, `appsettings.json`, y el proyecto `FarmaGestion.Tests/`.

> **Verificación:** `dotnet build` → 0 errores / 0 advertencias · `dotnet test` → **16/16 correctas**.

### Pendiente (única tarea mayor restante)
- **M7** — Almacenar montos como `decimal` sin convertir a `double` (requiere migración de esquema y conversión de datos; se dejó fuera por el riesgo sobre datos existentes).

> ⚠️ **Nota de migración de datos:** la base de datos ahora vive en la carpeta de datos del
> usuario, no junto al ejecutable. La BD anterior del repositorio ya no se usa; en el primer
> arranque se crea una nueva y se genera la contraseña de admin en `credenciales_admin.txt`.

---

## 1. Resumen ejecutivo

FarmaGestion es una aplicación de escritorio (Avalonia/MVVM) para gestionar el inventario
de una farmacia: medicamentos, despachos (ventas), reportes y usuarios. La base técnica
es **moderna y correcta** (MVVM con CommunityToolkit, hashing de contraseñas con PBKDF2,
modelo de datos con relaciones bien definidas), pero existen **brechas importantes de
seguridad, robustez y arquitectura** que conviene cerrar antes de considerarla lista para
producción.

| Aspecto | Estado actual |
|---|---|
| **Listo como MVP** | ~60% (autenticación y CRUD básico funcionan) |
| **Listo para producción** | ~30% (falta endurecimiento, manejo de errores, pruebas) |
| **Listo a nivel empresa** | ~10% (falta observabilidad, auditoría, escalabilidad) |

### Lo que ya está bien hecho ✅
- MVVM moderno con `[ObservableProperty]` y `[RelayCommand]`.
- Hashing de contraseñas robusto: **PBKDF2-SHA256**, salt de 128 bits, 100 000 iteraciones, comparación en tiempo constante.
- Modelo de entidades limpio con relaciones (Despacho → DetalleDespacho).
- Importación de CSV asíncrona.
- Bindings compilados (`x:DataType`) y tema Fluent.

### Brechas críticas ❌
- Archivo de base de datos `.db` versionado en Git.
- Contraseña de administrador **hardcodeada** en el código fuente.
- **Sin control de acceso por roles** (los roles existen pero no se usan).
- Llamadas a base de datos **bloqueantes** en el hilo de la UI.
- Sin manejo de errores, sin logging y sin pruebas.
- Sin inyección de dependencias ni capa de repositorio.

---

## 2. Hallazgos detallados

### 🔴 Críticos (seguridad / integridad de datos)

| # | Hallazgo | Ubicación | Impacto |
|---|---|---|---|
| C1 | **Base de datos versionada en Git.** `inventario_medicamentos.db` está rastreado; `.gitignore` no excluye `*.db`. | `FarmaGestion/inventario_medicamentos.db`, `.gitignore` | Conflictos de merge, fuga de datos, esquema desincronizado. |
| C2 | **Contraseña de admin hardcodeada** (`admin` / `Admin123!`), visible en código e historial de Git. | `Services/AuthService.cs:71` | Cualquiera con acceso al repo tiene credenciales de administrador. |
| C3 | **Sin transacción en el despacho.** Se descuenta inventario y se crea el despacho sin transacción explícita. | `ViewModels/DespachoViewModel.cs:70-116` | Si `SaveChanges` falla a la mitad, el inventario queda inconsistente. |
| C4 | **Sin control de acceso por roles (RBAC).** Los roles `Admin`/`Empleado` se guardan pero nunca se verifican; todos ven todas las pestañas. | `MainWindow`, todos los ViewModels | Un empleado puede crear/editar usuarios y modificar todo el inventario. |
| C5 | **Consulta N+1 en reportes.** Por cada medicamento se consultan todos los detalles. | `ViewModels/ReportesViewModel.cs:39-46` | Degradación grave de rendimiento con muchos productos. |

### 🟡 Altos (robustez / experiencia)

| # | Hallazgo | Ubicación | Impacto |
|---|---|---|---|
| A1 | **DB bloquea la UI.** Casi todas las consultas son síncronas en el hilo de UI; solo el CSV es async. | `LoginViewModel.cs:39`, `ProductoViewModel.cs:34`, etc. | La interfaz se congela con operaciones lentas o importaciones grandes. |
| A2 | **Sin manejo de errores en `SaveChanges()`.** Ninguna llamada está en try-catch. | `InventarioViewModel.cs:114`, `DespachoViewModel.cs:116`, `UsuarioFormViewModel.cs:160` | Excepciones de BD hacen crashear la app. |
| A3 | **Sin logging ni auditoría.** No hay `ILogger`; hay `Console.WriteLine` en código de producción. | `DespachoViewModel.cs:75` | Imposible depurar incidentes; sin rastro de quién hizo qué. |
| A4 | **Sin validación de unicidad de código de barras.** No se verifica antes de insertar; el `parse` de Precio/Existencias no está protegido. | `ViewModels/ProductoViewModel.cs:23-28` | La restricción única de la BD lanza excepción no controlada. |
| A5 | **Ruta de BD hardcodeada** al directorio de trabajo. | `Data/InventarioContext.cs:19` | Falla si la app se ejecuta desde otra carpeta (instalador, acceso directo). |
| A6 | **Sin configuración externa** (no hay `appsettings.json`). Ruta de BD y contraseña en código. | — | Imposible parametrizar por entorno. |

### 🟠 Medios (mantenibilidad)

| # | Hallazgo | Ubicación |
|---|---|---|
| M1 | Política de contraseñas débil (mínimo 6 caracteres, sin complejidad). | `UsuarioFormViewModel.cs:94` |
| M2 | Código duplicado en la creación de la ventana de login y su callback. | `App.axaml.cs:29-50`, `MainWindow.axaml.cs:29-47` |
| M3 | **Cero pruebas** (unitarias o de integración). | — |
| M4 | `Database.Migrate()` se llama en cada carga de inventario, no solo al inicio. | `InventarioViewModel.cs:40` y `App.axaml.cs:29` |
| M5 | Los ViewModels crean `InventarioContext` directamente; no hay repositorio ni unidad de trabajo. | Todos los ViewModels |
| M6 | Roles como *magic strings* (`"Admin"`, `"Empleado"`); no hay enum. | `UsuarioFormViewModel.cs:32`, `Usuario.cs:11` |
| M7 | `decimal` se almacena como `double` en SQLite → posible pérdida de precisión en cálculos financieros. | `InventarioContext.cs:38-47` |

### 🔵 Bajos (pulido / diseño)

- B1 — Modelos sin atributos de validación (`[Required]`, `[Range]`, `[StringLength]`).
- B2 — `ViewLocator` falla en silencio devolviendo un `TextBlock`.
- B3 — Reportes no reactivos: cambiar fechas no regenera; hay que pulsar el botón.
- B4 — Manejo de foco frágil basado en nombres de controles (`TxtCodigoBarras`, etc.).
- B5 — Sin internacionalización (textos fijos en español en el código).

---

## 3. Hoja de ruta por fases

### Fase 1 — Seguridad y estabilidad *(prioridad máxima)*
**Objetivo:** cerrar las brechas que ponen en riesgo datos y operación.

- [ ] **C1** Quitar `inventario_medicamentos.db` del control de versiones y añadir `*.db`, `*.db-shm`, `*.db-wal` a `.gitignore`.
  ```bash
  git rm --cached FarmaGestion/inventario_medicamentos.db
  ```
- [ ] **C2** Eliminar la contraseña hardcodeada: generar una aleatoria en el primer arranque o forzar un asistente de configuración inicial que obligue a cambiarla.
- [ ] **C3** Envolver la confirmación de despacho en una transacción de EF Core (`context.Database.BeginTransaction()`), con commit/rollback.
- [ ] **C4** Implementar RBAC: ocultar/deshabilitar pestañas y acciones según el rol; verificar permisos en los ViewModels (vía `CanExecute` o un servicio de autorización).
- [ ] **A2** Añadir try-catch alrededor de cada `SaveChanges()` con mensajes de error amigables.
- [ ] **A1** Convertir las operaciones de BD a `async/await` y usar comandos asíncronos de CommunityToolkit (`[RelayCommand]` sobre métodos `async Task`).

### Fase 2 — Calidad de código *(impacto medio)*
**Objetivo:** hacer el código testeable y observable.

- [ ] **M5** Introducir inyección de dependencias (`Microsoft.Extensions.DependencyInjection`) y registrar `DbContext`, servicios y ViewModels.
- [ ] **M5** Crear una capa de abstracción `IRepository<T>` / unidad de trabajo para desacoplar los ViewModels de EF Core.
- [ ] **A3** Añadir logging estructurado (Serilog o `Microsoft.Extensions.Logging`) y un rastro de auditoría (quién creó/editó medicamentos, despachos, usuarios).
- [ ] **C5** Reescribir el reporte con una sola consulta `GroupBy` + `Include` para eliminar el N+1.
- [ ] **M3** Crear proyecto de pruebas (xUnit) cubriendo: autenticación, importación de CSV y confirmación de despacho.
- [ ] **A4** Validar unicidad de código de barras antes de insertar y proteger los `parse`.

### Fase 3 — Mantenibilidad *(deseable)*
- [ ] **A6** Añadir `appsettings.json` con `IConfiguration` (ruta de BD, parámetros).
- [ ] **A5** Resolver la ruta de la BD desde una carpeta de datos de aplicación bien definida.
- [ ] **M6** Reemplazar los *magic strings* de roles por un `enum UserRole`.
- [ ] **M2** Extraer la creación de la ventana de login a un helper/servicio de navegación.
- [ ] **M1** Reforzar la política de contraseñas (8+ caracteres, mayúscula + número).
- [ ] **M4** Llamar `Database.Migrate()` solo una vez, al inicio de la app.
- [ ] **M7** Evaluar almacenar montos como `decimal` con conversor explícito para evitar pérdida de precisión.
- [ ] **B1** Añadir atributos de validación a los modelos.

### Fase 4 — UX y funcionalidades *(post-MVP)*
- [ ] Alertas de stock bajo y de medicamentos próximos a vencer (ya existe `FechaVencimiento`).
- [ ] Edición y eliminación (borrado lógico) de productos y usuarios con auditoría.
- [ ] Integración con lector de código de barras (hardware).
- [ ] Reportes reactivos (regenerar al cambiar fechas) y exportación (PDF/Excel).
- [ ] Internacionalización (i18n).

---

## 4. Quick wins (1–2 horas)

Cambios de bajo esfuerzo y alto valor para empezar hoy mismo:

1. **Sacar la `.db` de Git** y actualizar `.gitignore` *(C1)*.
2. **Quitar el `Console.WriteLine`** de `DespachoViewModel.cs:75` *(A3)*.
3. **Mover `Database.Migrate()`** a un único punto de arranque *(M4)*.
4. **try-catch en los `SaveChanges()`** más críticos (login y despacho) *(A2/C3)*.
5. **Definir `enum UserRole`** y reemplazar los strings *(M6)*.

---

## 5. Priorización (impacto vs. esfuerzo)

```
Alto impacto
   │  C2 ·          C3 ·          C4 ·
   │  C1 ·  A2 ·            A1 ·
   │       A3 ·     C5 ·         M5 ·
   │  M4 · A4 ·          M3 ·
   │       M6 ·    A6 · A5 ·
   │  M2 ·    M1 ·        M7 ·
   │            B1·B3·B4·B5 ·
Bajo│________________________________
    Bajo esfuerzo            Alto esfuerzo
```

**Empezar por:** C1, C2, M4, A2 (alto impacto / bajo esfuerzo) → luego C3, C4, A1 (alto impacto / esfuerzo medio).

---

*Generado a partir de una inspección directa del código en `FarmaGestion/`. Las referencias `archivo:línea` corresponden al estado del repositorio en la fecha indicada.*
