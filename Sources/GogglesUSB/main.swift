import Foundation
import GogglesUSBSupport

let devices = USBInventory.devices()

if CommandLine.arguments.dropFirst().contains("--json") {
    let encoder = JSONEncoder()
    encoder.outputFormatting = [.prettyPrinted, .sortedKeys]
    do {
        print(String(decoding: try encoder.encode(devices), as: UTF8.self))
    } catch {
        fputs("Could not generate JSON report: \(error.localizedDescription)\n", stderr)
        exit(1)
    }
} else if devices.isEmpty {
    print("No external USB devices detected by IOKit.")
    print("Connect and power on the Goggles 3, enable OTG Wired Connection and run this diagnostic again.")
} else {
    for device in devices {
        print(device.displayName.isEmpty ? "Unnamed USB device" : device.displayName)
        print("  Vendor ID: \(hex(device.vendorID))  Product ID: \(hex(device.productID))  Location ID: \(hex(device.locationID))")
        if device.hasSerialNumber {
            print("  Serial number: [hidden; detected]")
        }
        if device.interfaces.isEmpty {
            print("  USB interfaces: not published by IOKit")
        } else {
            for interface in device.interfaces {
                let name = interface.name.map { " [\($0)]" } ?? ""
                print("  Interface \(interface.number.map(String.init) ?? "?")\(name): \(interface.purpose)")
                print("    class \(hex(interface.interfaceClass)), subclass \(hex(interface.interfaceSubClass)), protocol \(hex(interface.interfaceProtocol)), endpoints \(interface.endpointCount.map(String.init) ?? "?")")
            }
        }
    }
}

private func hex(_ value: Int?) -> String {
    value.map { String(format: "0x%04X", $0) } ?? "unknown"
}
