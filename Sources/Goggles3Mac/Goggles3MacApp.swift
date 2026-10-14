import SwiftUI
import GogglesUSBSupport
import AppKit

@main
struct Goggles3MacApp: App {
    var body: some Scene {
        WindowGroup("Goggle 3 LiveView") {
            ContentView()
                .onAppear { NSApp.setActivationPolicy(.regular); NSApp.activate(ignoringOtherApps: true) }
        }
        .windowStyle(.hiddenTitleBar)
        .defaultSize(width: 1180, height: 720)
    }
}

private struct ContentView: View {
    @StateObject private var viewer = ViewerModel()
    @State private var showTutorial = false
    @State private var showDiagnostics = false
    @State private var fullscreen = false

    var body: some View {
        ZStack {
            Color.black
            if let image = viewer.image {
                VideoSurface(image: image)
            } else {
                LinearGradient(colors: [Color(red: 0.045, green: 0.12, blue: 0.14), .black], startPoint: .topLeading, endPoint: .bottomTrailing)
                VStack(spacing: 18) {
                    Image(systemName: "visionpro").font(.system(size: 58, weight: .ultraLight)).foregroundStyle(.mint.opacity(0.85))
                    Text(viewer.running ? "Esperando imagen" : "Tu visión, en una pantalla más grande")
                        .font(.title2.weight(.medium)).foregroundStyle(.white)
                    Text(viewer.running ? "Comprueba LiveView y los ajustes de las gafas." : "Conecta las Goggles 3 por USB-C y pulsa Conectar.")
                        .foregroundStyle(.white.opacity(0.5))
                }
            }
            if !fullscreen {
            VStack(spacing: 0) {
                HStack(alignment: .top) {
                    VStack(alignment: .leading, spacing: 5) {
                        Text("GOGGLE 3 LIVEVIEW").font(.system(size: 15, weight: .semibold, design: .rounded)).tracking(2)
                        Text("USB LIVE VIEW").font(.system(size: 9, weight: .medium)).tracking(2.2).foregroundStyle(.white.opacity(0.55))
                    }
                    .contextMenu { Button("Diagnóstico USB") { showDiagnostics = true } }
                    Spacer()
                    HStack(spacing: 8) {
                        Circle().fill(viewer.image != nil ? Color.mint : Color.white.opacity(0.35)).frame(width: 6, height: 6)
                        Text(viewer.status.isEmpty ? "Listo para conectar" : viewer.status)
                            .font(.system(size: 11, weight: .medium))
                    }
                    .padding(.horizontal, 12).padding(.vertical, 8)
                    .background(.black.opacity(0.45), in: Capsule())
                }
                .padding(.horizontal, 28).padding(.top, 38).padding(.bottom, 24)
                .background(LinearGradient(colors: [.black.opacity(0.75), .clear], startPoint: .top, endPoint: .bottom))
                Spacer(minLength: 20)
                VStack(alignment: .leading, spacing: 10) {
                    if !viewer.recordingMessage.isEmpty {
                        Text(viewer.recordingMessage).font(.caption)
                            .foregroundStyle(viewer.recording ? .red : .white.opacity(0.7))
                            .padding(.horizontal, 14).padding(.vertical, 8)
                            .background(.black.opacity(0.65), in: Capsule())
                    }
                    HStack(spacing: 12) {
                        Button(viewer.running ? "Desconectar" : "Conectar", systemImage: viewer.running ? "stop.fill" : "bolt.fill") {
                            if viewer.running { viewer.stop() } else { viewer.start() }
                        }
                        .buttonStyle(.plain).padding(.horizontal, 16).padding(.vertical, 10)
                        .background(Color.mint, in: Capsule()).foregroundStyle(.black)
                        .keyboardShortcut(.return, modifiers: [])
                        Button("Tutorial", systemImage: "questionmark.circle") { showTutorial = true }
                            .buttonStyle(.bordered)
                        Button(viewer.recording ? "Detener" : (viewer.finalizingRecording ? "Guardando…" : "Grabar"), systemImage: viewer.recording ? "stop.circle.fill" : "record.circle") {
                            viewer.toggleRecording()
                        }
                        .buttonStyle(.bordered).tint(viewer.recording ? .red : .white)
                        .disabled(!viewer.running || viewer.image == nil || viewer.finalizingRecording)
                        Button {
                            if let window = NSApp.keyWindow ?? NSApp.mainWindow {
                                fullscreen = true
                                window.toggleFullScreen(nil)
                            }
                        } label: {
                            Image(systemName: "arrow.up.left.and.arrow.down.right")
                                .frame(width: 18, height: 18)
                        }
                        .buttonStyle(.bordered)
                        .help("Pantalla completa sin controles · Esc para salir")
                        .accessibilityLabel("Pantalla completa sin controles")
                        Spacer(minLength: 16)
                        metric("FPS", value: viewer.fps > 0 ? "\(viewer.fps)" : "—")
                        metric("RESOLUCIÓN", value: viewer.image != nil ? "1920×1080" : "—")
                        metric("BITRATE", value: viewer.bitrate > 0 ? String(format: "%.1f Mbps", viewer.bitrate) : "—")
                    }
                    .controlSize(.large)
                    .padding(16)
                    .background(.ultraThinMaterial, in: RoundedRectangle(cornerRadius: 16))
                    .overlay(RoundedRectangle(cornerRadius: 16).stroke(.white.opacity(0.1), lineWidth: 1))
                }
                .padding(.horizontal, 24).padding(.bottom, 22)
                .background(LinearGradient(colors: [.clear, .black.opacity(0.55)], startPoint: .top, endPoint: .bottom))
            }
            }
        }
        .preferredColorScheme(.dark)
        .frame(minWidth: 860, minHeight: 500)
        .onDisappear { viewer.stop() }
        .onReceive(NotificationCenter.default.publisher(for: NSWindow.didEnterFullScreenNotification)) { _ in fullscreen = true }
        .onReceive(NotificationCenter.default.publisher(for: NSWindow.didExitFullScreenNotification)) { _ in fullscreen = false }
        .sheet(isPresented: $showTutorial) { TutorialView() }
        .sheet(isPresented: $showDiagnostics) {
            VStack(alignment: .leading, spacing: 14) {
                Text("Diagnóstico USB").font(.title2.bold())
                Text("\(USBInventory.devices().filter { $0.vendorID == 0x2CA3 && $0.productID == 0x0020 }.count) Goggles 3 detectadas")
                ScrollView { Text(viewer.log).font(.system(.caption, design: .monospaced)).textSelection(.enabled).frame(maxWidth: .infinity, alignment: .leading) }
                Button("Cerrar") { showDiagnostics = false }
            }.padding(24).frame(width: 680, height: 400)
        }
    }

    private func metric(_ title: String, value: String) -> some View {
        VStack(alignment: .leading, spacing: 4) {
            Text(title).font(.system(size: 9, weight: .medium)).tracking(1.4).foregroundStyle(.white.opacity(0.45))
            Text(value).font(.system(size: 15, weight: .medium, design: .rounded)).monospacedDigit()
        }.frame(minWidth: 65, alignment: .leading)
    }
}

private struct TutorialView: View {
    @Environment(\.dismiss) private var dismiss
    private let manual = URL(string: "https://dl.djicdn.com/downloads/DJI_Goggles_3/20240528/DJI_Goggles_3_User_Manual_ES.pdf")!
    private let steps: [(String, String)] = [
        ("Conecta las gafas al Mac", "Enciende las Goggles 3 y utiliza un cable USB de datos. Para este Mac, conecta USB-C directamente entre las gafas y el ordenador."),
        ("Activa Conexión por cable OTG", "En el menú de las gafas, entra en Configuración → Acerca de y activa el modo OTG. DJI describe este ajuste para conectar las gafas al ordenador."),
        ("Activa la vista en directo compartida", "Empuja el botón 5D hacia atrás y selecciona «Compartir la vista en directo con el dispositivo móvil por Wi-Fi» en los accesos directos. Goggle 3 LiveView utiliza también este ajuste para su recepción USB."),
        ("Para ver la pantalla sin aeronave", "En Configuración → Cámara → Configuración avanzada de la cámara, habilita «Grabación de la vista de cámara». DJI indica que esta opción incluye la información en pantalla; mantenla activada para el modo de pantalla de Goggle 3 LiveView sin dron."),
        ("Pulsa Conectar", "Si la sesión se queda sin imagen, desconecta en la app, desactiva y activa la vista compartida en las gafas y conecta de nuevo.")
    ]

    var body: some View {
        VStack(alignment: .leading, spacing: 18) {
            Text("Prepara tus Goggles 3").font(.title.bold())
            Text("USB directo al Mac · sin RC Pro").foregroundStyle(.secondary)
            ForEach(Array(steps.enumerated()), id: \.offset) { index, step in
                HStack(alignment: .top, spacing: 14) {
                    Text("\(index + 1)").font(.headline).frame(width: 28, height: 28).background(.mint.opacity(0.15), in: Circle()).foregroundStyle(.mint)
                    VStack(alignment: .leading, spacing: 5) {
                        Text(step.0).font(.headline)
                        Text(step.1).foregroundStyle(.secondary).fixedSize(horizontal: false, vertical: true)
                    }
                }
            }
            Divider()
            Text("Grabar crea un MP4 en Películas/Goggle 3 LiveView. Usa Detener para cerrar el archivo antes de apagar las gafas.").font(.footnote).foregroundStyle(.secondary)
            HStack {
                Link("Manual oficial DJI · págs. 24, 28–31", destination: manual).font(.caption)
                Spacer()
                Button("Entendido") { dismiss() }.buttonStyle(.borderedProminent).tint(.mint)
            }
            Text("Los nombres y rutas proceden del manual DJI. La recepción en Mac y la pantalla sin aeronave son funciones verificadas en Goggle 3 LiveView.")
                .font(.caption2).foregroundStyle(.secondary)
        }
        .padding(28).frame(width: 620)
        .preferredColorScheme(.dark)
    }
}
