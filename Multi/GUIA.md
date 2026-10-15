# Goggle 3 LiveView Multi

Four video panels, independent controls and mosaic recording in one window.

## Install on Mac

Requires Apple Silicon and macOS 14 or later. Open the DMG and drag **Goggle 3 LiveView Multi** to **Applications**. Multi is separate from the original viewer: both can be installed, but close one before using the same goggles in the other.

This app is locally signed, not notarized by Apple. Verify the download source and follow the [installation guide](https://knpuma.github.io/goggle-3-liveview/#instalar). Do not disable macOS security protections.

## Set up each pair of DJI Goggles 3

1. Power on the goggles and connect a USB **data** cable to your Mac.
2. Enable **Settings → About → OTG Wired Connection**.
3. Push the **5D button backward** to open shortcuts and enable **liveview sharing to a mobile device via Wi-Fi**. This app receives video over USB; the Mac does not need to join that Wi-Fi network.
4. Without a linked aircraft, keep **Camera View Recording** enabled under **Settings → Camera → Advanced Camera Settings**.
5. Select a USB port in a panel and click **Connect**. For more goggles, use a separate cable and port for each device. Name each panel to identify its pilot.

To move goggles to another panel, click **Disconnect**, then choose **Unassigned** in the original panel before selecting them elsewhere.

Setting names follow the [official DJI manual](https://dl.djicdn.com/downloads/DJI_Goggles_3/DJI_Goggles_3_User_Manual_EN.pdf). USB reception by this app and operation without an aircraft are independent project features, not functions documented or endorsed by DJI for this application.

## Record and use fullscreen

- **Record** in a panel saves that channel as a 1920×1080 MP4.
- **Record mosaic** saves all four panels as one 1920×1080 MP4 at approximately 30 fps. Panels without a signal remain black.
- Individual and mosaic recording can run together; this increases system load.
- Recordings have no audio and are saved in **Movies → Goggle 3 LiveView Multi**. Click **Stop** and wait for confirmation that the file was saved.
- The icon beside Record opens clean fullscreen. **Esc** returns to the window. Expanding a panel does not alter the recorded mosaic.

**FPS** measures decoded frames per second, not the goggles display refresh rate. Hover over it for displayed FPS. Bitrate is received USB video throughput, not the drone radio link bitrate.

## If no video arrives

Check the data cable, OTG and LiveView sharing. Close other viewers using the same goggles. Disconnect the affected channel, switch LiveView sharing off and on again on those goggles, then reconnect. After unplugging a cable, select the USB device again.

Always use the goggles as your primary display when flying. This app does not modify firmware or upload recordings to the Internet.

## Report a problem

[Report an issue on GitHub](https://github.com/knpuma/goggle-3-liveview/issues/new?template=multi-error.yml). Include your app version and language, Mac model, macOS version, number of goggles, hub or direct connection, and reproduction steps. Do not include serial numbers or private information.

Project code: MIT. FFmpeg: LGPL 2.1 or later, with its notices and matching source supplied. Independent project, not affiliated with DJI.
