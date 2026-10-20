#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/../.."
version=0.3.3
dist="$PWD/outputs/linux"
stage="$(mktemp -d -t goggle-linux.XXXXXXXX)"
app="$stage/Goggle-3-LiveView-Linux-x64"
mkdir -p "$dist" "$app/Licenses"
dotnet publish Linux/Goggle3.Linux/Goggle3.Linux.csproj -c Release -r linux-x64 --self-contained true \
  -p:DebugType=None -p:DebugSymbols=false -o "$app"
cp LICENSE "$app/LICENSE.txt"
cp Linux/README.md "$app/README.md"
cp Linux/README.es.md "$app/LEEME.md"
cp Linux/scripts/prepare-usb.py "$app/prepare-usb.py"
cp Linux/Licenses/* "$app/Licenses/"
cp /usr/share/common-licenses/Apache-2.0 "$app/Licenses/Apache-2.0.txt"
# Preserve notices supplied by NuGet and the exact bundled .NET runtime.
nuget="${NUGET_PACKAGES:-$HOME/.nuget/packages}"
for dependency in skiasharp harfbuzzsharp skiasharp.nativeassets.linux harfbuzzsharp.nativeassets.linux microsoft.netcore.app.runtime.linux-x64; do
  while IFS= read -r -d '' notice; do
    relative="${notice#"$nuget/"}"
    cp "$notice" "$app/Licenses/${relative//\//_}"
  done < <(find "$nuget/$dependency" -type f \( -iname '*LICENSE*' -o -iname '*NOTICE*' \) -print0)
done
test -n "$(find "$app/Licenses" -name '*microsoft.netcore*' -print -quit)"
chmod 755 "$app/goggle-3-liveview"
tar -C "$stage" -czf "$dist/Goggle-3-LiveView-Linux-x64-$version.tar.gz" Goggle-3-LiveView-Linux-x64
deb="$stage/deb"
mkdir -p "$deb/DEBIAN" "$deb/usr/lib/goggle-3-liveview" "$deb/usr/bin" "$deb/usr/share/applications" "$deb/usr/share/icons/hicolor/256x256/apps" "$deb/usr/share/doc/goggle-3-liveview"
cp Linux/packaging/control "$deb/DEBIAN/control"
cp -a "$app/." "$deb/usr/lib/goggle-3-liveview/"
install -m 755 Linux/packaging/launch.sh "$deb/usr/bin/goggle-3-liveview"
install -m 644 Linux/packaging/goggle-3-liveview.desktop "$deb/usr/share/applications/"
install -m 644 docs/assets/icon.png "$deb/usr/share/icons/hicolor/256x256/apps/goggle-3-liveview.png"
cp LICENSE "$deb/usr/share/doc/goggle-3-liveview/copyright"
dpkg-deb --root-owner-group --build "$deb" "$dist/Goggle-3-LiveView-Linux-x64-$version.deb"
cd "$dist"
sha256sum ./*.deb ./*.tar.gz > SHA256SUMS-Linux.txt
