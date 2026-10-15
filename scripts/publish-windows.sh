#!/bin/zsh
set -euo pipefail
project_root="${0:A:h:h}"
architecture="${1:-x64}"
[[ "$architecture" == x64 || "$architecture" == arm64 ]] || exit 1
dotnet_command="${GOGGLES_DOTNET:-$project_root/work/microsoft-dotnet/dotnet}"
output="$project_root/outputs/Goggle-3-LiveView-Windows-$architecture"
DOTNET_CLI_TELEMETRY_OPTOUT=1 "$dotnet_command" publish \
  "$project_root/Windows/Goggles3.Windows/Goggles3.Windows.csproj" -c Release \
  -r "win-$architecture" --self-contained true -p:PublishSingleFile=false -o "$output"
mkdir -p "$output/ffmpeg"
cp "$project_root/Windows/third_party/ffmpeg-$architecture/ffmpeg.exe" "$output/ffmpeg/"
cp "$project_root/Windows/third_party/ffmpeg-$architecture/COPYING.LGPLv2.1" "$output/ffmpeg/"
cp "$project_root/LICENSE" "$project_root/THIRD_PARTY_NOTICES.md" "$output/"
mkdir -p "$output/Licenses"
runtime_root="${NUGET_PACKAGES:-$HOME/.nuget/packages}"
cp "$runtime_root/microsoft.netcore.app.runtime.win-$architecture/10.0.13/LICENSE.TXT" "$output/Licenses/DotNET-LICENSE.txt"
cp "$runtime_root/microsoft.netcore.app.runtime.win-$architecture/10.0.13/THIRD-PARTY-NOTICES.TXT" "$output/Licenses/DotNET-THIRD-PARTY-NOTICES.txt"
cp "$runtime_root/microsoft.windowsdesktop.app.runtime.win-$architecture/10.0.13/LICENSE" "$output/Licenses/WPF-LICENSE.txt"
cp "$project_root/Windows/README.md" "$output/README.md"
echo "Windows $architecture: $output"
