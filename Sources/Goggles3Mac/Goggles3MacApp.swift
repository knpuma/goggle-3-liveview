import SwiftUI
import GogglesUSBSupport
import AppKit

@main
struct Goggles3MacApp: App {
    var body: some Scene {
        WindowGroup("Goggle 3 LiveView") {
            ContentView()
                .onAppear { NSApp.setActivationPolicy(.regular); NSApp.activate(ignoringOtherApps: true) }
        }
        .windowStyle(.hiddenTitleBar)
        .defaultSize(width: 1180, height: 720)
    }
}

private struct ContentView: View {
    @StateObject private var viewer = ViewerModel()
    @State private var showTutorial = false
    @State private var showDiagnostics = false
    @State private var fullscreen = false

    var body: some View {
        ZStack {
            Color.black
            if let image = viewer.image {
                VideoSurface(image: image)
            } else {
                LinearGradient(colors: [Color(red: 0.045, green: 0.12, blue: 0.14), .black], startPoint: .topLeading, endPoint: .bottomTrailing)
                VStack(spacing: 18) {
                    Image(systemName: "visionpro").font(.system(size: 58, weight: .ultraLight)).foregroundStyle(.mint.opacity(0.85))
                    Text(viewer.running ? "Waiting for video" : "Your view, on a bigger screen")
                        .font(.title2.weight(.medium)).foregroundStyle(.white)
                    Text(viewer.running ? "Check LiveView and your goggles settings." : "Connect your Goggles 3 via USB-C and click Connect.")
                        .foregroundStyle(.white.opacity(0.5))
                }
            }
            if !fullscreen {
            VStack(spacing: 0) {
                HStack(alignment: .top) {
                    VStack(alignment: .leading, spacing: 5) {
                        Text("GOGGLE 3 LIVEVIEW").font(.system(size: 15, weight: .semibold, design: .rounded)).tracking(2)
                        Text("USB LIVE VIEW").font(.system(size: 9, weight: .medium)).tracking(2.2).foregroundStyle(.white.opacity(0.55))
                    }
                    .contextMenu { Button("USB diagnostics") { showDiagnostics = true } }
                    Spacer()
                    HStack(spacing: 8) {
                        Circle().fill(viewer.image != nil ? Color.mint : Color.white.opacity(0.35)).frame(width: 6, height: 6)
                        Text(viewer.status.isEmpty ? "Ready to connect" : viewer.status)
                            .font(.system(size: 11, weight: .medium))
                    }
                    .padding(.horizontal, 12).padding(.vertical, 8)
                    .background(.black.opacity(0.45), in: Capsule())
                }
                .padding(.horizontal, 28).padding(.top, 38).padding(.bottom, 24)
                .background(LinearGradient(colors: [.black.opacity(0.75), .clear], startPoint: .top, endPoint: .bottom))
                Spacer(minLength: 20)
                VStack(alignment: .leading, spacing: 10) {
                    if !viewer.recordingMessage.isEmpty {
                        Text(viewer.recordingMessage).font(.caption)
                            .foregroundStyle(viewer.recording ? .red : .white.opacity(0.7))
                            .padding(.horizontal, 14).padding(.vertical, 8)
                            .background(.black.opacity(0.65), in: Capsule())
                    }
                    HStack(spacing: 12) {
                        Button(viewer.running ? "Disconnect" : "Connect", systemImage: viewer.running ? "stop.fill" : "bolt.fill") {
                            if viewer.running { viewer.stop() } else { viewer.start() }
                        }
                        .buttonStyle(.plain).padding(.horizontal, 16).padding(.vertical, 10)
                        .background(Color.mint, in: Capsule()).foregroundStyle(.black)
                        .keyboardShortcut(.return, modifiers: [])
                        Button("Tutorial", systemImage: "questionmark.circle") { showTutorial = true }
                            .buttonStyle(.bordered)
                        Button(viewer.recording ? "Stop" : (viewer.finalizingRecording ? "Saving…" : "Record"), systemImage: viewer.recording ? "stop.circle.fill" : "record.circle") {
                            viewer.toggleRecording()
                        }
                        .buttonStyle(.bordered).tint(viewer.recording ? .red : .white)
                        .disabled(!viewer.running || viewer.image == nil || viewer.finalizingRecording)
                        Button {
                            if let window = NSApp.keyWindow ?? NSApp.mainWindow {
                                fullscreen = true
                                window.toggleFullScreen(nil)
                            }
                        } label: {
                            Image(systemName: "arrow.up.left.and.arrow.down.right")
                                .frame(width: 18, height: 18)
                        }
                        .buttonStyle(.bordered)
                        .help("Clean fullscreen · press Esc to exit")
                        .accessibilityLabel("Clean fullscreen")
                        Spacer(minLength: 16)
                        metric("FPS", value: viewer.fps > 0 ? "\(viewer.fps)" : "—")
                        metric("RESOLUTION", value: viewer.image != nil ? "1920×1080" : "—")
                        metric("BITRATE", value: viewer.bitrate > 0 ? String(format: "%.1f Mbps", viewer.bitrate) : "—")
                    }
                    .controlSize(.large)
                    .padding(16)
                    .background(.ultraThinMaterial, in: RoundedRectangle(cornerRadius: 16))
                    .overlay(RoundedRectangle(cornerRadius: 16).stroke(.white.opacity(0.1), lineWidth: 1))
                }
                .padding(.horizontal, 24).padding(.bottom, 22)
                .background(LinearGradient(colors: [.clear, .black.opacity(0.55)], startPoint: .top, endPoint: .bottom))
            }
            }
        }
        .preferredColorScheme(.dark)
        .frame(minWidth: 860, minHeight: 500)
        .onDisappear { viewer.stop() }
        .onReceive(NotificationCenter.default.publisher(for: NSWindow.didEnterFullScreenNotification)) { _ in fullscreen = true }
        .onReceive(NotificationCenter.default.publisher(for: NSWindow.didExitFullScreenNotification)) { _ in fullscreen = false }
        .sheet(isPresented: $showTutorial) { TutorialView() }
        .sheet(isPresented: $showDiagnostics) {
            VStack(alignment: .leading, spacing: 14) {
                Text("USB diagnostics").font(.title2.bold())
                Text("\(USBInventory.devices().filter { $0.vendorID == 0x2CA3 && $0.productID == 0x0020 }.count) Goggles 3 detected")
                ScrollView { Text(viewer.log).font(.system(.caption, design: .monospaced)).textSelection(.enabled).frame(maxWidth: .infinity, alignment: .leading) }
                Button("Close") { showDiagnostics = false }
            }.padding(24).frame(width: 680, height: 400)
        }
    }

    private func metric(_ title: String, value: String) -> some View {
        VStack(alignment: .leading, spacing: 4) {
            Text(title).font(.system(size: 9, weight: .medium)).tracking(1.4).foregroundStyle(.white.opacity(0.45))
            Text(value).font(.system(size: 15, weight: .medium, design: .rounded)).monospacedDigit()
        }.frame(minWidth: 65, alignment: .leading)
    }
}

private struct TutorialView: View {
    @Environment(\.dismiss) private var dismiss
    private let manual = URL(string: "https://dl.djicdn.com/downloads/DJI_Goggles_3/DJI_Goggles_3_User_Manual_EN.pdf")!
    private let steps: [(String, String)] = [
        ("Connect the goggles to your Mac", "Power on the Goggles 3 and use a USB data cable. Connect the goggles directly to your Mac via USB-C."),
        ("Enable OTG Wired Connection", "On the goggles, open Settings → About and enable OTG Wired Connection. DJI documents this setting for connecting the goggles to a computer."),
        ("Enable LiveView sharing", "Push the 5D button backward to open the shortcut menu. Enable liveview sharing to a mobile device via Wi-Fi. Goggle 3 LiveView also needs this setting for USB reception."),
        ("Show the goggles screen without an aircraft", "Under Settings → Camera → Advanced Camera Settings, enable Camera View Recording. This includes on-screen information in recordings; keep it enabled when using this app without an aircraft."),
        ("Click Connect", "If no video arrives, disconnect in the app, switch LiveView sharing off and on again on the goggles, then reconnect.")
    ]

    var body: some View {
        VStack(alignment: .leading, spacing: 18) {
            Text("Set up your Goggles 3").font(.title.bold())
            Text("Direct USB to Mac · no RC Pro required").foregroundStyle(.secondary)
            ForEach(Array(steps.enumerated()), id: \.offset) { index, step in
                HStack(alignment: .top, spacing: 14) {
                    Text("\(index + 1)").font(.headline).frame(width: 28, height: 28).background(.mint.opacity(0.15), in: Circle()).foregroundStyle(.mint)
                    VStack(alignment: .leading, spacing: 5) {
                        Text(step.0).font(.headline)
                        Text(step.1).foregroundStyle(.secondary).fixedSize(horizontal: false, vertical: true)
                    }
                }
            }
            Divider()
            Text("Record saves an MP4 in Movies/Goggle 3 LiveView. Click Stop to finalize the file before turning off the goggles.").font(.footnote).foregroundStyle(.secondary)
            HStack {
                Link("Official DJI manual", destination: manual).font(.caption)
                Spacer()
                Button("Got it") { dismiss() }.buttonStyle(.borderedProminent).tint(.mint)
            }
            Text("Setting names follow the DJI manual. USB reception in this app and operation without an aircraft are independent project features, not DJI-supported functions.")
                .font(.caption2).foregroundStyle(.secondary)
        }
        .padding(28).frame(width: 620)
        .preferredColorScheme(.dark)
    }
}
