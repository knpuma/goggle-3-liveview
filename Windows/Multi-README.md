# Goggle 3 LiveView Multi · Windows x64

View up to four DJI Goggles 3 USB connections in a 2×2 mosaic. Name each pilot, view a channel fullscreen, and record individual channels or the complete mosaic to MP4.

## Install

1. Extract the entire ZIP to a folder.
2. Open **Goggle 3 LiveView Multi.exe**. Keep the included files together.
3. Use **ES / EN** to switch the interface language.

Windows 11 x64, or Windows 10 22H2 x64, is required. The .NET runtime and video encoder are included. Use USB data cables; a powered USB hub is recommended when connecting several goggles.

## Connect

On each pair of goggles:

1. Enable **Settings → About → OTG Wired Connection**.
2. Enable liveview sharing to a mobile device via Wi-Fi in the shortcut menu. The computer receives video over USB and does not need to join the goggles' Wi-Fi network.
3. Without an aircraft, enable **Camera View Recording** under **Settings → Camera → Advanced Camera Settings**.

Connect the USB cables, select a USB connection in each panel and click **Connect** or **Connect all**. Windows may ask for administrator approval to configure the goggles' USB network adapter. No Wi-Fi, Ethernet, DNS, default gateway or firewall settings are changed.

The USB connection must appear as a working network adapter in Windows Device Manager. If it does not, check Windows Update and the USB data cable. Do not install drivers from untrusted download sites.

## Record and fullscreen

- **Record** saves that channel.
- **Record mosaic** saves the complete 2×2 layout to a single 1920×1080 MP4 at a target 30 FPS. Missing or stale channels appear black.
- Both recording modes can run together. Recording performance depends on CPU speed, USB bandwidth and disk speed.
- Files are saved in **Videos / Goggle 3 LiveView Multi**.
- Stop recording and wait for **Saved** before unplugging devices or closing the app.
- **⛶** beside Record shows a clean fullscreen view without controls. The bottom toolbar's fullscreen button shows the full mosaic. Press **Esc** or **F11** to leave fullscreen.

If video does not arrive, disconnect the affected channel, restart LiveView sharing on those goggles, then reconnect. Close any other viewer using the same goggles.

Always use the goggles as your primary display when flying.

[Official DJI Goggles 3 manual](https://dl.djicdn.com/downloads/DJI_Goggles_3/DJI_Goggles_3_User_Manual_EN.pdf) · [Help and issue reports](https://github.com/knpuma/goggle-3-liveview/issues)

## Restore USB configuration

The app records its network changes for each DJI adapter. To reverse those changes, close the viewer and run the following in an administrator PowerShell window from the app folder, replacing the GUID with the DJI adapter's InterfaceGuid from `Get-NetAdapter`:

```powershell
.\Prepare-MultiUsb.ps1 -AdapterId "ADAPTER-GUID" -Restore
```

MIT license. Third-party licenses are included in **Licenses**.
