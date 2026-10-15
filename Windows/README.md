# Goggle 3 LiveView — Windows

Download **x64** for Intel / AMD or **ARM64** for ARM-based Windows computers. Extract the entire ZIP and open **Goggle 3 LiveView.exe**. The .NET runtime and decoder are included.

[Downloads and visual setup guide](https://knpuma.github.io/goggle-3-liveview/). Windows 11 recommended. This is a portable app: move the entire folder if needed. No service or automatic startup is installed.

## First USB connection

Enable OTG Wired Connection and LiveView sharing on the goggles, then click **Connect**. Windows should use Microsoft’s RNDIS adapter driver. **Do not replace it with WinUSB / Zadig.**

The app identifies Goggles 3 by VID `2CA3` and PID `0020`. If needed, it offers to configure only that adapter with administrator permission, adding `192.168.60.1/24` without changing other networks, DNS, the default gateway or firewall. Video requests go to `192.168.60.2:9003` through local port `12346`.

A backup of the DHCP state is kept in `%ProgramData%/Goggles3Mac` (historical name retained for compatibility). To undo the added configuration, open an administrator PowerShell in the app folder and run `./Prepare-Usb.ps1 -Restore`. The script refuses to change an adapter with another static IP or an address conflict.

**Record** creates a silent MP4 in **Videos → Goggle 3 LiveView**. **Stop** finalizes it. Recordings use MPEG-4 with real timestamps and variable frame rate. Session diagnostics are available in the controls context menu and `%LocalAppData%/Goggle 3 LiveView/diagnostic.txt`. Review logs before sharing them.

## Build

Use the official .NET 10 SDK. Matching FFmpeg binaries are included in the source tree.

```powershell
dotnet test Windows/Goggles3.Core.Tests
./Windows/scripts/Publish.ps1 -Runtime win-x64
# Or use -Runtime win-arm64
```

On macOS, Windows targeting is enabled in the project:

```sh
GOGGLES_DOTNET="$(command -v dotnet)" zsh scripts/publish-windows.sh x64
GOGGLES_DOTNET="$(command -v dotnet)" zsh scripts/publish-windows.sh arm64
```

To rebuild FFmpeg 8.0.3, place [llvm-mingw 20261006 for macOS universal](https://github.com/mstorsjo/llvm-mingw/releases/tag/20261006) under `work/windows-dependencies/compiler/`, then run `zsh scripts/build-windows-decoder.sh x64` or `arm64`. The script verifies the source SHA-256. See [third-party notices](../THIRD_PARTY_NOTICES.md).

Developer-only local replay: run `Goggle 3 LiveView.exe --replay C:\\tests\\capture.h264`. The `--software-render` option uses software rendering if a graphics driver cannot initialize WPF; it does not change USB transport. The `Goggles3.Check` project checks decoding and recording without the UI.

## References

- [Microsoft RNDIS driver](https://learn.microsoft.com/en-us/windows-hardware/drivers/network/remote-ndis--rndis-2)
- [Cross-building WPF](https://learn.microsoft.com/en-us/dotnet/core/tools/sdk-errors/netsdk1100)
- [Official DJI Goggles 3 manual](https://dl.djicdn.com/downloads/DJI_Goggles_3/DJI_Goggles_3_User_Manual_EN.pdf)
