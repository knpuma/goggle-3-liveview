# Goggle 3 LiveView · Linux

Direct USB viewing for **DJI Goggles 3**, with local MP4 recording and clean fullscreen. No RC Pro, Raspberry Pi or intermediate device. English and Spanish are included: use the **ES / EN** button.

**Linux 0.3.3 · Tested only on Ubuntu 24.04 LTS x64 (Intel/AMD); other Linux distributions have not been tested.** Requires an X11 desktop or XWayland. Linux ARM64 and native Wayland are not supported.

## Install

Download the `.deb` from the [main viewer release](https://github.com/knpuma/goggle-3-liveview/releases/tag/v0.3.2). In the download folder:

```sh
sudo apt install ./Goggle-3-LiveView-Linux-x64-0.3.3.deb
```

Open **Goggle 3 LiveView** from the applications menu. The .NET runtime is included; apt installs FFmpeg and desktop dependencies. **Do not run the app with sudo.** No firmware changes, proprietary DJI code or video uploads.

On a Wayland desktop, keep XWayland installed. A normal graphical session with a polkit authorization agent is needed for the first USB configuration. Linux Mint and other distributions may work but are not the build/test target.

## Connect

1. Connect powered-on Goggles 3 with a **USB data cable**.
2. On the goggles, enable **Settings → About → OTG Wired Connection**.
3. Push the 5D button backward and enable **LiveView sharing to a mobile device via Wi-Fi**. Despite the setting name, this viewer receives video over **USB**. Do not connect the computer to the goggles Wi-Fi.
4. Click **Connect**. Approve the system authorization dialog if requested. The helper verifies DJI USB vendor/product IDs and adds `192.168.60.1/24` **only to that USB network interface**. It does not change Wi-Fi, DNS, default gateways or firewall rules. Configuration is temporary and may be requested again after unplugging/rebooting.

Without a linked aircraft, also keep **Camera View Recording** enabled in **Settings → Camera → Advanced Camera Settings**. To show the flight camera, a compatible aircraft/air unit must supply its signal to the goggles.

Menu names follow the [official DJI Goggles 3 manual](https://dl.djicdn.com/downloads/DJI_Goggles_3/DJI_Goggles_3_User_Manual_EN.pdf). This independent USB viewer and operation without a linked aircraft are not DJI-supported Linux features.

## Use

- **Record / Stop:** saves a silent MP4 under your Videos directory → Goggle 3 LiveView. Wait for “Saved” before closing. Closing/disconnecting finalizes the recording.
- **Fullscreen icon**, next to Record, or **F11:** fills the screen and hides controls/statistics. **Esc** returns. Aspect ratio is preserved; letterboxing is intentional.
- **FPS:** frames decoded by this app. **1920 × 1080:** preview/recording size after scaling, not a claim about the original stream resolution. **Mb/s:** received video payload, not the aircraft radio-link bitrate.

## If no picture arrives

Close any other app using the goggles. Disconnect in this app, turn LiveView sharing off and on in the goggles, then reconnect. Check OTG, cable and Camera View Recording. A frozen frame is marked as interrupted rather than live.

Linux needs its standard **rndis_host / usbnet** kernel driver to expose the goggles network interface. Most Ubuntu desktop kernels include it. If no adapter is detected, inspect `lsusb` and `ip link`; do not replace other network drivers. The expected USB ID is `2ca3:0020`.

If another network uses `192.168.60.x`, the app refuses to configure USB. Disconnect that network or resolve the conflict first. This app does not reconfigure unrelated adapters. If your firewall blocks the reply, allow only the DJI interface/peer `192.168.60.2:9003` to local UDP `12346`; do not disable the firewall globally.

If NetworkManager keeps changing the USB address, use its connection editor for **the DJI USB adapter only**: IPv4 **Manual**, address `192.168.60.1`, prefix `24`, no gateway or DNS; enable **use this connection only for resources on its network**. Do not modify your Wi-Fi/Ethernet connection. This also avoids repeated authorization. The app deliberately refuses to overwrite an unexpected address.

## Portable archive

The `.tar.gz` includes the app and .NET but not system libraries or FFmpeg. On Ubuntu 24.04:

```sh
sudo apt install ffmpeg libx11-6 libice6 libsm6 libfontconfig1 libxrandr2 libxi6 libxcursor1 libgl1 libicu74 fonts-dejavu-core iproute2
tar -xzf Goggle-3-LiveView-Linux-x64-0.3.3.tar.gz
cd Goggle-3-LiveView-Linux-x64
./goggle-3-liveview
```

Configure the verified DJI USB adapter through NetworkManager as described above before connecting. The portable app cannot automatically install a privileged helper; use the `.deb` for the integrated authorization flow. Preserve all archive files together.

## Checks and feedback

The CI pipeline builds on Ubuntu 24.04 x64, checks USB identification and configuration safeguards, exercises UDP handshake/acknowledgements on loopback with interface pinning, decodes generated H.264 and verifies a finalized MP4 using ffprobe. It installs the real `.deb` and launches both installed and portable interfaces under Xvfb, checking EN/ES, tutorial, fullscreen and missing-device handling.

**These checks do not establish reception from physical Goggles 3 on Linux.** This first Linux release is a preview for community feedback. [Report an issue](https://github.com/knpuma/goggle-3-liveview/issues) with distro, desktop session, firmware, exact error and steps. Do not include serial numbers, passwords or private information. Always use the goggles as your primary flight display.

## Build

Requires .NET SDK 10.0.402, FFmpeg and the desktop dependencies above. From the repository root:

```sh
ffmpeg -v error -f lavfi -i testsrc2=size=320x180:rate=60 -t 3 -c:v libx264 -preset ultrafast -f h264 /tmp/goggle-ci.h264
GOGGLE_TEST_H264=/tmp/goggle-ci.h264 dotnet test Linux/Goggle3.Linux.Tests -c Release
python3 -m unittest discover -s Linux/scripts -p 'test_*.py'
bash Linux/scripts/package.sh
```

Packages land in `outputs/linux/`, with SHA-256 hashes. Test media are generated outside the package and are never presented as USB video. Source code: MIT. Avalonia, .NET, SkiaSharp, HarfBuzz and other components retain their licenses in `Licenses/`. Linux uses the distribution's FFmpeg executable; **no FFmpeg binary is redistributed in Linux packages**.

[Español](README.es.md) · [All editions](../README.md)
