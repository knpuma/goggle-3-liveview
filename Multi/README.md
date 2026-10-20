# Goggle 3 LiveView Multi

Four video panels, independent controls and mosaic recording in one window.

## Install on Windows

**[Download Multi 0.5.0 · Windows x64 · English / Español](https://github.com/knpuma/goggle-3-liveview/releases/download/multi-v0.5.0/Goggle-3-LiveView-Multi-Windows-x64-0.5.0.zip)**

Requires Windows 11 x64 or Windows 10 22H2 x64. Extract the entire ZIP and open **Goggle 3 LiveView Multi.exe**, keeping the included files together. No separate .NET installation is needed. Use **ES / EN** to switch languages.

[Windows setup guide](../Windows/Multi-README.md) · [Guía en español](../Windows/Multi-LEEME.md)

## Install on Mac

Requires Apple Silicon and macOS 14 or later. Open the DMG and drag **Goggle 3 LiveView Multi** to **Applications**. Multi is separate from the original viewer: both can be installed, but close one before using the same goggles in the other.

This app is locally signed, not notarized by Apple. Verify the download source and follow the [installation guide](https://knpuma.github.io/goggle-3-liveview/#instalar). Do not disable macOS security protections.

## Set up each pair of DJI Goggles 3

1. Power on the goggles and connect a USB **data** cable to your computer.
2. Enable **Settings → About → OTG Wired Connection**.
3. Push the **5D button backward** to open shortcuts and enable **liveview sharing to a mobile device via Wi-Fi**. This app receives video over USB; the computer does not need to join that Wi-Fi network.
4. Without a linked aircraft, keep **Camera View Recording** enabled under **Settings → Camera → Advanced Camera Settings**.
5. Select a USB port in a panel and click **Connect**. For more goggles, use a separate cable and port for each device. Name each panel to identify its pilot.

To move goggles to another panel, click **Disconnect**, then choose **Unassigned** in the original panel before selecting them elsewhere.

Setting names follow the [official DJI manual](https://dl.djicdn.com/downloads/DJI_Goggles_3/DJI_Goggles_3_User_Manual_EN.pdf). USB reception by this app and operation without an aircraft are independent project features, not functions documented or endorsed by DJI for this application.

## Record and use fullscreen

- **Record** in a panel saves that channel as a 1920×1080 MP4.
- **Record mosaic** saves all four panels as one 1920×1080 MP4 at approximately 30 fps. Panels without a signal remain black.
- Individual and mosaic recording can run together; this increases system load.
- Recordings have no audio and are saved in **Movies → Goggle 3 LiveView Multi** on Mac or **Videos → Goggle 3 LiveView Multi** on Windows. Click **Stop** and wait for confirmation that the file was saved.
- The icon beside Record opens clean fullscreen. **Esc** returns to the window. Expanding a panel does not alter the recorded mosaic.

**FPS** measures decoded frames per second, not the goggles display refresh rate. Hover over it for displayed FPS. Bitrate is received USB video throughput, not the drone radio link bitrate.

## If no video arrives

Check the data cable, OTG and LiveView sharing. Close other viewers using the same goggles. Disconnect the affected channel, switch LiveView sharing off and on again on those goggles, then reconnect. After unplugging a cable, select the USB device again.

Always use the goggles as your primary display when flying. This app does not modify firmware or upload recordings to the Internet.

## Report a problem

[Report an issue on GitHub](https://github.com/knpuma/goggle-3-liveview/issues/new?template=multi-error.yml). Include your app version and language, computer model, operating system, number of goggles, hub or direct connection, and reproduction steps. Do not include serial numbers or private information.

Project code: MIT. FFmpeg: LGPL 2.1 or later, with its notices and matching source supplied. Independent project, not affiliated with DJI.

## Downloads

[English · 0.4.1](https://github.com/knpuma/goggle-3-liveview/releases/download/multi-v0.4.1/Goggle-3-LiveView-Multi-Mac-English.dmg) · [Español · 0.4.0](https://github.com/knpuma/goggle-3-liveview/releases/download/multi-v0.4.0/Goggle-3-LiveView-Multi-Mac.dmg)

![English Multi interface](../docs/assets/multi-mac-en.png)

## Build from source

**Windows:** with the .NET 10 SDK, run `dotnet test Windows/Goggles3.Multi.Tests -c Release` and `Windows/scripts/Publish-Multi.ps1` from the repository root. Supply `Windows/third_party/ffmpeg-x64/ffmpeg.exe` and its `COPYING.LGPLv2.1` file first; see [the Windows build guide](../Windows/README.md) and `scripts/build-windows-decoder.sh` for the encoder build.

From this folder, with Swift 6 and Apple developer tools:

```sh
swift test
./script/build_and_run.sh --build-only
```

The output is `dist/Goggle 3 LiveView Multi.app`. Release builds include no demo videos or developer menus. Matching FFmpeg 8.0.3 source is attached to the release; see [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md).
