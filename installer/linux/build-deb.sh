#!/usr/bin/env bash
# Monta o pacote .deb do improve a partir da saída do dotnet publish (linux-x64).
# Uso: installer/linux/build-deb.sh <versão> <pasta do publish> <pasta de saída>
# DEB_MAINTAINER (opcional) preenche o campo Maintainer, ex.: "Nome <email>".
set -euo pipefail

version="$1"
publish_dir="$2"
output_dir="$3"
root="$(cd "$(dirname "$0")/../.." && pwd)"
stage="$(mktemp -d)"
trap 'rm -rf "$stage"' EXIT

# Programa em /opt/improve, comando em /usr/bin.
mkdir -p "$stage/opt/improve" "$stage/usr/bin" "$stage/usr/share/applications" "$stage/DEBIAN"
cp -a "$publish_dir/." "$stage/opt/improve/"
find "$stage/opt/improve" -name '*.pdb' -delete
chmod 755 "$stage/opt/improve/improve"
ln -s ../../opt/improve/improve "$stage/usr/bin/improve"

# Entrada no menu de aplicativos e ícones nos tamanhos do tema hicolor.
cp "$root/installer/linux/improve.desktop" "$stage/usr/share/applications/improve.desktop"
for size in 16 24 32 48 64 128 256 512; do
  icon_dir="$stage/usr/share/icons/hicolor/${size}x${size}/apps"
  mkdir -p "$icon_dir"
  cp "$root/src/Improve.App/Assets/AppIcon/improve-${size}.png" "$icon_dir/improve.png"
done

installed_size="$(du -sk "$stage" | cut -f1)"
cat > "$stage/DEBIAN/control" <<EOF
Package: improve
Version: ${version}
Section: utils
Priority: optional
Architecture: amd64
Installed-Size: ${installed_size}
Depends: libx11-6, libice6, libsm6, libfontconfig1, libxrandr2, libxi6, libxcursor1
Maintainer: ${DEB_MAINTAINER:-improve}
Description: Aplica prompts de IA ao texto selecionado em qualquer app
 Selecione um texto, aperte o atalho global e escolha uma extensão:
 o resultado substitui a seleção ou aparece como sugestão.
EOF

mkdir -p "$output_dir"
dpkg-deb --build --root-owner-group "$stage" "$output_dir/improve-linux-amd64.deb"
echo "Gerado: $output_dir/improve-linux-amd64.deb"
