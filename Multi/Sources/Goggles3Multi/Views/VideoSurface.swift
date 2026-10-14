import AppKit
import SwiftUI
import QuartzCore
import Combine

/// Only the native video surface is invalidated for each new frame. Menus,
/// labels and buttons update at the one-second statistics cadence instead.
@MainActor
final class VideoFrameStore: ObservableObject {
    @Published var image: CGImage?
}

struct LiveVideoSurface: View {
    @ObservedObject var frames: VideoFrameStore
    var body: some View {
        if let image = frames.image { VideoSurface(image: image) }
    }
}

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
