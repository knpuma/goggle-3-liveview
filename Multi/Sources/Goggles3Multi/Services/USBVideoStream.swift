import AppKit
import Foundation
import MultiCore

/// Bound pending UI work to one image when the window or main thread is busy.
final class FrameDeliveryGate: @unchecked Sendable {
    private let lock = NSLock()
    private var pending = false
    func claim() -> Bool {
        lock.lock(); defer { lock.unlock() }
        guard !pending else { return false }
        pending = true; return true
    }
    func release() { lock.lock(); pending = false; lock.unlock() }
}

/// The helper owns IOKit; FFmpeg owns H.264 recovery. Both are bundled and
/// connected through local pipes. No network socket or external app is used.
final class USBVideoStream: @unchecked Sendable {
    private let source: InputSelection
    init(source: InputSelection) { self.source = source }
    var onFrame: (@Sendable (CGImage) -> Void)?
    var onLog: (@Sendable (String) -> Void)?
    var onBitrate: (@Sendable (Double) -> Void)?
    var onDecodedFPS: (@Sendable (Int) -> Void)?
    var onEnd: (@Sendable (String) -> Void)?
    var onRecordingEnd: (@Sendable (URL, String?) -> Void)?
    private let receiver = Process()
    private let decoder = Process()
    private let rawVideo = Pipe()
    private let compressedVideo = Pipe()
    private let receiverErrors = Pipe()
    private let decoderErrors = Pipe()
    private let lock = NSLock()
    private var stopping = false
    private var recorder: VideoRecorder?
    private let width = 1920, height = 1080
    private let statsLock = NSLock()
    private var statsBuffer = ""
    private var lastByteCount = 0
    private var lastByteTime: Date?

    private func executable(_ name: String) throws -> URL {
        let bundlePath = Bundle.main.bundleURL.appendingPathComponent("Contents/MacOS/" + name)
        if FileManager.default.isExecutableFile(atPath: bundlePath.path) { return bundlePath }
        let root = URL(fileURLWithPath: FileManager.default.currentDirectoryPath)
        let path = name == "g3-decoder" ? root.appendingPathComponent("third_party/ffmpeg/ffmpeg") : root.appendingPathComponent(".build/debug/" + name)
        guard FileManager.default.isExecutableFile(atPath: path.path) else {
            throw NSError(domain: "Goggles3Mac", code: 1, userInfo: [NSLocalizedDescriptionKey: "The receiver or decoder is missing. Reinstall the complete application."])
        }
        return path
    }

    func start() throws {
        receiver.executableURL = try executable("goggles-multi-usb")
        if case .usb(let id) = source { receiver.arguments = ["--stream", "--device-id", String(id)] }
        receiver.standardOutput = compressedVideo
        receiver.standardError = receiverErrors
        decoder.executableURL = try executable("g3-decoder")
        let inputArguments: [String]
        var demoBytes: Data?
        if case .demo(let channel) = source {
            let clip = Bundle.main.bundleURL.appendingPathComponent("Contents/Resources/Demo/channel-\(channel).h264")
            guard FileManager.default.fileExists(atPath: clip.path) else { throw CocoaError(.fileNoSuchFile) }
            demoBytes = try Data(contentsOf: clip)
            inputArguments = ["-re", "-r", "30", "-f", "h264", "-i", "pipe:0"]
        } else { inputArguments = ["-f", "h264", "-i", "pipe:0"] }
        decoder.arguments = ["-hide_banner", "-loglevel", "error", "-flags2", "+showall",
                             "-threads", "2", "-probesize", "65536", "-analyzeduration", "0",
                             ] + inputArguments + ["-an", "-pix_fmt", "bgra",
                             "-vf", "scale=1920:1080", "-fps_mode", "passthrough", "-f", "rawvideo", "pipe:1"]
        decoder.standardInput = compressedVideo
        decoder.standardOutput = rawVideo
        decoder.standardError = decoderErrors
        receiverErrors.fileHandleForReading.readabilityHandler = { [weak self] handle in
            let data = handle.availableData
            if data.isEmpty { handle.readabilityHandler = nil; return }
            let text = String(decoding: data, as: UTF8.self)
            self?.onLog?(text); self?.receiveStats(text)
        }
        decoderErrors.fileHandleForReading.readabilityHandler = { [weak self] handle in
            let data = handle.availableData
            if data.isEmpty { handle.readabilityHandler = nil; return }
            self?.onLog?(String(decoding: data, as: UTF8.self))
        }
        do {
            try decoder.run()
            if !source.isDemo { try receiver.run() }
        } catch {
            stop(); throw error
        }
        // Drop the parent's write references so helper termination produces EOF.
        if let bytes = demoBytes {
            DispatchQueue.global(qos: .userInitiated).async { [self] in
                defer { try? compressedVideo.fileHandleForWriting.close() }
                while true {
                    lock.lock(); let done = stopping; lock.unlock()
                    if done { break }
                    do { try compressedVideo.fileHandleForWriting.write(contentsOf: bytes) }
                    catch { break }
                }
            }
        } else { try? compressedVideo.fileHandleForWriting.close() }
        try? rawVideo.fileHandleForWriting.close()
        DispatchQueue.global(qos: .userInteractive).async { [self] in readFrames() }
    }

    private func receiveStats(_ text: String) {
        statsLock.lock(); defer { statsLock.unlock() }
        statsBuffer += text
        while let end = statsBuffer.firstIndex(of: "\n") {
            let line = String(statsBuffer[..<end])
            statsBuffer.removeSubrange(...end)
            guard line.hasPrefix("STREAM_STATS "), let field = line.components(separatedBy: "bytes=").last,
                  let count = Int(field) else { continue }
            let now = Date()
            if let previous = lastByteTime {
                let elapsed = now.timeIntervalSince(previous)
                if elapsed > 0 { onBitrate?(Double(count - lastByteCount) * 8 / elapsed / 1_000_000) }
            }
            lastByteCount = count; lastByteTime = now
        }
        if statsBuffer.count > 8192 { statsBuffer = String(statsBuffer.suffix(8192)) }
    }

    private func readFrames() {
        var frameCount = 0
        var measuredAt = ProcessInfo.processInfo.systemUptime
        let frameSize = width * height * 4
        var bytes = Data(); bytes.reserveCapacity(frameSize)
        while true {
            let received = autoreleasepool { () -> Bool in
            let count = frameSize - bytes.count
            guard let chunk = try? rawVideo.fileHandleForReading.read(upToCount: count), !chunk.isEmpty else { return false }
            bytes.append(chunk)
            if bytes.count == frameSize {
                frameCount += 1
                let now = ProcessInfo.processInfo.systemUptime
                if now - measuredAt >= 1 {
                    onDecodedFPS?(Int((Double(frameCount) / (now - measuredAt)).rounded()))
                    frameCount = 0; measuredAt = now
                }
                lock.lock(); let activeRecorder = recorder; lock.unlock()
                activeRecorder?.append(bytes)
                // The provider retains its immutable frame data until AppKit
                // releases the image; the next frame uses a new buffer.
                if let provider = CGDataProvider(data: bytes as CFData),
                   let image = CGImage(width: width, height: height, bitsPerComponent: 8, bitsPerPixel: 32,
                       bytesPerRow: width * 4, space: CGColorSpaceCreateDeviceRGB(),
                       bitmapInfo: CGBitmapInfo(rawValue: CGImageAlphaInfo.noneSkipFirst.rawValue).union(.byteOrder32Little),
                       provider: provider, decode: nil, shouldInterpolate: true, intent: .defaultIntent) {
                    onFrame?(image)
                }
                bytes = Data(); bytes.reserveCapacity(frameSize)
            }
            return true
            }
            if !received { break }
        }
        lock.lock(); let deliberate = stopping; lock.unlock()
        stop()
        if !deliberate { onEnd?(source.isDemo ? "The DEMO signal has ended." : "Connection closed. Check the cable and LiveView sharing.") }
    }

    func stop() {
        lock.lock()
        guard !stopping else { lock.unlock(); return }
        stopping = true; lock.unlock()
        stopRecording()
        if receiver.isRunning { receiver.terminate() }
        if decoder.isRunning { decoder.terminate() }
        for pipe in [receiverErrors, decoderErrors] { pipe.fileHandleForReading.readabilityHandler = nil }
    }

    func startRecording(to url: URL) throws {
        let recording = try VideoRecorder(url: url)
        recording.onEnd = onRecordingEnd
        lock.lock(); recorder = recording; lock.unlock()
    }

    func stopRecording() {
        lock.lock(); let active = recorder; recorder = nil; lock.unlock()
        active?.finish()
    }
}
