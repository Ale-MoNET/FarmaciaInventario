#!/bin/bash

# Script para compilar y ejecutar FarmaGestión con el lector de barras integrado

set -e

PROJECT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
CSPROJ="$PROJECT_DIR/FarmaGestion/FarmaGestion.csproj"

echo "════════════════════════════════════════════════════════════"
echo "🚀 FarmaGestión - Sistema de Escaneo de Códigos de Barras"
echo "════════════════════════════════════════════════════════════"
echo ""
echo "📁 Directorio del proyecto: $PROJECT_DIR"
echo ""

# Verificar que dotnet está instalado
if ! command -v dotnet &> /dev/null; then
    echo "❌ Error: dotnet no encontrado en el PATH"
    echo ""
    echo "Soluciones:"
    echo "1. Instala .NET desde: https://dotnet.microsoft.com/download"
    echo "2. O abre el proyecto en Visual Studio/Rider y presiona Run"
    echo ""
    exit 1
fi

DOTNET_VERSION=$(dotnet --version)
echo "✅ .NET encontrado: $DOTNET_VERSION"
echo ""

# Limpiar y compilar
echo "🔨 Compilando el proyecto..."
echo "────────────────────────────────────────────────────────────"

cd "$PROJECT_DIR"

# Restaurar paquetes
echo "📦 Restaurando paquetes NuGet..."
dotnet restore FarmaGestion/FarmaGestion.csproj

# Compilar
echo ""
echo "🏗️ Compilando..."
dotnet build FarmaGestion/FarmaGestion.csproj --configuration Release

echo ""
echo "════════════════════════════════════════════════════════════"
echo "✅ Compilación exitosa"
echo "════════════════════════════════════════════════════════════"
echo ""
echo "🎮 Iniciando FarmaGestión..."
echo ""
echo "💡 INSTRUCCIONES PARA PROBAR EL LECTOR DE BARRAS:"
echo "────────────────────────────────────────────────────────────"
echo "1. Inicia sesión en la aplicación"
echo "2. Ve a la sección 'Inventario'"
echo "3. Haz clic en el campo 'Buscar o escanear código de barras…'"
echo "4. Escribe o escanea un código de barras"
echo "5. La tabla se filtrará automáticamente ✨"
echo ""
echo "Ejemplos para probar:"
echo "  • Escribe: 7501  (para ver productos con ese prefijo)"
echo "  • Escribe: Para  (para ver medicamentos que contienen 'Para')"
echo "  • Escanea un código real con tu lector"
echo ""
echo "════════════════════════════════════════════════════════════"
echo ""

# Ejecutar la aplicación
dotnet run --project FarmaGestion/FarmaGestion.csproj --configuration Release

echo ""
echo "👋 Aplicación cerrada"
