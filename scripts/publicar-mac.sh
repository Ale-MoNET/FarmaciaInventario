#!/usr/bin/env bash
# Publica FarmaGestión para macOS (Apple Silicon) y arma FarmaGestion.app en dist/mac/
set -euo pipefail

cd "$(dirname "$0")/.."   # raíz del repositorio

# --- localizar dotnet ---
if command -v dotnet >/dev/null 2>&1; then
  DOTNET="dotnet"
elif [ -x "/Applications/Rider.app/Contents/lib/ReSharperHost/macos-arm64/dotnet/dotnet" ]; then
  DOTNET="/Applications/Rider.app/Contents/lib/ReSharperHost/macos-arm64/dotnet/dotnet"
else
  echo "❌ No se encontró 'dotnet'. Instala el SDK de .NET o abre desde Rider." ; exit 1
fi

RID="osx-arm64"          # para Mac Intel usa: osx-x64
PROJ="FarmaGestion/FarmaGestion.csproj"
PUBDIR="FarmaGestion/bin/Release/net8.0/$RID/publish"
APP="dist/mac/FarmaGestion.app"
VERSION="$(sed -n 's:.*<Version>\(.*\)</Version>.*:\1:p' "$PROJ" | head -1)"
VERSION="${VERSION:-0.0.0}"

echo "🔨 Publicando ($RID)…"
"$DOTNET" publish "$PROJ" -c Release -r "$RID" --self-contained true \
  -p:PublishSingleFile=false -p:DebugType=none

echo "📦 Armando $APP…"
rm -rf "$APP"
mkdir -p "$APP/Contents/MacOS" "$APP/Contents/Resources"

# binarios + runtime + assets
cp -R "$PUBDIR/." "$APP/Contents/MacOS/"
# ícono
cp "FarmaGestion/Assets/FarmaGestion.icns" "$APP/Contents/Resources/FarmaGestion.icns"

# Info.plist
cat > "$APP/Contents/Info.plist" <<PLIST
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
  <key>CFBundleName</key>            <string>FarmaGestión</string>
  <key>CFBundleDisplayName</key>     <string>FarmaGestión</string>
  <key>CFBundleExecutable</key>      <string>FarmaGestion</string>
  <key>CFBundleIdentifier</key>      <string>com.farmagestion.app</string>
  <key>CFBundleIconFile</key>        <string>FarmaGestion</string>
  <key>CFBundlePackageType</key>     <string>APPL</string>
  <key>CFBundleShortVersionString</key> <string>$VERSION</string>
  <key>CFBundleVersion</key>         <string>$VERSION</string>
  <key>LSMinimumSystemVersion</key>  <string>11.0</string>
  <key>NSHighResolutionCapable</key> <true/>
</dict>
</plist>
PLIST

chmod +x "$APP/Contents/MacOS/FarmaGestion"

# Firma ad-hoc para que arranque sin el aviso de "app dañada" en este equipo.
codesign --force --deep --sign - "$APP" >/dev/null 2>&1 || echo "⚠️  No se pudo firmar (opcional)."

echo "✅ Listo: $APP"
echo "   Ábrela con doble clic. Para compartir por internet requiere firma/notarización de Apple Developer."
