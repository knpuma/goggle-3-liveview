import Foundation
import CoreGraphics
import CoreText

/// Composite independent latest frames, not a screen capture. Fullscreen and
/// window visibility never change the 2x2 recorded layout. Missing/stale feeds
/// become black rather than presenting an old frame as current.
final class MosaicRecorder: @unchecked Sendable {
    private struct Feed {
        var token = UUID()
        var image: CGImage?
        var updated = 0.0
        var demo = false
    }
    private let lock = NSLock()
    private var feeds = Array(repeating: Feed(), count: 4)
    private let queue = DispatchQueue(label: "org.goggles3mac.multi.mosaic", qos: .userInitiated)
    private var recorder: VideoRecorder?
    private var timer: DispatchSourceTimer?
    func reset(_ index: Int, token: UUID, demo: Bool) {
        lock.lock(); defer { lock.unlock() }
        feeds[index] = Feed(token: token, image: nil, updated: 0, demo: demo)
    }
    func update(_ index: Int, token: UUID, image: CGImage) {
        lock.lock(); defer { lock.unlock() }
        guard feeds[index].token == token else { return }
        feeds[index].image = image; feeds[index].updated = ProcessInfo.processInfo.systemUptime
    }
    func start(url: URL, onEnd: @escaping @Sendable (URL, String?) -> Void) throws {
        let recording = try VideoRecorder(url: url)
        recording.onEnd = onEnd
        queue.sync {
            recorder = recording
            let timer = DispatchSource.makeTimerSource(queue: queue)
            timer.schedule(deadline: .now(), repeating: .milliseconds(33), leeway: .milliseconds(3))
            timer.setEventHandler { [weak self] in self?.render() }
            self.timer = timer; timer.resume()
        }
    }
    func stop() {
        queue.async { [self] in
            timer?.cancel(); timer = nil
            recorder?.finish(); recorder = nil
        }
    }
    private func render() {
        guard let recorder else { return }
        lock.lock(); let snapshot = feeds; lock.unlock()
        autoreleasepool {
            guard let context = CGContext(data: nil, width: 1920, height: 1080, bitsPerComponent: 8, bytesPerRow: 1920 * 4,
                space: CGColorSpaceCreateDeviceRGB(), bitmapInfo: CGImageAlphaInfo.noneSkipFirst.rawValue | CGBitmapInfo.byteOrder32Little.rawValue) else { return }
            context.setFillColor(CGColor(gray: 0, alpha: 1)); context.fill(CGRect(x: 0, y: 0, width: 1920, height: 1080))
            context.interpolationQuality = .medium
            let now = ProcessInfo.processInfo.systemUptime
            for (index, feed) in snapshot.enumerated() {
                let x = CGFloat(index % 2) * 960, y = CGFloat(1 - index / 2) * 540
                let visible = feed.image != nil && now - feed.updated < 3
                if visible, let image = feed.image { context.draw(image, in: CGRect(x: x, y: y, width: 960, height: 540)) }
                let label = "CANAL \(index + 1) · \(visible ? (feed.demo ? "DEMO · NO ES USB" : "USB") : "SIN IMAGEN")"
                context.setFillColor(CGColor(gray: 0, alpha: 0.75)); context.fill(CGRect(x: x, y: y + 506, width: 960, height: 34))
                let text = NSAttributedString(string: label, attributes: [
                    NSAttributedString.Key(kCTFontAttributeName as String): CTFontCreateWithName("Helvetica" as CFString, 18, nil),
                    NSAttributedString.Key(kCTForegroundColorAttributeName as String): CGColor(gray: 1, alpha: 1)
                ])
                context.textPosition = CGPoint(x: x + 12, y: y + 516)
                CTLineDraw(CTLineCreateWithAttributedString(text), context)
            }
            if let bytes = context.data { recorder.append(Data(bytes: bytes, count: 1920 * 1080 * 4)) }
        }
    }
}
