import AppKit
import Foundation
import Combine
import MultiCore

@MainActor
final class ViewerModel: ObservableObject, Identifiable {
    let id: Int
    @Published var name: String
    @Published var source: InputSelection?
    var mosaic: MosaicRecorder?
    init(id: Int) { self.id = id; name = "Gafas \(id + 1)" }
    @Published var running = false
    let frameStore = VideoFrameStore()
    var image: CGImage? {
        get { frameStore.image }
        set { frameStore.image = newValue }
    }
    @Published var status = ""
    @Published var metrics = ""
    @Published var fps = 0
    var presentedFPS = 0
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
        guard let source, !running, !finalizingRecording else { return }
        stop()
        let token = generation
        mosaic?.reset(id, token: token, demo: source.isDemo)
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
        let session = USBVideoStream(source: source)
        let delivery = FrameDeliveryGate()
        let mosaic = self.mosaic, channel = id
        session.onFrame = { [weak self] frame in
            mosaic?.update(channel, token: token, image: frame)
            guard delivery.claim() else { return }
            Task { @MainActor in
                defer { delivery.release() }
                guard let self, self.generation == token else { return }
                self.image = frame
                let state = source.isDemo ? "DEMO · señal de prueba, no USB" : "En directo · USB"
                if self.status != state { self.status = state }
                self.lastFrameTime = Date()
                self.frames += 1
                let elapsed = Date().timeIntervalSince(self.lastStats)
                if elapsed >= 1 {
                    self.presentedFPS = Int((Double(self.frames) / elapsed).rounded())
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
        session.onDecodedFPS = { [weak self] rate in
            Task { @MainActor in
                guard let self, self.generation == token else { return }
                self.fps = rate
            }
        }
        session.onEnd = { [weak self] message in
            Task { @MainActor in
                guard let self, self.generation == token else { return }
                self.status = message; self.running = false; self.stream = nil
                if self.recording { self.recording = false; self.finalizingRecording = true }
                self.image = nil; self.fps = 0; self.bitrate = 0
                self.mosaic?.reset(self.id, token: UUID(), demo: source.isDemo)
                self.watchdog?.invalidate(); self.watchdog = nil
            }
        }
        session.onRecordingEnd = { [weak self] url, error in
            Task { @MainActor in
                guard let self, self.recordingURL == url else { return }
                self.recording = false; self.finalizingRecording = false
                self.recordingMessage = error ?? "Guardado: \(url.lastPathComponent)"
            }
        }
        stream = session
        do { try session.start() }
        catch { running = false; status = error.localizedDescription; log += error.localizedDescription; stream = nil }
    }

    func stop() {
        generation = UUID()
        mosaic?.reset(id, token: generation, demo: source?.isDemo == true)
        watchdog?.invalidate(); watchdog = nil
        if recording { recording = false; finalizingRecording = true; recordingMessage = "Finalizando el archivo MP4…" }
        stream?.stop(); stream = nil; running = false; image = nil; metrics = ""
        status = "Desconectado"; fps = 0; bitrate = 0
        presentedFPS = 0
    }

    func toggleRecording() {
        guard let stream, (recording || image != nil), !finalizingRecording else { return }
        if recording {
            recording = false; finalizingRecording = true; recordingMessage = "Finalizando el archivo MP4…"
            stream.stopRecording()
        } else {
            do {
                let movies = FileManager.default.urls(for: .moviesDirectory, in: .userDomainMask)[0]
                    .appendingPathComponent("Goggle 3 LiveView Multi", isDirectory: true)
                try FileManager.default.createDirectory(at: movies, withIntermediateDirectories: true)
                let stamp = DateFormatter(); stamp.dateFormat = "yyyy-MM-dd_HH-mm-ss"
                let mode = source?.isDemo == true ? "DEMO" : "USB"
                let url = movies.appendingPathComponent("\(mode)_Canal-\(id + 1)_\(stamp.string(from: Date()))_\(UUID().uuidString.prefix(8)).mp4")
                try stream.startRecording(to: url)
                recordingURL = url
                recording = true; recordingMessage = "Grabando · \(url.lastPathComponent)"
            } catch { recordingMessage = error.localizedDescription }
        }
    }
}
