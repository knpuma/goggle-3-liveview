import AppKit
import Foundation
import Combine

@MainActor
final class ViewerModel: ObservableObject {
    @Published var running = false
    @Published var image: CGImage?
    @Published var status = ""
    @Published var metrics = ""
    @Published var fps = 0
    @Published var bitrate = 0.0
    @Published var log = ""
    @Published var recording = false
    @Published var finalizingRecording = false
    @Published var recordingMessage = ""
    private var stream: USBVideoStream?
    private var generation = UUID()
    private var frames = 0
    private var lastStats = Date()
    private var lastFrameTime = Date()
    private var watchdog: Timer?
    private var recordingURL: URL?

    func start() {
        stop()
        let token = generation
        running = true; status = "Abriendo conexión con las gafas…"; log = ""; image = nil
        frames = 0; lastStats = Date(); lastFrameTime = Date()
        watchdog = Timer.scheduledTimer(withTimeInterval: 1, repeats: true) { [weak self] _ in
            Task { @MainActor in
                guard let self, self.generation == token, self.running else { return }
                if Date().timeIntervalSince(self.lastFrameTime) > 3 {
                    self.status = "Esperando imagen · comprueba LiveView en las gafas"
                    self.image = nil; self.fps = 0; self.bitrate = 0
                }
            }
        }
        let session = USBVideoStream()
        let delivery = FrameDeliveryGate()
        session.onFrame = { [weak self] frame in
            guard delivery.claim() else { return }
            Task { @MainActor in
                defer { delivery.release() }
                guard let self, self.generation == token else { return }
                self.image = frame; self.status = "En directo · imagen recibida por USB"; self.lastFrameTime = Date()
                self.frames += 1
                let elapsed = Date().timeIntervalSince(self.lastStats)
                if elapsed >= 1 {
                    self.fps = Int((Double(self.frames) / elapsed).rounded())
                    self.metrics = "1920×1080 · \(self.fps) FPS"
                    self.frames = 0; self.lastStats = Date()
                }
            }
        }
        session.onLog = { [weak self] text in
            Task { @MainActor in
                guard let self, self.generation == token else { return }
                self.log = String((self.log + text).suffix(10000))
            }
        }
        session.onBitrate = { [weak self] rate in
            Task { @MainActor in
                guard let self, self.generation == token else { return }
                self.bitrate = rate
            }
        }
        session.onEnd = { [weak self] message in
            Task { @MainActor in
                guard let self, self.generation == token else { return }
                self.status = message; self.running = false; self.stream = nil
                self.image = nil; self.fps = 0; self.bitrate = 0
                self.watchdog?.invalidate(); self.watchdog = nil
            }
        }
        session.onRecordingEnd = { [weak self] url, error in
            Task { @MainActor in
                guard let self, self.recordingURL == url else { return }
                self.recording = false; self.finalizingRecording = false
                self.recordingMessage = error ?? "Guardado en Películas/Goggle 3 LiveView: \(url.lastPathComponent)"
            }
        }
        stream = session
        do { try session.start() }
        catch { running = false; status = error.localizedDescription; log += error.localizedDescription; stream = nil }
    }

    func stop() {
        generation = UUID()
        watchdog?.invalidate(); watchdog = nil
        if recording { recording = false; finalizingRecording = true; recordingMessage = "Finalizando el archivo MP4…" }
        stream?.stop(); stream = nil; running = false; image = nil; metrics = ""
        status = "Desconectado"; fps = 0; bitrate = 0
    }

    func toggleRecording() {
        guard let stream, !finalizingRecording else { return }
        if recording {
            recording = false; finalizingRecording = true; recordingMessage = "Finalizando el archivo MP4…"
            stream.stopRecording()
        } else {
            do {
                let movies = FileManager.default.urls(for: .moviesDirectory, in: .userDomainMask)[0]
                    .appendingPathComponent("Goggle 3 LiveView", isDirectory: true)
                try FileManager.default.createDirectory(at: movies, withIntermediateDirectories: true)
                let stamp = DateFormatter(); stamp.dateFormat = "yyyy-MM-dd_HH-mm-ss"
                let url = movies.appendingPathComponent("Goggle3LiveView_\(stamp.string(from: Date()))_\(UUID().uuidString.prefix(4)).mp4")
                try stream.startRecording(to: url)
                recordingURL = url
                recording = true; recordingMessage = "Grabando · \(url.lastPathComponent)"
            } catch { recordingMessage = error.localizedDescription }
        }
    }
}

/// Bound pending UI work to one image when the window or main thread is busy.
private final class FrameDeliveryGate: @unchecked Sendable {
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
    var onFrame: (@Sendable (CGImage) -> Void)?
    var onLog: (@Sendable (String) -> Void)?
    var onBitrate: (@Sendable (Double) -> Void)?
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
            throw NSError(domain: "Goggles3Mac", code: 1, userInfo: [NSLocalizedDescriptionKey: "Falta el receptor o el decodificador. Reconstruye la aplicación con scripts/make-app.sh."])
        }
        return path
    }

    func start() throws {
        receiver.executableURL = try executable("goggles-rndis-probe")
        receiver.arguments = ["--stream"]
        receiver.standardOutput = compressedVideo
        receiver.standardError = receiverErrors
        decoder.executableURL = try executable("g3-decoder")
        decoder.arguments = ["-hide_banner", "-loglevel", "error", "-flags2", "+showall",
                             "-threads", "2", "-probesize", "65536", "-analyzeduration", "0",
                             "-f", "h264", "-i", "pipe:0", "-an", "-pix_fmt", "bgra",
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
            try receiver.run()
        } catch {
            stop(); throw error
        }
        // Drop the parent's write references so helper termination produces EOF.
        try? compressedVideo.fileHandleForWriting.close()
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
        let frameSize = width * height * 4
        var bytes = Data(); bytes.reserveCapacity(frameSize)
        while true {
            let received = autoreleasepool { () -> Bool in
            let count = frameSize - bytes.count
            guard let chunk = try? rawVideo.fileHandleForReading.read(upToCount: count), !chunk.isEmpty else { return false }
            bytes.append(chunk)
            if bytes.count == frameSize {
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
        if !deliberate { onEnd?("La conexión se ha cerrado. Revisa el cable y Compartir LiveView.") }
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
        let recording = try VideoRecorder(executable: executable("g3-decoder"), url: url)
        recording.onEnd = onRecordingEnd
        lock.lock(); recorder = recording; lock.unlock()
    }

    func stopRecording() {
        lock.lock(); let active = recorder; recorder = nil; lock.unlock()
        active?.finish()
    }
}

/// Encodes the already decoded real frames into a conventional H.264 MP4.
/// This gives players keyframes even when the goggles uses intra refresh.
private final class VideoRecorder: @unchecked Sendable {
    var onEnd: (@Sendable (URL, String?) -> Void)?
    private let process = Process()
    private let input = Pipe()
    private let errors = Pipe()
    private let url: URL
    private let queue = DispatchQueue(label: "org.goggles3mac.recording")
    private let lock = NSLock()
    private var closing = false
    private var pending = 0
    private var dropped = 0

    init(executable: URL, url: URL) throws {
        self.url = url
        process.executableURL = executable
        process.arguments = ["-hide_banner", "-loglevel", "error", "-f", "rawvideo",
            "-pixel_format", "bgra", "-video_size", "1920x1080", "-framerate", "60",
            "-i", "pipe:0", "-an", "-c:v", "h264_videotoolbox", "-b:v", "12M",
            "-g", "60", "-pix_fmt", "yuv420p", "-movflags", "+faststart", "-f", "mp4", url.path]
        process.standardInput = input
        process.standardOutput = FileHandle.nullDevice
        process.standardError = errors
        errors.fileHandleForReading.readabilityHandler = { handle in
            if handle.availableData.isEmpty { handle.readabilityHandler = nil }
        }
        try process.run()
    }

    func append(_ data: Data) {
        lock.lock()
        guard !closing else { lock.unlock(); return }
        guard pending < 4 else { dropped += 1; lock.unlock(); return }
        pending += 1; lock.unlock()
        queue.async { [self] in
            autoreleasepool { try? input.fileHandleForWriting.write(contentsOf: data) }
            lock.lock(); pending -= 1; lock.unlock()
        }
    }

    func finish() {
        lock.lock()
        guard !closing else { lock.unlock(); return }
        closing = true; lock.unlock()
        queue.async { [self] in
            try? input.fileHandleForWriting.close()
            process.waitUntilExit()
            errors.fileHandleForReading.readabilityHandler = nil
            lock.lock(); let skipped = dropped; lock.unlock()
            let error = process.terminationStatus == 0 ? (skipped > 0 ? "MP4 guardado con \(skipped) fotogramas omitidos por carga del Mac: \(url.lastPathComponent)" : nil) : "La grabación no pudo finalizar correctamente: \(url.lastPathComponent)"
            onEnd?(url, error)
        }
    }
}
