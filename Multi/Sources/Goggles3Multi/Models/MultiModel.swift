import AppKit
import Combine
import GogglesUSBSupport
import MultiCore

struct DetectedDevice: Identifiable, Equatable {
    let id: UInt64
    let label: String
}

@MainActor
final class MultiModel: ObservableObject {
    let slots = (0..<4).map { ViewerModel(id: $0) }
    @Published private(set) var devices: [DetectedDevice] = []
    @Published var message = "Multi-channel USB viewer"
    @Published var mosaicRecording = false
    @Published var mosaicFinalizing = false
    @Published var mosaicMessage = ""
    private let mosaic = MosaicRecorder()
    init() { slots.forEach { $0.mosaic = mosaic } }
    private var assignments = SlotAssignments()
    private var timer: Timer?

    func begin() {
        guard timer == nil else { return }
        refresh()
        timer = Timer.scheduledTimer(withTimeInterval: 2, repeats: true) { [weak self] _ in
            Task { @MainActor in self?.refresh() }
        }
    }
    func refresh() {
        let previousIDs = Set(devices.map(\.id))
        let detected: [DetectedDevice] = USBInventory.devices().filter { $0.vendorID == 0x2CA3 && $0.productID == 0x0020 }.compactMap { device in
            guard let interface = device.interfaces.first(where: { $0.number == 1 && $0.registryID != nil }),
                  let id = interface.registryID else { return nil }
            return DetectedDevice(id: id, label: "USB · port \(String(device.locationID ?? 0, radix: 16).uppercased())")
        }.sorted { $0.id < $1.id }
        if devices != detected { devices = detected }
        // Never reassign a stale selection to a new physical device automatically.
        // Auto-assign only newly detected devices. Explicitly releasing a source
        // must keep it available instead of reclaiming it at the next refresh.
        for device in devices where !previousIDs.contains(device.id) && !assignments.sources.contains(.usb(device.id)) {
            if let slot = slots.first(where: { $0.source == nil && !$0.running && !$0.finalizingRecording }) {
                select(.usb(device.id), slot: slot.id)
            }
        }
    }
    func select(_ source: InputSelection?, slot: Int) {
        guard slots.indices.contains(slot), !slots[slot].running, !slots[slot].finalizingRecording else { return }
        do {
            try assignments.assign(source, to: slot)
            slots[slot].source = source
            objectWillChange.send()
        } catch { message = "Each USB device can only be assigned to one channel." }
    }
    func available(_ source: InputSelection, slot: Int) -> Bool { assignments.available(source, for: slot) }
    func start(_ slot: ViewerModel) {
        if case .usb(let id) = slot.source, !devices.contains(where: { $0.id == id }) {
            slot.status = "Device disconnected: select its USB port again."; return
        }
        slot.start()
    }
    func startAll() { for slot in slots where !slot.running { start(slot) } }
    func stopAll() {
        if mosaicRecording { toggleMosaicRecording() }
        slots.forEach { $0.stop() }
    }
    func toggleMosaicRecording() {
        guard !mosaicFinalizing else { return }
        if mosaicRecording {
            mosaicRecording = false; mosaicFinalizing = true
            mosaicMessage = "Finalizing the mosaic MP4…"; mosaic.stop(); return
        }
        guard slots.contains(where: { $0.image != nil }) else { mosaicMessage = "Connect at least one channel before recording the mosaic."; return }
        do {
            let directory = FileManager.default.urls(for: .moviesDirectory, in: .userDomainMask)[0].appendingPathComponent("Goggle 3 LiveView Multi", isDirectory: true)
            try FileManager.default.createDirectory(at: directory, withIntermediateDirectories: true)
            let stamp = DateFormatter(); stamp.dateFormat = "yyyy-MM-dd_HH-mm-ss"
            let url = directory.appendingPathComponent("MOSAIC_\(stamp.string(from: Date()))_\(UUID().uuidString.prefix(8)).mp4")
            try mosaic.start(url: url) { [weak self] url, error in
                Task { @MainActor in
                    self?.mosaicRecording = false; self?.mosaicFinalizing = false
                    self?.mosaicMessage = error ?? "Mosaic saved: \(url.lastPathComponent)"
                }
            }
            mosaicRecording = true; mosaicMessage = "Recording 2×2 mosaic · 1920×1080 · \(url.lastPathComponent)"
        } catch { mosaicMessage = error.localizedDescription }
    }
    func addDemos() {
        for slot in slots where slot.source == nil && !slot.running && !slot.finalizingRecording {
            select(.demo(slot.id + 1), slot: slot.id); start(slot)
        }
        message = "DEMO fills empty channels only. It does not simulate four physical USB connections."
    }
    func clearDemos() {
        for slot in slots where slot.source?.isDemo == true {
            slot.stop()
            if !slot.finalizingRecording { select(nil, slot: slot.id) }
        }
    }
    var finalizing: Bool { mosaicRecording || mosaicFinalizing || slots.contains { $0.recording || $0.finalizingRecording } }
    func exportReport() {
        let panel = NSSavePanel()
        panel.nameFieldStringValue = "Goggle3-Multi-report.txt"
        guard panel.runModal() == .OK, let url = panel.url else { return }
        var report = "Goggle 3 LiveView Multi — 0.4.1\n\(Date())\nmacOS: \(ProcessInfo.processInfo.operatingSystemVersionString)\nUSB devices detected: \(devices.count)\nSerial numbers omitted. DEMO does not validate USB hardware.\n"
        for slot in slots {
            report += "\nCHANNEL \(slot.id + 1) · \(slot.source?.isDemo == true ? "DEMO" : "USB / empty")\nStatus: \(slot.status)\nDecoded FPS: \(slot.fps) · FPS displayed: \(slot.presentedFPS) · Mbps: \(slot.bitrate)\nRecording: \(slot.recordingMessage)\n\(slot.log)\n"
        }
        do { try report.write(to: url, atomically: true, encoding: .utf8); message = "Report saved. Review it before sharing." }
        catch { message = error.localizedDescription }
    }
}
