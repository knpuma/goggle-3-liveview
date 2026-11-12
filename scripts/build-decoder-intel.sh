#!/bin/zsh
set -euo pipefail
project_root="${0:A:h:h}"
decoder_source="$project_root/work/ffmpeg-8.0.3"
source_archive="$project_root/work/ffmpeg-8.0.3.tar.xz"
mkdir -p "$project_root/work"
if [[ ! -f "$source_archive" ]]; then
  curl -fL https://ffmpeg.org/releases/ffmpeg-8.0.3.tar.xz -o "$source_archive"
fi
archive_hash="$(shasum -a 256 "$source_archive" | awk '{print $1}')"
if [[ "$archive_hash" != 6136812ea6d4e68bdba27e33c2a94382711cdf4f8602ffef056ff792bd6f9818 ]]; then
  echo "La fuente FFmpeg no coincide con la versión validada." >&2
  exit 1
fi
if [[ ! -d "$decoder_source" ]]; then
  tar -xf "$source_archive" -C "$project_root/work"
fi
cd "$decoder_source"
if [[ -d /Applications/Xcode.app/Contents/Developer ]]; then
  export DEVELOPER_DIR=/Applications/Xcode.app/Contents/Developer
fi
export SDKROOT="$(xcrun --sdk macosx --show-sdk-path)"
for architecture in x86_64; do
  build_directory="$project_root/work/ffmpeg-build-$architecture"
  architecture_options=(--arch="$architecture" --target-os=darwin)
  if [[ "$architecture" != "$(uname -m)" ]]; then
    architecture_options+=(--enable-cross-compile)
  fi
  if [[ "$architecture" == x86_64 ]] && ! command -v nasm >/dev/null 2>&1; then
    architecture_options+=(--disable-x86asm)
  fi
  rm -rf "$build_directory"
  mkdir -p "$build_directory"
  cd "$decoder_source"
  if [[ -f ffbuild/config.mak ]]; then
    make distclean
  fi
  ./configure --disable-everything --disable-autodetect --disable-network \
    --disable-doc --disable-debug --disable-shared --enable-static \
    --disable-ffplay --disable-ffprobe --enable-ffmpeg \
    --enable-decoder=h264,rawvideo --enable-parser=h264 --enable-demuxer=h264,rawvideo \
    --enable-encoder=rawvideo,h264_videotoolbox --enable-muxer=rawvideo,mp4 \
    --enable-protocol=pipe,file --enable-filter=scale,format,fps \
    --enable-videotoolbox --enable-swscale --enable-pthreads \
    "${architecture_options[@]}" --cc="$(xcrun -f clang)" \
    --extra-cflags="-arch $architecture -mmacosx-version-min=14.0" \
    --extra-ldflags="-arch $architecture -mmacosx-version-min=14.0"
  make -j"$(sysctl -n hw.logicalcpu)" ffmpeg
  cp ffmpeg "$build_directory/ffmpeg"
done
mkdir -p "$project_root/third_party/ffmpeg"
cp "$project_root/work/ffmpeg-build-x86_64/ffmpeg" "$project_root/third_party/ffmpeg/ffmpeg-intel"
cp "$decoder_source/COPYING.LGPLv2.1" "$decoder_source/LICENSE.md" "$project_root/third_party/ffmpeg/"
