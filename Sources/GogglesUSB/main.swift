import Foundation
import GogglesUSBSupport

let devices = USBInventory.devices()

if CommandLine.arguments.dropFirst().contains("--json") {
    let encoder = JSONEncoder()
    encoder.outputFormatting = [.prettyPrinted, .sortedKeys]
    do {
        print(String(decoding: try encoder.encode(devices), as: UTF8.self))
    } catch {
        fputs("No se pudo generar el informe JSON: \(error.localizedDescription)\n", stderr)
        exit(1)
    }
} else if devices.isEmpty {
    print("No se han detectado dispositivos USB externos publicados por IOKit.")
    print("Conecta y enciende las Goggles 3; activa su conexión OTG por cable y ejecuta de nuevo este diagnóstico.")
} else {
    for device in devices {
        print(device.displayName.isEmpty ? "Dispositivo USB sin nombre" : device.displayName)
        print("  Vendor ID: \(hex(device.vendorID))  Product ID: \(hex(device.productID))  Location ID: \(hex(device.locationID))")
        if device.hasSerialNumber {
            print("  Número de serie: [oculto; detectado]")
        }
        if device.interfaces.isEmpty {
            print("  Interfaces USB: no publicadas por IOKit")
        } else {
            for interface in device.interfaces {
                let name = interface.name.map { " [\($0)]" } ?? ""
                print("  Interfaz \(interface.number.map(String.init) ?? "?")\(name): \(interface.purpose)")
                print("    clase \(hex(interface.interfaceClass)), subclase \(hex(interface.interfaceSubClass)), protocolo \(hex(interface.interfaceProtocol)), endpoints \(interface.endpointCount.map(String.init) ?? "?")")
            }
        }
    }
}

private func hex(_ value: Int?) -> String {
    value.map { String(format: "0x%04X", $0) } ?? "desconocido"
}
