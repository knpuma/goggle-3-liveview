# Third-party notices

## FFmpeg 8.0.3

Copyright (c) 2000–2026 the FFmpeg developers.

Goggle 3 LiveView runs a separately built FFmpeg executable, without GPL or non-free components. License: **GNU Lesser General Public License, version 2.1 or later**. The license text is in `third_party/ffmpeg/COPYING.LGPLv2.1` and the app bundle’s Resources/Licenses directory.

- Complete original source: https://ffmpeg.org/releases/ffmpeg-8.0.3.tar.xz
- SHA-256: `6136812ea6d4e68bdba27e33c2a94382711cdf4f8602ffef056ff792bd6f9818`
- Build configuration: `scripts/build-decoder.sh`.
- FFmpeg source is unmodified; components are selected through build configuration.
- The executable at `Goggle 3 LiveView.app/Contents/MacOS/g3-decoder` can be replaced with a compatible build; locally re-sign the app afterward. There is no license restriction against modifying or debugging this app to use a modified version.
- The matching full FFmpeg source archive is provided with each release. No Homebrew shared libraries or proprietary reference-app components are distributed.

## Windows

Windows FFmpeg is also built from the complete, unmodified FFmpeg 8.0.3 source above using `scripts/build-windows-decoder.sh` and llvm-mingw 20261006. It includes no external libraries, GPL or non-free components. Windows recording uses FFmpeg’s native MPEG-4 encoder instead of VideoToolbox. The replaceable x64 and ARM64 decoders are separate executables under LGPL 2.1 or later. Exact configuration is in the build script.

The Windows app uses **.NET 10 and WPF**, open-source software from Microsoft and contributors. The runtime is bundled through `dotnet publish --self-contained`. Notices are retained in `Licenses/DotNET-THIRD-PARTY-NOTICES.txt`, `Licenses/DotNET-LICENSE.txt` and `Licenses/WPF-LICENSE.txt`. Sources: https://github.com/dotnet/runtime and https://github.com/dotnet/wpf.

No proprietary DJI code or Apple fonts are redistributed. BtbN packages were investigated during development but are not included in delivered packages; the distributed decoders are the minimal builds described above.

## Linux

The Linux 0.3.3 packages use Avalonia 11.3.22 (MIT) and a self-contained .NET 10 runtime (MIT and third-party notices). They include SkiaSharp 2.88.9, HarfBuzzSharp 8.3.1.1, MicroCom.Runtime 0.11.0 and Tmds.DBus.Protocol 0.21.3. Original licenses and notices are retained in each package's `Licenses/` directory, including Avalonia's upstream third-party notices and the exact runtime's notices.

Avalonia license/notice snapshot: upstream commit `627ae9ef921621e27e7aa58df2796fbadb50af88`. Tmds.DBus license snapshot: `8cb04f66c330b64244e996ff68f685f27f7381a0`. Sources: https://github.com/AvaloniaUI/Avalonia, https://github.com/dotnet/runtime, https://github.com/mono/SkiaSharp, https://github.com/kekekeks/MicroCom and https://github.com/tmds/Tmds.DBus.

**No FFmpeg executable is included in Linux downloads.** The app launches `/usr/bin/ffmpeg` installed by the user's distribution. Its version, license configuration and source are supplied by that distribution; the Mac/Windows FFmpeg build description above does not apply to it. Generated CI test media are not shipped.

## Research references, not bundled dependencies

- https://github.com/samuelsadok/dji_protocol/blob/master/udp_protocol.md describes earlier DJI UDP protocol fields. Compatibility was investigated on the developer’s own Goggles 3; no repository code was incorporated.
- https://github.com/wllm-rbnt/third-eye documents Goggles 3 and a network path distinct from its mobile transport. Its code is not included.
- https://github.com/planktonwhc/Goggles3-USB-Livestream was observed as a reference for USB video. Its proprietary software is not redistributed.
- https://github.com/morluto/rea was used locally for analysis. REA is not part of this app or required to run it.
