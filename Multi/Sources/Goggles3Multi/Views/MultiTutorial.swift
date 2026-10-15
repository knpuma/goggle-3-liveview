import SwiftUI

struct MultiTutorial: View {
    @Environment(\.dismiss) private var dismiss
    var body: some View {
        VStack(alignment: .leading, spacing: 16) {
            Text("Connect your goggles").font(.title2.bold())
            Text("This app is separate from Goggle 3 LiveView. Close the original viewer before connecting: both apps cannot use the same goggles at the same time.").foregroundStyle(.secondary)
            Text("1. Connect each pair of goggles with its own USB data cable. Start with one and add the others one at a time.")
            Text("2. On each pair of goggles: Settings → About → OTG Wired Connection.")
            Text("3. Enable liveview sharing to a mobile device via Wi-Fi in the shortcut menu (push the 5D button backward). The Mac receives video over USB; it does not need to join that Wi-Fi network.")
            Text("4. Without an aircraft: keep Camera View Recording enabled under Settings → Camera → Advanced Camera Settings.")
            Text("5. Choose a different USB port for each channel, name each pilot and click Connect. After unplugging a cable, select the device again; it is not replaced automatically.")
            Divider()
            Text("Record saves an individual channel. Record mosaic saves a single 1920×1080 MP4 of all four panels (2×2), even while one is fullscreen. Both can run at once. Files are saved in Movies/Goggle 3 LiveView Multi. Click Stop and wait for Saved. Press Esc to leave fullscreen.")
            Text("If no video arrives, disconnect that channel, restart LiveView sharing on those goggles, then reconnect. Always use the goggles as your primary display when flying.")
            HStack {
                Link("Official DJI manual", destination: URL(string: "https://dl.djicdn.com/downloads/DJI_Goggles_3/DJI_Goggles_3_User_Manual_EN.pdf")!)
                Spacer()
                Button("Got it") { dismiss() }.keyboardShortcut(.defaultAction)
            }
        }.padding(28).frame(width: 700).preferredColorScheme(.dark)
    }
}
