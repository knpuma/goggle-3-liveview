#!/bin/zsh
set -euo pipefail

project_root="${0:A:h:h}"
app_path="$project_root/outputs/Goggle 3 LiveView.app"

cd "$project_root"
swift build -c release --product Goggles3Mac
swift build -c release --product goggles-rndis-probe
if [[ ! -x "$project_root/third_party/ffmpeg/ffmpeg" ]]; then
  zsh "$project_root/scripts/build-decoder.sh"
fi
mkdir -p "$app_path/Contents/MacOS"
mkdir -p "$app_path/Contents/Resources/Licenses"
cp ".build/release/Goggles3Mac" "$app_path/Contents/MacOS/Goggle3LiveView"
cp ".build/release/goggles-rndis-probe" "$app_path/Contents/MacOS/goggles-rndis-probe"
cp "third_party/ffmpeg/ffmpeg" "$app_path/Contents/MacOS/g3-decoder"
cp "third_party/ffmpeg/COPYING.LGPLv2.1" "$app_path/Contents/Resources/Licenses/FFmpeg-LGPL-2.1.txt"
cp "THIRD_PARTY_NOTICES.md" "LICENSE" "$app_path/Contents/Resources/Licenses/"
cp "assets/AppIcon.icns" "$app_path/Contents/Resources/AppIcon.icns"
cp "AppInfo.plist" "$app_path/Contents/Info.plist"
codesign --force --sign - "$app_path/Contents/MacOS/goggles-rndis-probe"
codesign --force --sign - "$app_path/Contents/MacOS/g3-decoder"
codesign --force --sign - "$app_path"
echo "App creada: $app_path"
