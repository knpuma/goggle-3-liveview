# Third-party notices

## FFmpeg 8.0.3

Copyright (c) 2000–2026 the FFmpeg developers.

Goggle 3 LiveView Multi runs a separately built FFmpeg executable, without GPL or non-free components. License: **GNU Lesser General Public License, version 2.1 or later**. The license text is in `third_party/ffmpeg/COPYING.LGPLv2.1` and the app bundle’s Resources/Licenses directory.

- Complete original source: https://ffmpeg.org/releases/ffmpeg-8.0.3.tar.xz
- SHA-256: `6136812ea6d4e68bdba27e33c2a94382711cdf4f8602ffef056ff792bd6f9818`
- Build configuration: `scripts/build-decoder.sh`.
- FFmpeg source is unmodified; components are selected through build configuration.
- The executable at `Goggle 3 LiveView Multi.app/Contents/MacOS/g3-decoder` can be replaced with a compatible build; locally re-sign the app afterward. There is no license restriction against modifying or debugging this app to use a modified version.
- The matching full FFmpeg source archive is provided with each release. No Homebrew shared libraries or proprietary reference-app components are distributed.

## Multi for macOS

Multi does not include .NET or Windows binaries. Individual and mosaic recording use the system AVFoundation framework. The installer contains no demo videos.

## Research references, not bundled dependencies

- https://github.com/samuelsadok/dji_protocol/blob/master/udp_protocol.md describes earlier DJI UDP protocol fields. Compatibility was investigated on the developer’s own Goggles 3; no repository code was incorporated.
- https://github.com/wllm-rbnt/third-eye documents Goggles 3 and a network path distinct from its mobile transport. Its code is not included.
- https://github.com/planktonwhc/Goggles3-USB-Livestream was observed as a reference for USB video. Its proprietary software is not redistributed.
- https://github.com/morluto/rea was used locally for analysis. REA is not part of this app or required to run it.
