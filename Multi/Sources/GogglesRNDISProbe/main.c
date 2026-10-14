#include <CoreFoundation/CoreFoundation.h>
#include <IOKit/IOKitLib.h>
#include <IOKit/usb/IOUSBLib.h>
#include <stdint.h>
#include <stdio.h>
#include <string.h>
#include <time.h>
#include <unistd.h>
#include <signal.h>
#include <errno.h>
#include <stdlib.h>

// Text diagnostics are separate from the optional binary H.264 pipe.
#define printf(...) fprintf(stderr, __VA_ARGS__)
static FILE *videoOutput;
static int streamForever;
static volatile sig_atomic_t stopRequested;
static void requestStop(int signalNumber) { (void)signalNumber; stopRequested = 1; }
static double monotonicSeconds(void) {
    struct timespec value;
    clock_gettime(CLOCK_MONOTONIC, &value);
    return value.tv_sec + value.tv_nsec / 1000000000.0;
}

enum { kMaximumParts = 127, kAssemblySlots = 32 };
typedef struct {
    int occupied, complete;
    UInt16 baseSequence;
    UInt8 count, received;
    UInt8 *parts[kMaximumParts];
    UInt16 lengths[kMaximumParts];
} VideoUnit;

static void clearUnit(VideoUnit *unit) {
    for (int i = 0; i < kMaximumParts; i++) free(unit->parts[i]);
    memset(unit, 0, sizeof(*unit));
}

static void assembleVideo(VideoUnit *units, const UInt8 *message, UInt16 length) {
    unsigned int count = message[17] & 0x7f;
    unsigned int part = (message[17] >> 7) | ((unsigned int)message[18] << 1) | ((unsigned int)message[19] << 9);
    if (!count || part >= count) return;
    UInt16 sequence = (UInt16)(message[4] | (message[5] << 8)) & 0xfff8;
    UInt16 base = sequence - part * 8;
    VideoUnit *unit = &units[(base >> 3) % kAssemblySlots];
    if (!unit->occupied || unit->baseSequence != base || unit->count != count) {
        clearUnit(unit);
        unit->occupied = 1; unit->baseSequence = base; unit->count = count;
    }
    if (unit->complete || unit->parts[part]) return;
    UInt16 payloadLength = length - 20;
    unit->parts[part] = malloc(payloadLength);
    if (!unit->parts[part]) return;
    memcpy(unit->parts[part], message + 20, payloadLength);
    unit->lengths[part] = payloadLength;
    unit->received++;
    if (unit->received == unit->count) {
        if (videoOutput) {
            for (unsigned int i = 0; i < count; i++) {
                if (fwrite(unit->parts[i], 1, unit->lengths[i], videoOutput) != unit->lengths[i]) {
                    stopRequested = 1; break;
                }
            }
            fflush(videoOutput);
        }
        for (unsigned int i = 0; i < count; i++) { free(unit->parts[i]); unit->parts[i] = NULL; }
        unit->complete = 1;
    }
}

enum {
    kDJIVendorID = 0x2CA3,
    kGoggles3ProductID = 0x0020,
    kRNDISDataInterfaceNumber = 1,
    kRNDISInitializeMessage = 0x00000002,
    kRNDISInitializeComplete = 0x80000002,
    kRNDISHaltMessage = 0x00000003,
    kRNDISQueryMessage = 0x00000004,
    kRNDISQueryComplete = 0x80000004,
    kRNDISSetMessage = 0x00000005,
    kRNDISSetComplete = 0x80000005,
    kOIDGenCurrentPacketFilter = 0x0001010E,
    kOID8023CurrentAddress = 0x01010102,
    kRNDISPacketFilterDirected = 0x00000001,
    kRNDISPacketFilterMulticast = 0x00000002,
    kRNDISPacketFilterAllMulticast = 0x00000004,
    kRNDISPacketFilterBroadcast = 0x00000008,
    kRNDISPacketFilterPromiscuous = 0x00000020,
    kSendEncapsulatedCommand = 0,
    kGetEncapsulatedResponse = 1,
};

static UInt8 kHostMAC[6] = {0x02, 0x47, 0x33, 0x4d, 0x61, 0x63};
static const UInt8 kHostIP[4] = {192, 168, 60, 1};
static const UInt8 kGogglesIP[4] = {192, 168, 60, 2};

static void writeLE32(UInt8 *buffer, uint32_t value) {
    buffer[0] = (UInt8)(value & 0xFF);
    buffer[1] = (UInt8)((value >> 8) & 0xFF);
    buffer[2] = (UInt8)((value >> 16) & 0xFF);
    buffer[3] = (UInt8)((value >> 24) & 0xFF);
}

static uint32_t readLE32(const UInt8 *buffer) {
    return ((uint32_t)buffer[0]) |
           ((uint32_t)buffer[1] << 8) |
           ((uint32_t)buffer[2] << 16) |
           ((uint32_t)buffer[3] << 24);
}

static uint32_t unsignedProperty(io_registry_entry_t service, CFStringRef key) {
    CFTypeRef value = IORegistryEntryCreateCFProperty(service, key, kCFAllocatorDefault, 0);
    uint32_t result = UINT32_MAX;
    if (value != NULL && CFGetTypeID(value) == CFNumberGetTypeID()) {
        CFNumberGetValue((CFNumberRef)value, kCFNumberSInt32Type, &result);
    }
    if (value != NULL) {
        CFRelease(value);
    }
    return result;
}

static const char *directionName(UInt8 direction) {
    return direction == kUSBIn ? "IN" : "OUT";
}

static const char *transferName(UInt8 type) {
    switch (type) {
        case kUSBControl: return "control";
        case kUSBIsoc: return "isocrono";
        case kUSBBulk: return "bulk";
        case kUSBInterrupt: return "interrupción";
        default: return "desconocido";
    }
}

static IOReturn sendEncapsulatedCommand(IOUSBInterfaceInterface300 **interface, UInt8 *buffer, UInt16 length) {
    IOUSBDevRequest request = {
        .bmRequestType = USBmakebmRequestType(kUSBOut, kUSBClass, kUSBInterface),
        .bRequest = kSendEncapsulatedCommand,
        .wValue = 0,
        .wIndex = 0,
        .wLength = length,
        .pData = buffer,
        .wLenDone = 0,
    };
    return (*interface)->ControlRequest(interface, 0, &request);
}

static IOReturn getEncapsulatedResponse(IOUSBInterfaceInterface300 **interface, UInt8 *buffer, UInt16 capacity, UInt16 *length) {
    IOUSBDevRequest request = {
        .bmRequestType = USBmakebmRequestType(kUSBIn, kUSBClass, kUSBInterface),
        .bRequest = kGetEncapsulatedResponse,
        .wValue = 0,
        .wIndex = 0,
        .wLength = capacity,
        .pData = buffer,
        .wLenDone = 0,
    };
    IOReturn status = (*interface)->ControlRequest(interface, 0, &request);
    *length = request.wLenDone;
    return status;
}

static int probeRNDISInitialization(IOUSBInterfaceInterface300 **interface, int endSession) {
    UInt8 initialize[24] = {0};
    writeLE32(initialize, kRNDISInitializeMessage);
    writeLE32(initialize + 4, sizeof(initialize));
    writeLE32(initialize + 8, 1);       // request ID
    writeLE32(initialize + 12, 1);      // RNDIS major version
    writeLE32(initialize + 16, 0);      // RNDIS minor version
    writeLE32(initialize + 20, 0x4000); // requested transfer size

    IOReturn status = sendEncapsulatedCommand(interface, initialize, sizeof(initialize));
    if (status != kIOReturnSuccess) {
        fprintf(stderr, "RNDIS INITIALIZE no fue aceptado (0x%08x).\n", status);
        return 1;
    }

    usleep(20 * 1000);
    UInt8 response[256] = {0};
    UInt16 responseLength = 0;
    status = getEncapsulatedResponse(interface, response, sizeof(response), &responseLength);
    if (status != kIOReturnSuccess) {
        fprintf(stderr, "No llegó respuesta RNDIS INITIALIZE (0x%08x).\n", status);
        return 1;
    }
    if (responseLength < 16 || readLE32(response) != kRNDISInitializeComplete) {
        fprintf(stderr, "Respuesta RNDIS inesperada (%u bytes).\n", responseLength);
        return 1;
    }

    const uint32_t messageLength = readLE32(response + 4);
    const uint32_t requestID = readLE32(response + 8);
    const uint32_t statusCode = readLE32(response + 12);
    if (requestID != 1 || messageLength > responseLength || statusCode != 0) {
        fprintf(stderr, "RNDIS INITIALIZE respondió con estado 0x%08x.\n", statusCode);
        return 1;
    }
    printf("RNDIS INITIALIZE confirmado: respuesta estándar de %u bytes.\n", responseLength);

    if (!endSession) {
        return 0;
    }

    // End the standard transport negotiation cleanly. No packet filter is set and no data pipe is used.
    UInt8 halt[12] = {0};
    writeLE32(halt, kRNDISHaltMessage);
    writeLE32(halt + 4, sizeof(halt));
    writeLE32(halt + 8, 2);
    status = sendEncapsulatedCommand(interface, halt, sizeof(halt));
    if (status != kIOReturnSuccess) {
        fprintf(stderr, "RNDIS HALT no fue aceptado (0x%08x).\n", status);
        return 1;
    }
    printf("Sesión RNDIS cerrada sin enviar paquetes de vídeo o red.\n");
    return 0;
}

static int setRNDISPacketFilter(IOUSBInterfaceInterface300 **interface) {
    UInt8 request[32] = {0};
    writeLE32(request, kRNDISSetMessage);
    writeLE32(request + 4, sizeof(request));
    writeLE32(request + 8, 2);
    writeLE32(request + 12, kOIDGenCurrentPacketFilter);
    writeLE32(request + 16, 4);
    writeLE32(request + 20, 20);
    // Capture the unicast host stream as well as any broadcast or multicast
    // discovery emitted before the goggles has an ARP entry for this Mac.
    writeLE32(request + 28,
        kRNDISPacketFilterDirected |
        kRNDISPacketFilterMulticast |
        kRNDISPacketFilterAllMulticast |
        kRNDISPacketFilterBroadcast |
        kRNDISPacketFilterPromiscuous);

    IOReturn status = sendEncapsulatedCommand(interface, request, sizeof(request));
    if (status != kIOReturnSuccess) {
        fprintf(stderr, "RNDIS SET(packet filter) no fue aceptado (0x%08x).\n", status);
        return 1;
    }
    usleep(20 * 1000);
    UInt8 response[64] = {0};
    UInt16 responseLength = 0;
    status = getEncapsulatedResponse(interface, response, sizeof(response), &responseLength);
    if (status != kIOReturnSuccess || responseLength < 16 || readLE32(response) != kRNDISSetComplete ||
        readLE32(response + 8) != 2 || readLE32(response + 12) != 0) {
        fprintf(stderr, "RNDIS SET(packet filter) no confirmó la escucha.\n");
        return 1;
    }
    return 0;
}

static int queryRNDISMAC(IOUSBInterfaceInterface300 **interface, UInt8 mac[6]) {
    UInt8 request[28] = {0};
    writeLE32(request, kRNDISQueryMessage);
    writeLE32(request + 4, sizeof(request));
    writeLE32(request + 8, 3);
    writeLE32(request + 12, kOID8023CurrentAddress);

    IOReturn status = sendEncapsulatedCommand(interface, request, sizeof(request));
    if (status != kIOReturnSuccess) {
        fprintf(stderr, "RNDIS QUERY(mac) no fue aceptado (0x%08x).\n", status);
        return 1;
    }
    usleep(20 * 1000);
    UInt8 response[64] = {0};
    UInt16 responseLength = 0;
    status = getEncapsulatedResponse(interface, response, sizeof(response), &responseLength);
    if (status != kIOReturnSuccess || responseLength < 30 || readLE32(response) != kRNDISQueryComplete ||
        readLE32(response + 8) != 3 || readLE32(response + 12) != 0 || readLE32(response + 16) != 6) {
        fprintf(stderr, "RNDIS QUERY(mac) no confirmó una dirección Ethernet.\n");
        return 1;
    }
    uint32_t offset = 8 + readLE32(response + 20);
    if (offset > responseLength || 6 > responseLength - offset) {
        fprintf(stderr, "RNDIS QUERY(mac) devolvió un offset no válido.\n");
        return 1;
    }
    memcpy(mac, response + offset, 6);
    return 0;
}

static UInt16 internetChecksum(const UInt8 *data, size_t length) {
    UInt32 sum = 0;
    while (length > 1) {
        sum += ((UInt16)data[0] << 8) | data[1];
        data += 2;
        length -= 2;
    }
    if (length == 1) {
        sum += (UInt16)data[0] << 8;
    }
    while ((sum >> 16) != 0) {
        sum = (sum & 0xffff) + (sum >> 16);
    }
    return (UInt16)~sum;
}

static int writeRNDISPacket(IOUSBInterfaceInterface300 **interface, UInt8 pipe, const UInt8 *ethernet, UInt32 ethernetLength) {
    UInt8 message[2048] = {0};
    const UInt32 headerLength = 44;
    if (ethernetLength > sizeof(message) - headerLength) {
        return 1;
    }
    writeLE32(message, 1);
    writeLE32(message + 4, headerLength + ethernetLength);
    writeLE32(message + 8, 36);
    writeLE32(message + 12, ethernetLength);
    memcpy(message + headerLength, ethernet, ethernetLength);
    UInt32 length = headerLength + ethernetLength;
    IOReturn status = (*interface)->WritePipeTO(interface, pipe, message, length, 500, 500);
    if (status != kIOReturnSuccess) {
        fprintf(stderr, "No se pudo escribir una trama RNDIS (0x%08x).\n", status);
        return 1;
    }
    return 0;
}

static int sendIPv4UDP(
    IOUSBInterfaceInterface300 **interface, UInt8 pipe, const UInt8 gogglesMAC[6],
    UInt16 sourcePort, UInt16 destinationPort, const UInt8 *payload, UInt16 payloadLength
) {
    UInt8 ethernet[1600] = {0};
    UInt8 *ip = ethernet + 14;
    UInt8 *udp = ip + 20;
    const UInt16 totalIPLength = 20 + 8 + payloadLength;
    memcpy(ethernet, gogglesMAC, 6);
    memcpy(ethernet + 6, kHostMAC, 6);
    ethernet[12] = 0x08;
    ethernet[13] = 0x00;
    ip[0] = 0x45;
    ip[2] = (UInt8)(totalIPLength >> 8);
    ip[3] = (UInt8)totalIPLength;
    ip[8] = 64;
    ip[9] = 17;
    memcpy(ip + 12, kHostIP, 4);
    memcpy(ip + 16, kGogglesIP, 4);
    UInt16 checksum = internetChecksum(ip, 20);
    ip[10] = (UInt8)(checksum >> 8);
    ip[11] = (UInt8)checksum;
    udp[0] = (UInt8)(sourcePort >> 8);
    udp[1] = (UInt8)sourcePort;
    udp[2] = (UInt8)(destinationPort >> 8);
    udp[3] = (UInt8)destinationPort;
    const UInt16 udpLength = 8 + payloadLength;
    udp[4] = (UInt8)(udpLength >> 8);
    udp[5] = (UInt8)udpLength;
    memcpy(udp + 8, payload, payloadLength);
    return writeRNDISPacket(interface, pipe, ethernet, 14 + totalIPLength);
}

static int sendARPReply(
    IOUSBInterfaceInterface300 **interface, UInt8 pipe, const UInt8 gogglesMAC[6]
) {
    UInt8 ethernet[42] = {0};
    memcpy(ethernet, gogglesMAC, 6);
    memcpy(ethernet + 6, kHostMAC, 6);
    ethernet[12] = 0x08;
    ethernet[13] = 0x06;
    ethernet[14] = 0;
    ethernet[15] = 1;
    ethernet[16] = 0x08;
    ethernet[17] = 0;
    ethernet[18] = 6;
    ethernet[19] = 4;
    ethernet[20] = 0;
    ethernet[21] = 2;
    memcpy(ethernet + 22, kHostMAC, 6);
    memcpy(ethernet + 28, kHostIP, 4);
    memcpy(ethernet + 32, gogglesMAC, 6);
    memcpy(ethernet + 38, kGogglesIP, 4);
    return writeRNDISPacket(interface, pipe, ethernet, sizeof(ethernet));
}

static UInt8 xorHeader(const UInt8 *data) {
    UInt8 value = 0;
    for (int index = 0; index < 7; index++) value ^= data[index];
    return value;
}

static int resolvePeerMAC(IOUSBInterfaceInterface300 **interface, UInt8 inputPipe,
                          UInt8 outputPipe, UInt8 peerMAC[6]) {
    UInt8 arp[42] = {0};
    memset(arp, 0xff, 6);
    memcpy(arp + 6, kHostMAC, 6);
    arp[12] = 8; arp[13] = 6;
    arp[15] = 1; arp[16] = 8; arp[18] = 6; arp[19] = 4;
    arp[21] = 1;
    memcpy(arp + 22, kHostMAC, 6);
    memcpy(arp + 28, kHostIP, 4);
    memcpy(arp + 38, kGogglesIP, 4);
    double started = monotonicSeconds(), lastRequest = 0;
    while (!stopRequested && monotonicSeconds() - started < 3) {
        if (monotonicSeconds() - lastRequest > 0.25) {
            if (writeRNDISPacket(interface, outputPipe, arp, sizeof(arp))) return 1;
            lastRequest = monotonicSeconds();
        }
        UInt8 buffer[16384] = {0};
        UInt32 length = sizeof(buffer);
        IOReturn status = (*interface)->ReadPipeTO(interface, inputPipe, buffer, &length, 250, 250);
        if (status == kIOReturnTimeout || status == kIOUSBTransactionTimeout) continue;
        if (status != kIOReturnSuccess) {
            fprintf(stderr, "Lectura durante ARP: 0x%08x.\n", status);
            continue;
        }
        if (length < 44 || readLE32(buffer) != 1) continue;
        uint32_t offset = 8 + readLE32(buffer + 8);
        uint32_t frameLength = readLE32(buffer + 12);
        if (offset > length || frameLength > length - offset || frameLength < 42) continue;
        UInt8 *frame = buffer + offset;
        if (frame[12] != 8 || frame[13] != 6 || memcmp(frame + 28, kGogglesIP, 4)) continue;
        memcpy(peerMAC, frame + 22, 6);
        if (frame[21] == 1) sendARPReply(interface, outputPipe, peerMAC);
        printf("ARP confirmado: las gafas respondieron desde 192.168.60.2.\n");
        return 0;
    }
    fprintf(stderr, "Sin respuesta ARP de 192.168.60.2; no se enviará una suscripción a una dirección Ethernet supuesta.\n");
    return 1;
}

static int liveProbe(
    IOUSBInterfaceInterface300 **interface, UInt8 inputPipe, UInt8 outputPipe
) {
    UInt8 gogglesMAC[6] = {0};
    if (setRNDISPacketFilter(interface) != 0 || queryRNDISMAC(interface, kHostMAC) != 0) return 1;
    printf("RNDIS activo; resolviendo la dirección Ethernet remota mediante ARP.\n");
    if (resolvePeerMAC(interface, inputPipe, outputPipe, gogglesMAC)) return 1;

    UInt8 handshake[] = {
        0x30, 0x80, 0x3a, 0xdd, 0x00, 0x00, 0x00, 0x00,
        0xd0, 0xe9, 0x64, 0x00, 0x64, 0x00, 0xc0, 0x05,
        0x14, 0x00, 0x00, 0x0a, 0x00, 0x64, 0x00, 0x64,
        0x00, 0xc0, 0x05, 0x14, 0x00, 0x00, 0x64, 0x00,
        0x14, 0x00, 0x64, 0x00, 0xc0, 0x05, 0x14, 0x00,
        0x00, 0x64, 0x00, 0x01, 0x01, 0x04, 0x0a, 0x02,
    };
    UInt16 sessionID = (UInt16)arc4random();
    handshake[2] = sessionID & 0xff;
    handshake[3] = sessionID >> 8;
    handshake[7] = xorHeader(handshake);

    // The goggles' network stack may not yet know the Ethernet address that
    // owns 192.168.60.1.  Advertise it before sending UDP: otherwise the
    // first subscription can be discarded while the goggles resolve ARP.
    if (sendARPReply(interface, outputPipe, gogglesMAC) != 0) return 1;
    usleep(20 * 1000);
    if (sendIPv4UDP(interface, outputPipe, gogglesMAC, 12346, 9003, handshake, sizeof(handshake)) != 0) return 1;
    printf("Suscripción UDP LiveView enviada; %s.\n", streamForever ? "recepción continua" : "escuchando seis segundos");

    UInt8 session[2] = {handshake[2], handshake[3]};
    VideoUnit *units = calloc(kAssemblySlots, sizeof(VideoUnit));
    if (!units) return 1;
    int packets = 0, videoPackets = 0;
    size_t videoBytes = 0;
    const double started = monotonicSeconds();
    double lastHandshake = started;
    double lastStats = started;
    while (!stopRequested && (streamForever || monotonicSeconds() - started < 6)) {
        UInt8 buffer[16384] = {0};
        UInt32 length = sizeof(buffer);
        IOReturn status = (*interface)->ReadPipeTO(interface, inputPipe, buffer, &length, 250, 250);
        if (status == kIOReturnTimeout || status == kIOUSBTransactionTimeout) {
            if (!packets && monotonicSeconds() - lastHandshake > 0.25) {
                sendIPv4UDP(interface, outputPipe, gogglesMAC, 12346, 9003, handshake, sizeof(handshake));
                lastHandshake = monotonicSeconds();
            }
            continue;
        }
        if (status != kIOReturnSuccess) {
            fprintf(stderr, "La lectura USB terminó (0x%08x).\n", status);
            break;
        }
        if (length < 44 || readLE32(buffer) != 1) continue;
        uint32_t frameOffset = 8 + readLE32(buffer + 8);
        uint32_t frameLength = readLE32(buffer + 12);
        if (frameOffset > length || frameLength > length - frameOffset || frameLength < 42) continue;
        UInt8 *ethernet = buffer + frameOffset;
        if (ethernet[12] != 0x08 || ethernet[13] != 0x00) {
            if (ethernet[12] == 0x08 && ethernet[13] == 0x06) {
                // ARP is the goggles' confirmation that it is resolving the
                // host.  Reply and send the subscription again now that its
                // IP-to-MAC cache is populated.
                sendARPReply(interface, outputPipe, gogglesMAC);
                sendIPv4UDP(interface, outputPipe, gogglesMAC, 12346, 9003, handshake, sizeof(handshake));
            }
            continue;
        }
        UInt8 *ip = ethernet + 14;
        UInt8 ipLength = (ip[0] & 0x0f) * 4;
        if (ipLength < 20 || ip[9] != 17 || frameLength < 14 + ipLength + 8) continue;
        UInt8 *udp = ip + ipLength;
        UInt16 sourcePort = ((UInt16)udp[0] << 8) | udp[1];
        UInt16 destinationPort = ((UInt16)udp[2] << 8) | udp[3];
        UInt16 udpLength = ((UInt16)udp[4] << 8) | udp[5];
        if (sourcePort != 9003 || destinationPort != 12346 || udpLength < 8 || udpLength > frameLength - 14 - ipLength) continue;
        UInt8 *message = udp + 8;
        UInt16 messageLength = udpLength - 8;
        if (messageLength < 8 || (readLE32(message) & 0x7fff) != messageLength ||
            message[2] != handshake[2] || message[3] != handshake[3] || message[7] != xorHeader(message)) continue;
        packets++;
        session[0] = message[2]; session[1] = message[3];
        if (message[6] == 0) printf("  confirmación UDP recibida.\n");
        if (message[6] != 2 || messageLength < 20) continue;
        videoPackets++;
        videoBytes += messageLength - 20;
        if (videoPackets <= 8) {
            printf("VIDEO_HEADER ");
            for (int i = 0; i < 20; i++) printf("%02x", message[i]);
            printf("\n");
        }
        assembleVideo(units, message, messageLength);
        if (monotonicSeconds() - lastStats >= 1) {
            printf("STREAM_STATS packets=%d bytes=%zu\n", videoPackets, videoBytes);
            lastStats = monotonicSeconds();
        }
        UInt8 packetCount = message[17] & 0x7f;
        unsigned int packetPart = (message[17] >> 7) | ((unsigned int)message[18] << 1) | ((unsigned int)message[19] << 9);
        if (packetPart + 1 == packetCount) {
            UInt8 ack[] = {
                0x1e, 0x80, session[0], session[1], 0x00, 0x00, 0x04, 0x00,
                0, 0, 0, 0, 0, 0, 0xd0, 0xe9, 0xd0, 0xe9, 0, 0,
                0xd0, 0xe9, 0xd8, 0xe9, 0, 0, 0, 0, 0, 0,
            };
            ack[8] = message[4]; ack[9] = message[5]; ack[10] = message[4]; ack[11] = message[5];
            ack[7] = xorHeader(ack);
            sendIPv4UDP(interface, outputPipe, gogglesMAC, 12346, 9003, ack, sizeof(ack));
        }
    }
    printf("LiveView: %d paquete(s) UDP, %d de vídeo, %zu bytes H.264.\n", packets, videoPackets, videoBytes);
    for (int i = 0; i < kAssemblySlots; i++) clearUnit(&units[i]);
    free(units);
    return videoPackets > 0 ? 0 : 3;
}

static void printFrameSummary(const UInt8 *buffer, UInt32 length) {
    if (length < 44 || readLE32(buffer) != 1) {
        printf("  transferencia no-RNDIS de %u bytes\n", length);
        return;
    }
    const uint32_t payloadLength = readLE32(buffer + 12);
    const uint32_t payloadOffset = readLE32(buffer + 8);
    const uint32_t offset = 8 + payloadOffset;
    if (offset > length || payloadLength > length - offset || payloadLength < 14) {
        printf("  trama RNDIS con longitud no válida (%u bytes)\n", length);
        return;
    }
    const UInt8 *ethernet = buffer + offset;
    const uint16_t etherType = ((uint16_t)ethernet[12] << 8) | ethernet[13];
    printf("  trama Ethernet: %u bytes, EtherType 0x%04x\n", payloadLength, etherType);
}

static int listenForRNDISFrames(IOUSBInterfaceInterface300 **interface, UInt8 inPipe) {
    if (setRNDISPacketFilter(interface) != 0) {
        return 1;
    }
    printf("Escuchando RNDIS durante 5 segundos; no se envían comandos DJI ni paquetes UDP.\n");
    int frames = 0;
    for (int attempt = 0; attempt < 20; attempt++) {
        UInt8 buffer[16384] = {0};
        UInt32 length = sizeof(buffer);
        IOReturn status = (*interface)->ReadPipeTO(interface, inPipe, buffer, &length, 250, 250);
        if (status == kIOReturnSuccess && length > 0) {
            frames++;
            printFrameSummary(buffer, length);
        } else if (status != kIOReturnTimeout && status != kIOUSBTransactionTimeout) {
            fprintf(stderr, "  lectura RNDIS terminó con 0x%08x.\n", status);
            break;
        }
    }
    printf("Escucha finalizada: %d trama(s) recibida(s).\n", frames);
    return 0;
}

int main(int argc, const char *argv[]) {
    // Mandatory identity for streaming: never open the first matching goggles.
    uint64_t selectedID = 0;
    if (argc == 4 && strcmp(argv[2], "--device-id") == 0) {
        char *end = NULL;
        errno = 0;
        selectedID = strtoull(argv[3], &end, 10);
        if (errno || !end || *end || !selectedID || argv[3][0] == '-') return 2;
        argc = 2;
    }
    if (argc > 1 && !selectedID) {
        fprintf(stderr, "Selecciona una interfaz concreta: --stream --device-id ID\n");
        return 2;
    }
    signal(SIGINT, requestStop);
    signal(SIGTERM, requestStop);
    signal(SIGPIPE, SIG_IGN);
    const int initialize = argc == 2 && strcmp(argv[1], "--initialize") == 0;
    const int listen = argc == 2 && strcmp(argv[1], "--listen") == 0;
    const int capture = argc == 3 && strcmp(argv[1], "--capture") == 0;
    const int live = (argc == 2 && (strcmp(argv[1], "--live-probe") == 0 || strcmp(argv[1], "--stream") == 0)) || capture;
    streamForever = argc == 2 && strcmp(argv[1], "--stream") == 0;
    if (streamForever) videoOutput = stdout;
    if (capture) {
        videoOutput = fopen(argv[2], "wb");
        if (!videoOutput) { perror("No se pudo crear la captura H.264"); return 1; }
    }
    if (argc > 1 && !initialize && !listen && !live) {
        fprintf(stderr, "Uso: goggles-rndis-probe [--initialize|--listen|--live-probe|--stream|--capture archivo.h264]\n");
        return 2;
    }
    CFMutableDictionaryRef matching = IOServiceMatching("IOUSBHostInterface");
    io_iterator_t iterator = IO_OBJECT_NULL;
    kern_return_t result = IOServiceGetMatchingServices(kIOMainPortDefault, matching, &iterator);
    if (result != KERN_SUCCESS) {
        fprintf(stderr, "No se pudo enumerar interfaces USB (0x%08x).\n", result);
        return 1;
    }

    io_service_t service = IO_OBJECT_NULL;
    while ((service = IOIteratorNext(iterator)) != IO_OBJECT_NULL) {
        const uint32_t vendor = unsignedProperty(service, CFSTR("idVendor"));
        const uint32_t product = unsignedProperty(service, CFSTR("idProduct"));
        const uint32_t number = unsignedProperty(service, CFSTR("bInterfaceNumber"));
        uint64_t registryID = 0;
        IORegistryEntryGetRegistryEntryID(service, &registryID);
        if (selectedID && registryID != selectedID) { IOObjectRelease(service); continue; }
        if (vendor != kDJIVendorID || product != kGoggles3ProductID || number != kRNDISDataInterfaceNumber) {
            IOObjectRelease(service);
            continue;
        }

        IOCFPlugInInterface **plugin = NULL;
        SInt32 score = 0;
        IOReturn status = IOCreatePlugInInterfaceForService(
            service,
            kIOUSBInterfaceUserClientTypeID,
            kIOCFPlugInInterfaceID,
            &plugin,
            &score
        );
        if (status != kIOReturnSuccess || plugin == NULL) {
            fprintf(stderr, "No se pudo crear el cliente IOKit de la interfaz RNDIS (0x%08x).\n", status);
            IOObjectRelease(service);
            IOObjectRelease(iterator);
            return 1;
        }

        IOUSBInterfaceInterface300 **interface = NULL;
        HRESULT queryStatus = (*plugin)->QueryInterface(
            plugin,
            CFUUIDGetUUIDBytes(kIOUSBInterfaceInterfaceID300),
            (LPVOID)&interface
        );
        (*plugin)->Release(plugin);
        if (queryStatus || interface == NULL) {
            fprintf(stderr, "No se pudo obtener la interfaz USB de usuario (0x%08x).\n", (unsigned int)queryStatus);
            IOObjectRelease(service);
            IOObjectRelease(iterator);
            return 1;
        }

        status = (*interface)->USBInterfaceOpen(interface);
        if (status != kIOReturnSuccess) {
            fprintf(stderr, "La interfaz RNDIS está ocupada o no puede abrirse (0x%08x). Cierra otras aplicaciones que usen las gafas.\n", status);
            (*interface)->Release(interface);
            IOObjectRelease(service);
            IOObjectRelease(iterator);
            return 1;
        }

        UInt8 endpointCount = 0;
        status = (*interface)->GetNumEndpoints(interface, &endpointCount);
        if (status != kIOReturnSuccess) {
            fprintf(stderr, "No se pudo leer el número de endpoints (0x%08x).\n", status);
        } else {
            printf("DJI Goggles 3 — interfaz 1 RNDIS abierta.\n");
            printf("Endpoints publicados: %u\n", endpointCount);
            for (UInt8 pipe = 1; pipe <= endpointCount; pipe++) {
                UInt8 direction = 0, endpoint = 0, transferType = 0, interval = 0;
                UInt16 maxPacketSize = 0;
                status = (*interface)->GetPipeProperties(
                    interface, pipe, &direction, &endpoint, &transferType, &maxPacketSize, &interval
                );
                if (status == kIOReturnSuccess) {
                    printf("  Pipe %u: %s endpoint %u, %s, paquete máximo %u bytes\n",
                        pipe, directionName(direction), endpoint, transferName(transferType), maxPacketSize);
                } else {
                    fprintf(stderr, "  Pipe %u: no disponible (0x%08x).\n", pipe, status);
                }
            }
        }

        UInt8 inputPipe = 0, outputPipe = 0;
        for (UInt8 pipe = 1; pipe <= endpointCount; pipe++) {
            UInt8 direction = 0, endpoint = 0, transferType = 0, interval = 0;
            UInt16 maxPacketSize = 0;
            if ((*interface)->GetPipeProperties(interface, pipe, &direction, &endpoint, &transferType, &maxPacketSize, &interval) == kIOReturnSuccess &&
                direction == kUSBIn && transferType == kUSBBulk) {
                inputPipe = pipe;
            } else if ((*interface)->GetPipeProperties(interface, pipe, &direction, &endpoint, &transferType, &maxPacketSize, &interval) == kIOReturnSuccess &&
                       direction == kUSBOut && transferType == kUSBBulk) {
                outputPipe = pipe;
            }
        }

        int initializationResult = 0;
        if (status == kIOReturnSuccess && initialize) {
            initializationResult = probeRNDISInitialization(interface, 1);
        } else if (status == kIOReturnSuccess && listen) {
            if (inputPipe == 0) {
                fprintf(stderr, "No se encontró un endpoint RNDIS de entrada.\n");
                initializationResult = 1;
            } else if (probeRNDISInitialization(interface, 0) == 0) {
                initializationResult = listenForRNDISFrames(interface, inputPipe);
            } else {
                initializationResult = 1;
            }
        } else if (status == kIOReturnSuccess && live) {
            if (inputPipe == 0 || outputPipe == 0) {
                fprintf(stderr, "No se encontraron los endpoints RNDIS necesarios.\n");
                initializationResult = 1;
            } else if (probeRNDISInitialization(interface, 0) == 0) {
                initializationResult = liveProbe(interface, inputPipe, outputPipe);
            } else {
                initializationResult = 1;
            }
        }

        if (listen || live) {
            UInt8 halt[12] = {0};
            writeLE32(halt, kRNDISHaltMessage);
            writeLE32(halt + 4, sizeof(halt));
            writeLE32(halt + 8, 4);
            sendEncapsulatedCommand(interface, halt, sizeof(halt));
        }
        if (videoOutput && videoOutput != stdout) fclose(videoOutput);
        (*interface)->USBInterfaceClose(interface);
        (*interface)->Release(interface);
        IOObjectRelease(service);
        IOObjectRelease(iterator);
        return status == kIOReturnSuccess ? initializationResult : 1;
    }

    IOObjectRelease(iterator);
    fprintf(stderr, "No se encontró la interfaz 1 de las DJI Goggles 3. Comprueba OTG y el cable de datos.\n");
    return 1;
}
