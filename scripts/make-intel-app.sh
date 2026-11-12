#!/bin/zsh
set -euo pipefail

project_root="${0:A:h:h}"
app_path="$project_root/outputs/mac-intel/Goggle 3 LiveView.app"

cd "$project_root"
build_flags=(-Xswiftc -file-prefix-map -Xswiftc "$project_root=/src" -Xcc "-ffile-prefix-map=$project_root=/src")
swift build -c release --arch x86_64 --product Goggles3Mac "${build_flags[@]}"
swift build -c release --arch x86_64 --product goggles-rndis-probe "${build_flags[@]}"
bin_path="$(swift build -c release --arch x86_64 --show-bin-path)"
decoder="$project_root/third_party/ffmpeg/ffmpeg-intel"
decoder_archs="$(lipo -archs "$decoder" 2>/dev/null || true)"
if [[ ! -x "$decoder" || "$decoder_archs" != *x86_64* ]]; then
  zsh "$project_root/scripts/build-decoder-intel.sh"
fi
rm -rf "$app_path"
mkdir -p "$app_path/Contents/MacOS"
mkdir -p "$app_path/Contents/Resources/Licenses"
cp "$bin_path/Goggles3Mac" "$app_path/Contents/MacOS/Goggle3LiveView"
cp "$bin_path/goggles-rndis-probe" "$app_path/Contents/MacOS/goggles-rndis-probe"
cp "third_party/ffmpeg/ffmpeg-intel" "$app_path/Contents/MacOS/g3-decoder"
cp "third_party/ffmpeg/COPYING.LGPLv2.1" "$app_path/Contents/Resources/Licenses/FFmpeg-LGPL-2.1.txt"
cp "THIRD_PARTY_NOTICES.md" "LICENSE" "$app_path/Contents/Resources/Licenses/"
cp "assets/AppIcon.icns" "$app_path/Contents/Resources/AppIcon.icns"
cp "AppInfo.plist" "$app_path/Contents/Info.plist"
strip -S "$app_path/Contents/MacOS/Goggle3LiveView" "$app_path/Contents/MacOS/goggles-rndis-probe"
codesign --force --sign - "$app_path/Contents/MacOS/goggles-rndis-probe"
codesign --force --sign - "$app_path/Contents/MacOS/g3-decoder"
codesign --force --sign - "$app_path"
echo "App created: $app_path"
