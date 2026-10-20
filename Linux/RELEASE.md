# Linux packages in the main Goggle 3 LiveView release

Your DJI Goggles 3 screen on your Linux computer, over a direct USB cable. **No RC Pro. No Raspberry Pi. Free and open source.**

Linux downloads are published alongside Mac and Windows in the [main viewer release](https://github.com/knpuma/goggle-3-liveview/releases/tag/v0.3.2), not as a separate product or release. Existing Mac/Windows binaries keep version 0.3.2; Linux binaries keep version 0.3.3. Multi and the Goggles 2 experiment are unaffected.

**Tested only on Ubuntu 24.04 x64. Other Linux distributions have not been tested.**

![Goggle 3 LiveView on Linux](https://raw.githubusercontent.com/knpuma/goggle-3-liveview/main/docs/assets/app-linux-en.png)

## Download

| Your computer | Choose |
|---|---|
| Ubuntu 24.04 · Intel / AMD x64 | **Goggle-3-LiveView-Linux-x64-0.3.3.deb** — recommended installer |
| Linux x64 with dependencies configured | **Goggle-3-LiveView-Linux-x64-0.3.3.tar.gz** — portable archive |

Both packages include **English and Spanish**, selectable with **ES / EN**. X11 or XWayland is required. No Linux ARM64 build is included.

## Install and connect

In the download folder:

```sh
sudo apt install ./Goggle-3-LiveView-Linux-x64-0.3.3.deb
```

Open **Goggle 3 LiveView** from the applications menu — do not run the app with sudo. The runtime is included; FFmpeg and desktop libraries are installed from your distribution.

1. Connect powered-on Goggles 3 with a **USB data cable**.
2. Enable **Settings → About → OTG Wired Connection** on the goggles.
3. Enable **LiveView sharing** in the 5D shortcut menu. The setting refers to Wi-Fi sharing, but this app receives over **USB**.
4. Click **Connect**. If requested, authorize configuration of the DJI USB adapter only.

Without a linked aircraft, keep **Camera View Recording** enabled in **Settings → Camera → Advanced Camera Settings** too. The built-in Tutorial explains the setup.

## Included

- Full-window video with FPS, preview size and received bitrate.
- **Record / Stop** saves a silent MP4 locally in Videos → Goggle 3 LiveView.
- A fullscreen icon beside Record; **F11** enters clean fullscreen and **Esc** returns.
- USB identification and network-conflict checks. No changes to Wi-Fi, DNS, default gateways or firewall rules.
- MIT source code and third-party notices. No demo clips, proprietary DJI code or video uploads.

## Linux preview and feedback

Build, package installation, UI in both languages, fullscreen geometry, video rendering, UDP protocol loopback, H.264 decoding and MP4 finalization have been checked on Ubuntu 24.04 x64: [verified build and test run](https://github.com/knpuma/goggle-3-liveview/actions/runs/39485663644). **Physical Goggles 3 reception on Linux has not yet been verified.** The Linux port remains preliminary; please report your results through the main project's issue tracker.

[Installation and troubleshooting](https://github.com/knpuma/goggle-3-liveview/blob/main/Linux/README.md) · [Guía en español](https://github.com/knpuma/goggle-3-liveview/blob/main/Linux/README.es.md) · [Report a problem](https://github.com/knpuma/goggle-3-liveview/issues) · [All editions](https://knpuma.github.io/goggle-3-liveview/)

Verify downloads against `SHA256SUMS-Linux.txt`. Include your distro, desktop session, firmware, version and reproduction steps in reports, but no serial numbers or private information. Independent community project, not affiliated with DJI. Always use the goggles as your primary flight display.
