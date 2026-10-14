#!/bin/zsh
set -euo pipefail
project_root="${0:A:h:h}"
architecture="${1:-x64}"
case "$architecture" in
  x64) target=x86_64; family=x86 ;;
  arm64) target=aarch64; family=aarch64 ;;
  *) echo 'Arquitectura válida: x64 o arm64' >&2; exit 1 ;;
esac
compiler="$project_root/work/windows-dependencies/compiler/llvm-mingw-20261006-ucrt-macos-universal/bin"
if [[ ! -x "$compiler/$target-w64-mingw32-clang" ]]; then
  echo 'Falta llvm-mingw 20261006 para macOS. Consulta Windows/README.md.' >&2; exit 1
fi
source_archive="$project_root/work/ffmpeg-8.0.3.tar.xz"
source_directory="$project_root/work/ffmpeg-windows-source/ffmpeg-8.0.3"
if [[ ! -f "$source_archive" ]]; then
  curl -fL https://ffmpeg.org/releases/ffmpeg-8.0.3.tar.xz -o "$source_archive"
fi
actual_hash="$(shasum -a 256 "$source_archive" | cut -d ' ' -f 1)"
[[ "$actual_hash" == '6136812ea6d4e68bdba27e33c2a94382711cdf4f8602ffef056ff792bd6f9818' ]] || { echo 'Fuente FFmpeg inválida' >&2; exit 1; }
if [[ ! -d "$source_directory" ]]; then
  mkdir -p "$project_root/work/ffmpeg-windows-source"
  tar -xf "$source_archive" -C "$project_root/work/ffmpeg-windows-source"
fi
build_directory="$project_root/work/ffmpeg-win-$architecture-build"
mkdir -p "$build_directory"
cd "$build_directory"
"$source_directory/configure" --target-os=mingw32 --arch="$target" --enable-cross-compile \
  --cross-prefix="$compiler/$target-w64-mingw32-" \
  --cc="$compiler/$target-w64-mingw32-clang" --cxx="$compiler/$target-w64-mingw32-clang++" \
  --host-cc="$(xcrun -f clang)" \
  --host-cflags="-isysroot $(xcrun --sdk macosx --show-sdk-path)" \
  --host-ldflags="-isysroot $(xcrun --sdk macosx --show-sdk-path)" \
  --disable-everything --disable-autodetect --disable-network --disable-doc --disable-debug \
  --disable-shared --enable-static --disable-ffplay --disable-ffprobe --enable-ffmpeg \
  --disable-x86asm --enable-decoder=h264,rawvideo --enable-parser=h264 \
  --enable-demuxer=h264,rawvideo --enable-encoder=mpeg4,rawvideo --enable-muxer=rawvideo,mp4 \
  --enable-protocol=pipe,file --enable-filter=scale,format --enable-swscale --enable-w32threads
make -j4
mkdir -p "$project_root/Windows/third_party/ffmpeg-$architecture"
cp ffmpeg.exe "$project_root/Windows/third_party/ffmpeg-$architecture/ffmpeg.exe"
cp "$source_directory/COPYING.LGPLv2.1" "$project_root/Windows/third_party/ffmpeg-$architecture/"
cp ffbuild/config.mak "$project_root/Windows/third_party/ffmpeg-$architecture/config.mak"
echo "Decodificador Windows $architecture generado."
