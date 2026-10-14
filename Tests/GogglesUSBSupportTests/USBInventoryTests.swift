import Testing
@testable import GogglesUSBSupport

@Test("Clasifica los descriptores conocidos sin depender de un dispositivo conectado")
func classifiesKnownInterfaces() {
    #expect(USBInterface(name: nil, number: 0, alternateSetting: 0, interfaceClass: 0xE0, interfaceSubClass: 0x01, interfaceProtocol: 0x03, endpointCount: 1).purpose == "control RNDIS")
    #expect(USBInterface(name: nil, number: 1, alternateSetting: 0, interfaceClass: 0x0A, interfaceSubClass: 0, interfaceProtocol: 0, endpointCount: 2).purpose == "datos Ethernet RNDIS")
    #expect(USBInterface(name: nil, number: 2, alternateSetting: 0, interfaceClass: 0x08, interfaceSubClass: 0x06, interfaceProtocol: 0x50, endpointCount: 2).purpose == "almacenamiento masivo USB")
    #expect(USBInterface(name: nil, number: 3, alternateSetting: 0, interfaceClass: 0xFF, interfaceSubClass: 0x43, interfaceProtocol: 1, endpointCount: 2).purpose == "canal BULK propietario")
}
