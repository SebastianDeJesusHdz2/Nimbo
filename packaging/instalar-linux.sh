#!/bin/sh
set -eu
src=${1:?Indica la ruta del archivo AppImage de Nimbo}
dest="${XDG_DATA_HOME:-$HOME/.local/share}/Nimbo"
apps="${XDG_DATA_HOME:-$HOME/.local/share}/applications"
mkdir -p "$dest" "$apps"
install -m 755 "$src" "$dest/Nimbo.AppImage.new"
mv -f "$dest/Nimbo.AppImage.new" "$dest/Nimbo.AppImage"
cat > "$apps/nimbo.desktop" <<EOF
[Desktop Entry]
Type=Application
Name=Nimbo
Exec="$dest/Nimbo.AppImage"
Terminal=false
Categories=Office;WordProcessor;
EOF
echo "Nimbo instalado en el menú de aplicaciones."

