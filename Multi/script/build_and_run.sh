#!/bin/zsh
set -euo pipefail
project_root="${0:A:h:h}"
mode="${1:-run}"
case "$mode" in run|--verify|--debug|--logs|--telemetry|--build-only) ;; *) exit 2;; esac
cd "$project_root"
# Exact experimental executable only. Never terminate the published app.
if pgrep -x Goggles3Multi >/dev/null; then
  echo 'Cierra Goggle 3 LiveView Multi antes de reconstruir para finalizar sus grabaciones.' >&2
  exit 1
fi
swift build -c release -Xswiftc -file-prefix-map -Xswiftc "$project_root=/src" -Xcc "-ffile-prefix-map=$project_root=/src"
mkdir -p "$project_root/work" "$project_root/dist"
build_dir="$(mktemp -d "$project_root/work/package.XXXXXX")"
app="$build_dir/Goggle 3 LiveView Multi.app"
mkdir -p "$app/Contents/MacOS" "$app/Contents/Resources/Licenses"
cp .build/release/Goggles3Multi "$app/Contents/MacOS/"
cp .build/release/goggles-multi-usb "$app/Contents/MacOS/"
cp third_party/ffmpeg/ffmpeg "$app/Contents/MacOS/g3-decoder"
cp AppInfo.plist "$app/Contents/Info.plist"
cp assets/AppIcon.icns "$app/Contents/Resources/"
cp LICENSE THIRD_PARTY_NOTICES.md third_party/ffmpeg/COPYING.LGPLv2.1 "$app/Contents/Resources/Licenses/"
strip -S "$app/Contents/MacOS/Goggles3Multi" "$app/Contents/MacOS/goggles-multi-usb"
codesign --force --sign - "$app/Contents/MacOS/goggles-multi-usb"
codesign --force --sign - "$app/Contents/MacOS/g3-decoder"
codesign --force --sign - "$app"
destination="$project_root/dist/Goggle 3 LiveView Multi.app"
if [[ -e "$destination" ]]; then mv "$destination" "$build_dir/previous.app"; fi
mv "$app" "$destination"
app="$destination"
case "$mode" in
  --build-only) echo "$app" ;;
  --debug) lldb -- "$app/Contents/MacOS/Goggles3Multi" ;;
  --logs|--telemetry) open "$app"; /usr/bin/log stream --info --predicate 'process == "Goggles3Multi"' ;;
  --verify) open "$app"; sleep 1; pgrep -x Goggles3Multi ;;
  run) open "$app" ;;
esac
