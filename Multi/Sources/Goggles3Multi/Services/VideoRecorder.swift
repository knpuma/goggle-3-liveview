import Foundation
import AVFoundation

/// Independent encoder per tile. Monotonic timestamps preserve real duration
/// even if decoding slows down; a bounded queue never stalls a USB receiver.
final class VideoRecorder: @unchecked Sendable {
    var onEnd: (@Sendable (URL, String?) -> Void)?
    private let url: URL
    private let writer: AVAssetWriter
    private let input: AVAssetWriterInput
    private let adaptor: AVAssetWriterInputPixelBufferAdaptor
    private let queue = DispatchQueue(label: "org.goggles3mac.multi.recording")
    private let lock = NSLock()
    private var closing = false
    private var pending = 0
    private var skipped = 0
    private var frames = 0
    private var firstTime: Double?
    private var failure: String?

    init(url: URL) throws {
        self.url = url
        writer = try AVAssetWriter(outputURL: url, fileType: .mp4)
        input = AVAssetWriterInput(mediaType: .video, outputSettings: [
            AVVideoCodecKey: AVVideoCodecType.h264,
            AVVideoWidthKey: 1920, AVVideoHeightKey: 1080,
            AVVideoCompressionPropertiesKey: [AVVideoAverageBitRateKey: 10_000_000]
        ])
        input.expectsMediaDataInRealTime = true
        adaptor = AVAssetWriterInputPixelBufferAdaptor(assetWriterInput: input, sourcePixelBufferAttributes: [
            kCVPixelBufferPixelFormatTypeKey as String: kCVPixelFormatType_32BGRA,
            kCVPixelBufferWidthKey as String: 1920, kCVPixelBufferHeightKey as String: 1080,
            kCVPixelBufferIOSurfacePropertiesKey as String: [:]
        ])
        guard writer.canAdd(input) else { throw CocoaError(.fileWriteUnknown) }
        writer.add(input)
        guard writer.startWriting() else { throw writer.error ?? CocoaError(.fileWriteUnknown) }
        writer.startSession(atSourceTime: .zero)
    }
    func append(_ bytes: Data) {
        let time = ProcessInfo.processInfo.systemUptime
        lock.lock()
        guard !closing else { lock.unlock(); return }
        guard pending < 3 else { skipped += 1; lock.unlock(); return }
        pending += 1; lock.unlock()
        queue.async { [self] in
            defer { lock.lock(); pending -= 1; lock.unlock() }
            autoreleasepool {
                guard input.isReadyForMoreMediaData, let pool = adaptor.pixelBufferPool else {
                    lock.lock(); skipped += 1; lock.unlock(); return
                }
                var pixel: CVPixelBuffer?
                guard bytes.count == 1920 * 1080 * 4,
                      CVPixelBufferPoolCreatePixelBuffer(nil, pool, &pixel) == kCVReturnSuccess,
                      let pixel else { failure = "No se pudo preparar un fotograma."; return }
                CVPixelBufferLockBaseAddress(pixel, [])
                if let destination = CVPixelBufferGetBaseAddress(pixel) {
                    let stride = CVPixelBufferGetBytesPerRow(pixel)
                    bytes.withUnsafeBytes { data in
                        guard let base = data.baseAddress else { return }
                        for row in 0..<1080 { memcpy(destination.advanced(by: row * stride), base.advanced(by: row * 1920 * 4), 1920 * 4) }
                    }
                }
                CVPixelBufferUnlockBaseAddress(pixel, [])
                if firstTime == nil { firstTime = time }
                let timestamp = CMTime(seconds: time - (firstTime ?? time), preferredTimescale: 60000)
                if adaptor.append(pixel, withPresentationTime: timestamp) { frames += 1 }
                else { failure = writer.error?.localizedDescription ?? "No se pudo escribir un fotograma." }
            }
        }
    }
    func finish() {
        lock.lock()
        guard !closing else { lock.unlock(); return }
        closing = true; lock.unlock()
        queue.async { [self] in
            input.markAsFinished()
            if frames == 0 {
                writer.cancelWriting(); onEnd?(url, "Sin fotogramas: no se ha creado un vídeo válido."); return
            }
            writer.finishWriting { [self] in
                lock.lock(); let dropped = skipped; lock.unlock()
                let message = writer.status != .completed ? (writer.error?.localizedDescription ?? "No se pudo finalizar el MP4.") : failure
                onEnd?(url, message ?? (dropped > 0 ? "Guardado con \(dropped) fotogramas omitidos por carga: \(url.lastPathComponent)" : nil))
            }
        }
    }
}
