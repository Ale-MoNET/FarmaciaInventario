@echo off
setlocal
title Desinstalador de FarmaGestion

rem ------------------------------------------------------------------
rem  Desinstalador de FarmaGestion para Windows
rem  - Quita la app y los accesos directos.
rem  - NO borra los datos (base de datos, log, credenciales) en %AppData%\FarmaGestion.
rem  - Si la app se instalo para todos los usuarios, ejecutar como administrador.
rem ------------------------------------------------------------------

set "APP=FarmaGestion"
set "ENCONTRADO=0"

echo.
echo  ==========================================
echo    Desinstalador de FarmaGestion
echo  ==========================================
echo.

tasklist /FI "IMAGENAME eq %APP%.exe" 2>nul | find /I "%APP%.exe" >nul
if not errorlevel 1 (
    echo  ERROR: %APP% esta abierto. Cierralo y vuelve a ejecutar el desinstalador.
    echo.
    pause
    exit /b 1
)

for %%D in ("%ProgramFiles%\%APP%" "%LocalAppData%\Programs\%APP%") do (
    if exist "%%~D\%APP%.exe" (
        set "ENCONTRADO=1"
        echo  Eliminando %%~D ...
        rd /s /q "%%~D" 2>nul
        if exist "%%~D\%APP%.exe" echo  AVISO: no se pudo eliminar %%~D. Ejecuta este archivo como administrador.
    )
)

echo  Quitando accesos directos...
powershell -NoProfile -ExecutionPolicy Bypass -Command "foreach ($carpeta in 'Desktop','Programs','CommonDesktopDirectory','CommonPrograms') { Remove-Item -LiteralPath (Join-Path ([Environment]::GetFolderPath($carpeta)) '%APP%.lnk') -ErrorAction SilentlyContinue }" >nul 2>&1

echo.
if "%ENCONTRADO%"=="0" (
    echo  No se encontro ninguna instalacion de %APP% en esta PC.
) else (
    echo  Desinstalacion completada.
)
echo.
echo  Los datos NO se borran. Si ya no los necesitas, elimina a mano la carpeta:
echo    %AppData%\%APP%
echo  Contiene la base de datos inventario_medicamentos.db: respaldala antes.
echo.
pause
endlocal
exit /b 0
