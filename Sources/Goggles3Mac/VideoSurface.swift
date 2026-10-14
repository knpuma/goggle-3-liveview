import AppKit
import SwiftUI
import QuartzCore

/// A single native layer owns the current video frame. SwiftUI's Image
/// texture cache is unsuitable for a new, full-resolution image every 16 ms.
struct VideoSurface: NSViewRepresentable {
    let image: CGImage

    func makeNSView(context: Context) -> NSView {
        let view = NSView()
        view.wantsLayer = true
        view.layer?.contentsGravity = .resizeAspect
        view.layer?.backgroundColor = NSColor.black.cgColor
        return view
    }

    func updateNSView(_ view: NSView, context: Context) {
        CATransaction.begin()
        CATransaction.setDisableActions(true)
        view.layer?.contents = image
        CATransaction.commit()
    }
}
