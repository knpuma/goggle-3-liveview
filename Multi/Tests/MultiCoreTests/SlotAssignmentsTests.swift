import Testing
@testable import MultiCore

@Test func fourIndependentDevices() throws {
    var slots = SlotAssignments()
    for i in 0..<4 { try slots.assign(.usb(UInt64(100+i)), to: i) }
    #expect(Set(slots.sources.compactMap { $0 }).count == 4)
}
@Test func duplicateUSBRejected() throws {
    var slots = SlotAssignments()
    try slots.assign(.usb(42), to: 0)
    #expect(throws: SlotAssignments.AssignmentError.duplicateDevice) { try slots.assign(.usb(42), to: 1) }
    #expect(slots.sources[1] == nil)
}
@Test func releaseDoesNotAffectOtherChannels() throws {
    var slots = SlotAssignments()
    try slots.assign(.usb(1), to: 0); try slots.assign(.usb(2), to: 1)
    try slots.assign(nil, to: 0)
    try slots.assign(.usb(1), to: 3)
    #expect(slots.sources[1] == .usb(2)); #expect(slots.sources[0] == nil)
}
@Test func sameAssignmentIsIdempotent() throws {
    var slots = SlotAssignments()
    try slots.assign(.usb(1), to: 0); try slots.assign(.usb(1), to: 0)
    #expect(slots.available(.usb(1), for: 0)); #expect(!slots.available(.usb(1), for: 1))
}
@Test func rejectFifthChannel() {
    var slots = SlotAssignments()
    #expect(throws: SlotAssignments.AssignmentError.invalidSlot) { try slots.assign(.usb(1), to: 4) }
}
@Test func realAndDemoStayDistinct() throws {
    var slots = SlotAssignments()
    try slots.assign(.usb(1), to: 0); try slots.assign(.demo(1), to: 1)
    #expect(slots.sources[0]?.isDemo == false); #expect(slots.sources[1]?.isDemo == true)
}
@Test func staleIdentityIsNotSubstituted() throws {
    var slots = SlotAssignments()
    try slots.assign(.usb(100), to: 0)
    #expect(slots.sources[0] != .usb(101))
}
@Test func demoRangeIsBounded() {
    var slots = SlotAssignments()
    #expect(throws: SlotAssignments.AssignmentError.invalidDemo) { try slots.assign(.demo(5), to: 0) }
}

@Test func selectorHidesUSBReservedByOtherChannels() throws {
    var slots = SlotAssignments()
    try slots.assign(.usb(10), to: 0)
    try slots.assign(.usb(20), to: 1)
    let detected: [UInt64] = [10, 20, 30]
    #expect(detected.filter { slots.available(.usb($0), for: 0) } == [10, 30])
    #expect(detected.filter { slots.available(.usb($0), for: 1) } == [20, 30])
    #expect(detected.filter { slots.available(.usb($0), for: 2) } == [30])
    #expect(detected.filter { slots.available(.usb($0), for: 3) } == [30])
}

@Test func releasingUSBRestoresItToOtherSelectors() throws {
    var slots = SlotAssignments()
    try slots.assign(.usb(10), to: 0)
    try slots.assign(nil, to: 0)
    for channel in 0..<4 { #expect(slots.available(.usb(10), for: channel)) }
    try slots.assign(.usb(10), to: 2)
    #expect(slots.available(.usb(10), for: 2))
    #expect(!slots.available(.usb(10), for: 0))
}
