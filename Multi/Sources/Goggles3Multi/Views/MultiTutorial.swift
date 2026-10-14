import SwiftUI

struct MultiTutorial: View {
    @Environment(\.dismiss) private var dismiss
    var body: some View {
        VStack(alignment: .leading, spacing: 16) {
            Text("Conecta tus gafas").font(.title2.bold())
            Text("Esta app es independiente de Goggle 3 LiveView. Cierra el visor original antes de conectar: no pueden usar las mismas gafas a la vez.").foregroundStyle(.secondary)
            Text("1. Conecta cada gafa con su propio cable USB de datos. Empieza por una y añade las demás de una en una.")
            Text("2. En cada gafa: Configuración → Acerca de → Conexión por cable OTG.")
            Text("3. Activa compartir la vista en directo con el dispositivo móvil por Wi-Fi desde los accesos directos (botón 5D hacia atrás). El Mac recibe por USB; no tiene que unirse a ese Wi-Fi.")
            Text("4. Sin aeronave: mantén Grabación de la vista de cámara habilitada en Configuración → Cámara → Configuración avanzada de la cámara.")
            Text("5. Elige un puerto USB distinto por canal, identifica a cada piloto y pulsa Conectar. Si desconectas un cable, vuelve a seleccionar el dispositivo: no se sustituye automáticamente.")
            Divider()
            Text("Grabar en cada panel guarda ese canal. Grabar mosaico crea un único MP4 1920×1080 con las cuatro imágenes (2×2), aunque amplíes una de ellas. Ambas opciones pueden funcionar a la vez. Los vídeos se guardan en Películas/Goggle 3 LiveView Multi. Pulsa Detener y espera a Guardado. Esc sale de pantalla completa.")
            Text("Si no llega imagen, desconecta solo ese canal, desactiva y vuelve a activar la vista en directo en esas gafas y reconecta. Utiliza siempre las gafas como pantalla principal para pilotar.")
            HStack {
                Link("Manual oficial DJI", destination: URL(string: "https://dl.djicdn.com/downloads/DJI_Goggles_3/20240528/DJI_Goggles_3_User_Manual_ES.pdf")!)
                Spacer()
                Button("Entendido") { dismiss() }.keyboardShortcut(.defaultAction)
            }
        }.padding(28).frame(width: 700).preferredColorScheme(.dark)
    }
}
