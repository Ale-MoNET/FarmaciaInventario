#!/usr/bin/env bash
# Publica FarmaGestión para Windows como un único .exe autocontenido y arma el
# paquete de instalación listo para copiar a las PC:
#
#   dist/windows/FarmaGestion-<version>-win-x64/           carpeta: exe + instalador + guía
#   dist/windows/FarmaGestion-<version>-win-x64.zip        la misma carpeta comprimida
#   dist/windows/FarmaGestion-<version>-win-x64.zip.sha256 huella para verificar la copia
#
# Uso:
#   scripts/publicar-windows.sh               corre pruebas + publica + empaqueta
#   SKIP_TESTS=1 scripts/publicar-windows.sh  omite las pruebas
#   RID=win-x86  scripts/publicar-windows.sh  para PC de 32 bits
set -euo pipefail

cd "$(dirname "$0")/.."   # raíz del repositorio

if command -v dotnet >/dev/null 2>&1; then
  DOTNET="dotnet"
elif [ -x "/Applications/Rider.app/Contents/lib/ReSharperHost/macos-arm64/dotnet/dotnet" ]; then
  DOTNET="/Applications/Rider.app/Contents/lib/ReSharperHost/macos-arm64/dotnet/dotnet"
else
  echo "❌ No se encontró 'dotnet'." ; exit 1
fi

RID="${RID:-win-x64}"
PROJ="FarmaGestion/FarmaGestion.csproj"
TESTS="FarmaGestion.Tests/FarmaGestion.Tests.csproj"
PUBDIR="FarmaGestion/bin/Release/net8.0/$RID/publish"
VERSION="$(sed -n 's:.*<Version>\(.*\)</Version>.*:\1:p' "$PROJ" | head -1)"
VERSION="${VERSION:-0.0.0}"
OUT="dist/windows"
NOMBRE="FarmaGestion-${VERSION}-${RID}"
PKG="$OUT/$NOMBRE"

if [ "${SKIP_TESTS:-0}" != "1" ]; then
  echo "🧪 Ejecutando pruebas…"
  "$DOTNET" test "$TESTS" -c Release --nologo
fi

echo "🔨 Publicando FarmaGestión v$VERSION ($RID)…"
"$DOTNET" publish "$PROJ" -c Release -r "$RID" --self-contained true \
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=none

echo "📦 Armando paquete $PKG…"
rm -rf "$OUT"; mkdir -p "$PKG"
cp "$PUBDIR/FarmaGestion.exe" "$PKG/"
# appsettings.json va junto al exe (permite configurar una base de datos compartida).
cp "FarmaGestion/appsettings.json" "$PKG/"
# Instalador, desinstalador, guía y CSV de ejemplo para la PC destino.
cp scripts/windows/Instalar.bat scripts/windows/Desinstalar.bat "$PKG/"
cp scripts/LEEME.txt "$PKG/"
cp productos_ejemplo.csv "$PKG/"
# Archivos de texto con fin de línea CRLF (cmd.exe y Bloc de notas).
for f in "$PKG"/*.bat "$PKG"/*.txt "$PKG"/*.csv; do
  perl -pi -e 's/\r?\n/\r\n/' "$f"
done

(
  cd "$OUT"
  zip -r -X -q "$NOMBRE.zip" "$NOMBRE" -x '*.DS_Store'
  shasum -a 256 "$NOMBRE.zip" > "$NOMBRE.zip.sha256"
)

echo
echo "✅ Release v$VERSION ($RID) listo:"
echo "   $PKG/"
echo "   $OUT/$NOMBRE.zip  ($(du -h "$OUT/$NOMBRE.zip" | cut -f1))"
echo "   $OUT/$NOMBRE.zip.sha256"
echo "   En cada PC: extraer el zip y ejecutar Instalar.bat (ver LEEME.txt)."
