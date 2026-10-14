import SwiftUI
import MultiCore

struct MintPill: ButtonStyle {
    func makeBody(configuration: Configuration) -> some View {
        configuration.label.padding(.horizontal, 14).padding(.vertical, 9)
            .background(Color.mint.opacity(configuration.isPressed ? 0.7 : 1), in: Capsule()).foregroundStyle(.black)
    }
}
struct DarkPill: ButtonStyle {
    @Environment(\.isEnabled) private var enabled
    func makeBody(configuration: Configuration) -> some View {
        configuration.label.padding(.horizontal, 12).padding(.vertical, 9)
            .background(.white.opacity(configuration.isPressed ? 0.2 : 0.09), in: Capsule())
            .opacity(enabled ? 1 : 0.4)
    }
}
struct ChannelView: View {
    @ObservedObject var slot: ViewerModel
    @ObservedObject var model: MultiModel
    let clean: Bool
    let fullScreen: () -> Void
    var body: some View {
        ZStack {
            Color.black
            if slot.image != nil { LiveVideoSurface(frames: slot.frameStore) }
            else {
                LinearGradient(colors: [Color(red: 0.045, green: 0.12, blue: 0.14), .black], startPoint: .topLeading, endPoint: .bottomTrailing)
                VStack(spacing: 12) {
                    Image(systemName: "visionpro").font(.system(size: 36, weight: .ultraLight)).foregroundStyle(.mint.opacity(0.85))
                    Text(slot.running ? "Esperando imagen" : "Tu visión, en otra pantalla").font(.headline)
                    Text(slot.source == nil ? "Selecciona unas gafas para este canal." : slot.status).font(.caption).foregroundStyle(.secondary).multilineTextAlignment(.center)
                }.padding(28)
            }
            if !clean {
                VStack(spacing: 0) {
                    header
                    Spacer(minLength: 12)
                    VStack(alignment: .leading, spacing: 6) {
                        if !slot.recordingMessage.isEmpty {
                            Text(slot.recordingMessage).font(.system(size: 9))
                                .foregroundStyle(slot.recording ? .red : .white.opacity(0.7))
                                .lineLimit(1).padding(6).background(.black.opacity(0.7), in: Capsule()).help(slot.recordingMessage)
                        }
                        controls
                    }.padding(10)
                    .background(LinearGradient(colors: [.clear, .black.opacity(0.65)], startPoint: .top, endPoint: .bottom))
                }
            }
            if clean && slot.source?.isDemo == true { VStack { HStack { demoBadge; Spacer() }; Spacer() }.padding(10) }
        }
        .clipShape(RoundedRectangle(cornerRadius: clean ? 0 : 14))
        .overlay(RoundedRectangle(cornerRadius: clean ? 0 : 14).stroke(.white.opacity(0.1)))
    }
    private var header: some View {
        VStack(alignment: .leading, spacing: 6) {
            HStack(spacing: 10) {
                Text(String(format: "%02d", slot.id + 1)).font(.caption.monospacedDigit()).foregroundStyle(.mint)
                TextField("Nombre del canal", text: $slot.name).textFieldStyle(.plain).font(.headline)
                    .accessibilityLabel("Nombre del canal \(slot.id + 1)")
                Spacer(minLength: 0)
                Picker("Fuente", selection: Binding(get: { slot.source }, set: { model.select($0, slot: slot.id) })) {
                    Text("Sin asignar").tag(nil as InputSelection?)
                    ForEach(model.devices.filter { model.available(.usb($0.id), slot: slot.id) }) { device in
                        Text(device.label).tag(InputSelection.usb(device.id) as InputSelection?)
                    }
                    if case .usb(let id) = slot.source, !model.devices.contains(where: { $0.id == id }) { Text("USB desconectado").tag(slot.source) }
                    #if DEBUG
                    Text("DEMO \(slot.id + 1)").tag(InputSelection.demo(slot.id + 1) as InputSelection?)
                    #endif
                }.labelsHidden().frame(width: 175).controlSize(.small)
                .accessibilityLabel("Fuente del canal \(slot.id + 1)").disabled(slot.running || slot.finalizingRecording)
            }
            HStack {
                if slot.source?.isDemo == true { demoBadge }
                else {
                    Circle().fill(slot.image == nil ? Color.gray : .mint).frame(width: 5, height: 5)
                    Text(slot.status.isEmpty ? "Listo para conectar" : slot.status).font(.system(size: 10)).lineLimit(1)
                }
                Spacer()
            }
        }.padding(14)
        .background(LinearGradient(colors: [.black.opacity(0.85), .clear], startPoint: .top, endPoint: .bottom))
    }
    private var controls: some View {
        HStack(spacing: 7) {
            Button(slot.running ? "Desconectar" : "Conectar", systemImage: slot.running ? "stop.fill" : "bolt.fill") {
                if slot.running { slot.stop() } else { model.start(slot) }
            }.buttonStyle(MintPill()).disabled(slot.source == nil || slot.finalizingRecording)
                .opacity(slot.source == nil || slot.finalizingRecording ? 0.4 : 1)
                .accessibilityLabel("\(slot.running ? "Desconectar" : "Conectar") canal \(slot.id + 1)")
            Button(slot.recording ? "Detener" : (slot.finalizingRecording ? "Guardando" : "Grabar"), systemImage: slot.recording ? "stop.circle.fill" : "record.circle") { slot.toggleRecording() }
                .buttonStyle(DarkPill()).foregroundStyle(slot.recording ? .red : .white)
                .disabled((!slot.recording && slot.image == nil) || slot.finalizingRecording)
                .accessibilityLabel("\(slot.recording ? "Detener grabación" : "Grabar") canal \(slot.id + 1)")
            Button(action: fullScreen) { Image(systemName: "arrow.up.left.and.arrow.down.right") }
                .buttonStyle(DarkPill()).accessibilityLabel("Pantalla completa canal \(slot.id + 1)")
            Spacer(minLength: 0)
            metric("FPS", value: slot.image == nil ? "—" : "\(slot.fps)")
                .help("Decodificados: \(slot.fps) FPS · presentados: \(slot.presentedFPS) FPS. No es la frecuencia del panel de las gafas.")
            metric("RESOLUCIÓN", value: slot.image == nil ? "—" : "1920×1080")
            metric("BITRATE", value: slot.source?.isDemo == true ? "DEMO" : (slot.image == nil ? "—" : String(format: "%.1f Mb/s", slot.bitrate)))
        }.font(.system(size: 11, weight: .medium))
        .padding(9).background(.ultraThinMaterial, in: RoundedRectangle(cornerRadius: 13))
        .overlay(RoundedRectangle(cornerRadius: 13).stroke(.white.opacity(0.1)))
    }
    private var demoBadge: some View {
        Text("DEMO \(slot.id + 1) · NO ES USB").font(.system(size: 9, weight: .semibold))
            .padding(.horizontal, 8).padding(.vertical, 4).background(.black.opacity(0.75), in: Capsule()).foregroundStyle(.orange)
    }
    private func metric(_ title: String, value: String) -> some View {
        VStack(alignment: .leading, spacing: 3) {
            Text(title).font(.system(size: 7, weight: .medium)).tracking(0.6).foregroundStyle(.white.opacity(0.45))
            Text(value).font(.system(size: 10, weight: .medium, design: .rounded)).monospacedDigit()
        }
    }
}
