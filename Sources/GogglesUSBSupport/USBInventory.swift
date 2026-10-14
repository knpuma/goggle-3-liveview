import Foundation
import IOKit

public struct USBInterface: Sendable, Identifiable, Codable {
    public let id: String
    public let name: String?
    public let number: Int?
    public let alternateSetting: Int?
    public let interfaceClass: Int?
    public let interfaceSubClass: Int?
    public let interfaceProtocol: Int?
    public let endpointCount: Int?

    public init(
        name: String?, number: Int?, alternateSetting: Int?, interfaceClass: Int?,
        interfaceSubClass: Int?, interfaceProtocol: Int?, endpointCount: Int?
    ) {
        self.name = name
        self.number = number
        self.alternateSetting = alternateSetting
        self.interfaceClass = interfaceClass
        self.interfaceSubClass = interfaceSubClass
        self.interfaceProtocol = interfaceProtocol
        self.endpointCount = endpointCount
        self.id = [number, alternateSetting, interfaceClass, interfaceSubClass, interfaceProtocol]
            .map { $0.map(String.init) ?? "?" }
            .joined(separator: ":")
    }

    public var purpose: String {
        switch (interfaceClass, interfaceSubClass, interfaceProtocol) {
        case (0xE0, 0x01, 0x03): return "control RNDIS"
        case (0x0A, 0x00, 0x00): return "datos Ethernet RNDIS"
        case (0x08, 0x06, 0x50): return "almacenamiento masivo USB"
        case (0xFF, 0x43, 0x01): return "canal BULK propietario"
        default: return "interfaz USB"
        }
    }
}

public struct USBDevice: Sendable, Identifiable, Codable {
    public let id: String
    public let vendorID: Int?
    public let productID: Int?
    public let vendorName: String?
    public let productName: String?
    public let hasSerialNumber: Bool
    public let locationID: Int?
    public let interfaces: [USBInterface]

    public init(
        vendorID: Int?, productID: Int?, vendorName: String?, productName: String?,
        hasSerialNumber: Bool, locationID: Int?, interfaces: [USBInterface]
    ) {
        self.vendorID = vendorID
        self.productID = productID
        self.vendorName = vendorName
        self.productName = productName
        self.hasSerialNumber = hasSerialNumber
        self.locationID = locationID
        self.interfaces = interfaces
        self.id = [vendorID, productID, locationID].map { $0.map { String($0, radix: 16) } ?? "?" }.joined(separator: "-")
    }

    public var displayName: String {
        [vendorName, productName].compactMap { $0 }.joined(separator: " ").trimmingCharacters(in: .whitespaces)
    }
}

public enum USBInventory {
    public static func devices() -> [USBDevice] {
        let matching = IOServiceMatching("IOUSBHostDevice")
        var iterator: io_iterator_t = 0
        guard IOServiceGetMatchingServices(kIOMainPortDefault, matching, &iterator) == KERN_SUCCESS else {
            return []
        }
        defer { IOObjectRelease(iterator) }

        var result: [USBDevice] = []
        var service = IOIteratorNext(iterator)
        while service != 0 {
            result.append(device(from: service))
            IOObjectRelease(service)
            service = IOIteratorNext(iterator)
        }
        return result.sorted { $0.displayName.localizedCaseInsensitiveCompare($1.displayName) == .orderedAscending }
    }

    private static func device(from service: io_registry_entry_t) -> USBDevice {
        let locationID = integerProperty("locationID", on: service)
        return USBDevice(
            vendorID: integerProperty("idVendor", on: service),
            productID: integerProperty("idProduct", on: service),
            vendorName: stringProperty("USB Vendor Name", on: service),
            productName: sanitizedProductName(
                stringProperty("USB Product Name", on: service),
                vendorName: stringProperty("USB Vendor Name", on: service)
            ),
            hasSerialNumber: stringProperty("USB Serial Number", on: service) != nil,
            locationID: locationID,
            interfaces: interfaces(forLocationID: locationID)
        )
    }

    /// Enumerates interface nubs directly instead of recursively traversing descendants.
    /// Descendant storage-driver nodes repeat the parent descriptor fields, which previously
    /// made interface 2 appear several times.
    private static func interfaces(forLocationID locationID: Int?) -> [USBInterface] {
        guard let locationID else { return [] }
        let matching = IOServiceMatching("IOUSBHostInterface")
        var iterator: io_iterator_t = 0
        guard IOServiceGetMatchingServices(kIOMainPortDefault, matching, &iterator) == KERN_SUCCESS else {
            return []
        }
        defer { IOObjectRelease(iterator) }

        var result: [USBInterface] = []
        var entry = IOIteratorNext(iterator)
        while entry != 0 {
            if integerProperty("locationID", on: entry) == locationID {
                result.append(USBInterface(
                    name: stringProperty("kUSBString", on: entry),
                    number: integerProperty("bInterfaceNumber", on: entry),
                    alternateSetting: integerProperty("bAlternateSetting", on: entry),
                    interfaceClass: integerProperty("bInterfaceClass", on: entry),
                    interfaceSubClass: integerProperty("bInterfaceSubClass", on: entry),
                    interfaceProtocol: integerProperty("bInterfaceProtocol", on: entry),
                    endpointCount: integerProperty("bNumEndpoints", on: entry)
                ))
            }
            IOObjectRelease(entry)
            entry = IOIteratorNext(iterator)
        }
        return result.sorted {
            ($0.number ?? -1, $0.alternateSetting ?? -1) < ($1.number ?? -1, $1.alternateSetting ?? -1)
        }
    }

    private static func property(_ key: String, on service: io_registry_entry_t) -> AnyObject? {
        IORegistryEntryCreateCFProperty(service, key as CFString, kCFAllocatorDefault, 0)?.takeRetainedValue()
    }

    private static func integerProperty(_ key: String, on service: io_registry_entry_t) -> Int? {
        (property(key, on: service) as? NSNumber)?.intValue
    }

    private static func stringProperty(_ key: String, on service: io_registry_entry_t) -> String? {
        property(key, on: service) as? String
    }

    /// DJI's USB product string may append a device-specific identifier. Keep it out of
    /// terminal output, GUI state, and JSON snapshots intended for issue reports.
    private static func sanitizedProductName(_ productName: String?, vendorName: String?) -> String? {
        guard let productName else { return nil }
        if vendorName?.caseInsensitiveCompare("DJI") == .orderedSame,
           productName.lowercased().hasPrefix("goggles3-") {
            return "Goggles 3"
        }
        return productName
    }
}
