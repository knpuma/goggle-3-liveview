import AppKit
import SwiftUI

@MainActor
final class MultiAppDelegate: NSObject, NSApplicationDelegate {
    let model = MultiModel()
    private var shutdownTimer: Timer?
    func applicationDidFinishLaunching(_ notification: Notification) {
        signal(SIGPIPE, SIG_IGN)
        NSApp.setActivationPolicy(.regular)
        NSApp.activate(ignoringOtherApps: true)
    }
    func applicationShouldTerminateAfterLastWindowClosed(_ sender: NSApplication) -> Bool { true }
    func applicationShouldTerminate(_ sender: NSApplication) -> NSApplication.TerminateReply {
        let mustFinish = model.finalizing
        model.stopAll()
        guard mustFinish else { return .terminateNow }
        let timer = Timer(timeInterval: 0.2, repeats: true) { [weak self] _ in
            MainActor.assumeIsolated {
                guard let self else { return }
                if !self.model.finalizing {
                    self.shutdownTimer?.invalidate()
                    self.shutdownTimer = nil
                    NSApp.reply(toApplicationShouldTerminate: true)
                }
            }
        }
        shutdownTimer = timer
        // AppKit waits for terminateLater in a modal run-loop mode. A default-mode
        // timer can otherwise leave the app open after the MP4 files are closed.
        RunLoop.main.add(timer, forMode: .common)
        RunLoop.main.add(timer, forMode: .modalPanel)
        return .terminateLater
    }
}

@main
struct Goggles3MultiApp: App {
    @NSApplicationDelegateAdaptor(MultiAppDelegate.self) private var delegate
    var body: some Scene {
        Window("Goggle 3 LiveView Multi", id: "main") {
            MultiView(model: delegate.model)
        }
        .defaultSize(width: 1280, height: 860)
        .windowStyle(.hiddenTitleBar)
    }
}
