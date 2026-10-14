#!/bin/zsh
set -euo pipefail
project_root="${0:A:h:h}"
delivery_dir="${1:?Indica la carpeta de entrega}"
ffmpeg_source="${2:?Indica ffmpeg-8.0.3.tar.xz}"
app="$project_root/dist/Goggle 3 LiveView Multi.app"
[[ -d "$app" && -f "$ffmpeg_source" ]]
[[ ! -e "$delivery_dir/Goggle-3-LiveView-Multi-Mac.dmg" ]]
[[ ! -e "$delivery_dir/Goggle 3 LiveView Multi.app" ]]
codesign --verify --deep --strict "$app"
# Fail if development videos accidentally enter a release bundle.
if find "$app" -type f \( -iname '*.h264' -o -iname '*.mp4' \) | /usr/bin/grep -q .; then
  echo 'El paquete contiene vídeos de desarrollo.' >&2
  exit 1
fi
mkdir -p "$project_root/work" "$delivery_dir"
package_dir="$(mktemp -d "$project_root/work/dmg.XXXXXX")"
ditto "$app" "$package_dir/Goggle 3 LiveView Multi.app"
ln -s /Applications "$package_dir/Aplicaciones"
cp "$project_root/GUIA.md" "$package_dir/LEEME.md"
mkdir -p "$package_dir/Licencias"
cp "$project_root/LICENSE" "$project_root/THIRD_PARTY_NOTICES.md" "$project_root/third_party/ffmpeg/COPYING.LGPLv2.1" "$ffmpeg_source" "$package_dir/Licencias/"
hdiutil create -volname 'Goggle 3 LiveView Multi' -srcfolder "$package_dir" -format UDZO "$delivery_dir/Goggle-3-LiveView-Multi-Mac.dmg"
hdiutil verify "$delivery_dir/Goggle-3-LiveView-Multi-Mac.dmg"
ditto "$app" "$delivery_dir/Goggle 3 LiveView Multi.app"
cp "$project_root/GUIA.md" "$delivery_dir/Goggle-3-LiveView-Multi-Guia.md"
