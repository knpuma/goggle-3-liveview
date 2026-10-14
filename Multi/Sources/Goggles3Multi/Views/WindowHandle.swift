import AppKit
import SwiftUI

struct WindowHandle: NSViewRepresentable {
    let found: (NSWindow) -> Void
    func makeNSView(context: Context) -> HandleView { HandleView(found: found) }
    func updateNSView(_ view: HandleView, context: Context) {}
    final class HandleView: NSView {
        let found: (NSWindow) -> Void
        init(found: @escaping (NSWindow) -> Void) { self.found = found; super.init(frame: .zero) }
        required init?(coder: NSCoder) { nil }
        override func viewDidMoveToWindow() {
            super.viewDidMoveToWindow()
            if let window { DispatchQueue.main.async { [found] in found(window) } }
        }
    }
}
