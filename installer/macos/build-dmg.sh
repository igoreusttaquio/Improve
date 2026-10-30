#!/usr/bin/env bash
# Monta improve.app e o .dmg a partir da saída do dotnet publish (osx-arm64 ou osx-x64). Roda no macOS.
# Uso: installer/macos/build-dmg.sh <versão> <pasta do publish> <arm64|x64> <pasta de saída>
# MACOS_SIGN_IDENTITY (opcional): identidade "Developer ID Application: …" para assinar; sem ela, assinatura ad-hoc.
set -euo pipefail

version="$1"
publish_dir="$2"
arch="$3"
output_dir="$4"
root="$(cd "$(dirname "$0")/../.." && pwd)"
work="$(mktemp -d)"
trap 'rm -rf "$work"' EXIT

app="$work/improve.app"
mkdir -p "$app/Contents/MacOS" "$app/Contents/Resources"
cp -a "$publish_dir/." "$app/Contents/MacOS/"
find "$app/Contents/MacOS" -name '*.pdb' -delete
chmod 755 "$app/Contents/MacOS/improve"
cp "$root/src/Improve.App/Assets/AppIcon/improve.icns" "$app/Contents/Resources/improve.icns"
sed "s/__VERSION__/${version}/g" "$root/installer/macos/Info.plist" > "$app/Contents/Info.plist"

# Apple Silicon não executa binários sem assinatura: no mínimo, ad-hoc.
identity="${MACOS_SIGN_IDENTITY:--}"
codesign --force --deep --options runtime --timestamp=none --sign "$identity" "$app"

# DMG com o app e um atalho para Aplicativos.
dmg_root="$work/dmg"
mkdir -p "$dmg_root"
cp -a "$app" "$dmg_root/"
ln -s /Applications "$dmg_root/Applications"
mkdir -p "$output_dir"
hdiutil create -volname "improve" -srcfolder "$dmg_root" -ov -format UDZO "$output_dir/improve-macos-${arch}.dmg"
echo "Gerado: $output_dir/improve-macos-${arch}.dmg"
