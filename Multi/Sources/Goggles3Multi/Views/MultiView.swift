import AppKit
import SwiftUI

struct MultiView: View {
    @ObservedObject var model: MultiModel
    @State private var fullScreen = false
    @State private var focused: Int?
    @State private var tutorial = false
    @State private var window: NSWindow?
    var body: some View {
        VStack(spacing: fullScreen ? 2 : 12) {
            if !fullScreen {
                HStack(alignment: .top) {
                    VStack(alignment: .leading, spacing: 5) {
                        Text("GOGGLE 3 LIVEVIEW").font(.system(size: 15, weight: .semibold, design: .rounded)).tracking(2)
                        Text("MULTI VIEW").font(.system(size: 9, weight: .medium)).tracking(2).foregroundStyle(.mint)
                    }
                    Spacer()
                    Label("\(model.devices.count) USB goggles", systemImage: "circle.fill").font(.caption)
                        .foregroundStyle(.mint).padding(.horizontal, 12).padding(.vertical, 8)
                        .background(.black.opacity(0.45), in: Capsule())
                }.padding(.horizontal, 8).padding(.top, 22).padding(.bottom, 8)
            }
            if let focused, fullScreen { tile(model.slots[focused]) }
            else {
                VStack(spacing: fullScreen ? 2 : 12) {
                    HStack(spacing: fullScreen ? 2 : 12) { tile(model.slots[0]); tile(model.slots[1]) }
                    HStack(spacing: fullScreen ? 2 : 12) { tile(model.slots[2]); tile(model.slots[3]) }
                }
            }
            if !fullScreen {
                if !model.mosaicMessage.isEmpty { Text(model.mosaicMessage).font(.caption).foregroundStyle(model.mosaicRecording ? .red : .secondary).lineLimit(1).help(model.mosaicMessage) }
                HStack(spacing: 10) {
                    Button("Connect all", systemImage: "bolt.fill") { model.startAll() }.buttonStyle(MintPill())
                    Button("Tutorial", systemImage: "questionmark.circle") { tutorial = true }.buttonStyle(DarkPill())
                    Button(model.mosaicRecording ? "Stop mosaic" : (model.mosaicFinalizing ? "Saving…" : "Record mosaic"), systemImage: model.mosaicRecording ? "stop.circle.fill" : "record.circle") { model.toggleMosaicRecording() }
                        .buttonStyle(DarkPill()).foregroundStyle(model.mosaicRecording ? .red : .white).disabled(model.mosaicFinalizing)
                    Button { enterFullscreen(nil) } label: { Image(systemName: "arrow.up.left.and.arrow.down.right") }
                        .buttonStyle(DarkPill()).help("Fullscreen mosaic · press Esc to exit")
                        .accessibilityLabel("Fullscreen mosaic")
                    Spacer(minLength: 8)
                    #if DEBUG
                    Menu {
                        Button("Fill empty slots with DEMO") { model.addDemos() }
                        Button("Remove DEMO") { model.clearDemos() }
                        Divider()
                        Button("Disconnect all") { model.stopAll() }
                        Button("Save test report") { model.exportReport() }
                    } label: { Label("Testing", systemImage: "slider.horizontal.3") }
                    .menuStyle(.borderlessButton).fixedSize().padding(.horizontal, 12)
                    #else
                    Button("Disconnect all", systemImage: "stop.fill") { model.stopAll() }
                        .buttonStyle(DarkPill())
                    #endif
                }.font(.system(size: 12))
                .padding(14).background(.ultraThinMaterial, in: RoundedRectangle(cornerRadius: 16))
                .overlay(RoundedRectangle(cornerRadius: 16).stroke(.white.opacity(0.1)))
            }
        }
        .padding(fullScreen ? 0 : 20)
        .background(LinearGradient(colors: [Color(red: 0.045, green: 0.12, blue: 0.14), .black], startPoint: .topLeading, endPoint: .bottomTrailing))
        .background(WindowHandle { window = $0 })
        .preferredColorScheme(.dark).frame(minWidth: 1040, minHeight: 720)
        .onAppear { model.begin() }
        .onExitCommand { leaveFullscreen() }
        .onReceive(NotificationCenter.default.publisher(for: NSWindow.didExitFullScreenNotification)) { note in
            guard let changed = note.object as? NSWindow, changed === window else { return }
            fullScreen = false; focused = nil
        }
        .onReceive(NotificationCenter.default.publisher(for: NSWindow.didEnterFullScreenNotification)) { note in
            guard let changed = note.object as? NSWindow, changed === window else { return }
            fullScreen = true
        }
        .sheet(isPresented: $tutorial) { MultiTutorial() }
    }
    private func tile(_ slot: ViewerModel) -> some View {
        ChannelView(slot: slot, model: model, clean: fullScreen) { enterFullscreen(slot.id) }
            .frame(maxWidth: .infinity, maxHeight: .infinity)
    }
    private func enterFullscreen(_ channel: Int?) {
        guard let window else { return }
        focused = channel; window.makeKeyAndOrderFront(nil)
        if !window.styleMask.contains(.fullScreen) { window.toggleFullScreen(nil) }
        fullScreen = true
    }
    private func leaveFullscreen() {
        if let window, window.styleMask.contains(.fullScreen) { window.toggleFullScreen(nil) }
        else { fullScreen = false; focused = nil }
    }
}
