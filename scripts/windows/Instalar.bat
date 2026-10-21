@echo off
setlocal
title Instalador de FarmaGestion

rem ------------------------------------------------------------------
rem  Instalador de FarmaGestion para Windows
rem  - Copia la app a la PC y crea accesos directos.
rem  - Doble clic: instala solo para el usuario actual (sin clave admin).
rem  - "Ejecutar como administrador": instala para todos los usuarios.
rem  - Sirve tambien para actualizar: conserva appsettings.json y los datos.
rem ------------------------------------------------------------------

set "APP=FarmaGestion"
set "ORIGEN=%~dp0"

echo.
echo  ==========================================
echo    Instalador de FarmaGestion
echo  ==========================================
echo.

if not exist "%ORIGEN%%APP%.exe" (
    echo  ERROR: no se encuentra %APP%.exe junto a este instalador.
    echo  Extrae TODO el contenido del .zip a una carpeta y vuelve a ejecutar Instalar.bat.
    echo.
    pause
    exit /b 1
)

tasklist /FI "IMAGENAME eq %APP%.exe" 2>nul | find /I "%APP%.exe" >nul
if not errorlevel 1 (
    echo  ERROR: %APP% esta abierto. Cierralo y vuelve a ejecutar el instalador.
    echo.
    pause
    exit /b 1
)

rem Con permisos de administrador se instala para todos los usuarios de la PC;
rem sin ellos, solo para el usuario actual.
net session >nul 2>&1
if %errorlevel%==0 (
    set "DESTINO=%ProgramFiles%\%APP%"
    set "CARPETA_ESCRITORIO=CommonDesktopDirectory"
    set "CARPETA_MENU=CommonPrograms"
    set "ALCANCE=todos los usuarios de esta PC"
) else (
    set "DESTINO=%LocalAppData%\Programs\%APP%"
    set "CARPETA_ESCRITORIO=Desktop"
    set "CARPETA_MENU=Programs"
    set "ALCANCE=el usuario actual"
)

echo  Instalando para %ALCANCE% en:
echo    %DESTINO%
echo.

if not exist "%DESTINO%" mkdir "%DESTINO%"
if not exist "%DESTINO%" (
    echo  ERROR: no se pudo crear la carpeta de destino.
    echo.
    pause
    exit /b 1
)

copy /Y "%ORIGEN%%APP%.exe" "%DESTINO%\" >nul
if errorlevel 1 (
    echo  ERROR: no se pudo copiar %APP%.exe a %DESTINO%.
    echo.
    pause
    exit /b 1
)
copy /Y "%ORIGEN%LEEME.txt" "%DESTINO%\" >nul 2>&1

rem appsettings.json solo se copia si no existe, para conservar la configuracion
rem (p. ej. la ruta de una base de datos compartida) al actualizar la app.
if not exist "%DESTINO%\appsettings.json" copy /Y "%ORIGEN%appsettings.json" "%DESTINO%\" >nul 2>&1

rem Quita la marca "descargado de internet" del exe para reducir el aviso de SmartScreen.
powershell -NoProfile -ExecutionPolicy Bypass -Command "Unblock-File -LiteralPath '%DESTINO%\%APP%.exe' -ErrorAction SilentlyContinue" >nul 2>&1

echo  Creando accesos directos...
powershell -NoProfile -ExecutionPolicy Bypass -Command "$ws = New-Object -ComObject WScript.Shell; foreach ($carpeta in '%CARPETA_ESCRITORIO%','%CARPETA_MENU%') { $ruta = Join-Path ([Environment]::GetFolderPath($carpeta)) '%APP%.lnk'; $s = $ws.CreateShortcut($ruta); $s.TargetPath = '%DESTINO%\%APP%.exe'; $s.WorkingDirectory = '%DESTINO%'; $s.IconLocation = '%DESTINO%\%APP%.exe,0'; $s.Description = 'FarmaGestion - Inventario de farmacia'; $s.Save() }"
if errorlevel 1 echo  AVISO: no se pudieron crear los accesos directos. Abre la app desde %DESTINO%\%APP%.exe

echo.
echo  ==========================================
echo    Instalacion completada
echo  ==========================================
echo.
echo  Accesos directos "FarmaGestion" en el Escritorio y en el menu Inicio.
echo.
echo  PRIMER INICIO: la app crea la base de datos y el usuario "admin" con una
echo  clave aleatoria que queda guardada en:
echo    %AppData%\%APP%\credenciales_admin.txt
echo  Entra con esa clave, cambiala y borra el archivo.
echo.
choice /C SN /N /M "  Abrir FarmaGestion ahora? (S/N): "
if errorlevel 2 goto :fin
start "" "%DESTINO%\%APP%.exe"

:fin
echo.
endlocal
exit /b 0
