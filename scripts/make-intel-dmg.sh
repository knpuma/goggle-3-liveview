#!/bin/zsh
set -euo pipefail

project_root="${0:A:h:h}"
app_path="$project_root/outputs/mac-intel/Goggle 3 LiveView.app"
dmg_path="$project_root/outputs/Goggle-3-LiveView-Mac-Intel.dmg"

zsh "$project_root/scripts/make-intel-app.sh"
rm -f "$dmg_path"
hdiutil create \
  -volname "Goggle 3 LiveView" \
  -srcfolder "$app_path" \
  -format UDZO \
  -ov \
  "$dmg_path"

echo "Intel DMG created: $dmg_path"
