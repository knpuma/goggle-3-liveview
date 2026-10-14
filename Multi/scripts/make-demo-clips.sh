#!/bin/zsh
set -euo pipefail
project_root="${0:A:h:h}"
mkdir -p "$project_root/assets/demo"
# Developer-only dependency: a full FFmpeg build with lavfi, drawtext and libx264.
# The generated synthetic clips contain no third-party footage.
for channel in 1 2 3 4; do
  ffmpeg -hide_banner -loglevel error -f lavfi -i 'testsrc2=size=1920x1080:rate=30' -t 8 \
    -vf "hue=h=$((channel * 70)),drawtext=text='DEMO $channel - NO ES USB':fontfile=/System/Library/Fonts/Helvetica.ttc:fontsize=76:fontcolor=white:box=1:boxcolor=black@0.7:x=(w-tw)/2:y=(h-th)/2" \
    -an -c:v libx264 -preset ultrafast -crf 30 -pix_fmt yuv420p -g 30 -f h264 "$project_root/assets/demo/channel-$channel.h264"
done
