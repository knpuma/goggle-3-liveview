#!/usr/bin/env bash
set -euo pipefail
app="$1"
result="$2"
mkdir -p "$result"
openbox > "$result/window-manager.log" 2>&1 &
wm_pid=$!
trap 'kill "$wm_pid" 2>/dev/null || true' EXIT
sleep 1
"$app" --smoke-test "$result"
