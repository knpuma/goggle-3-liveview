import Foundation

public enum InputSelection: Hashable, Sendable, Codable {
    case usb(UInt64)
    case demo(Int)
    public var isDemo: Bool { if case .demo = self { true } else { false } }
}

/// A physical USB interface can belong to only one tile. Registry identities
/// expire on unplug: a replacement device at the same port is never claimed silently.
public struct SlotAssignments: Sendable {
    public static let capacity = 4
    public private(set) var sources: [InputSelection?] = Array(repeating: nil, count: capacity)
    public init() {}
    public enum AssignmentError: Error { case invalidSlot, duplicateDevice, invalidDemo }
    public mutating func assign(_ source: InputSelection?, to slot: Int) throws {
        guard sources.indices.contains(slot) else { throw AssignmentError.invalidSlot }
        if case .usb = source, sources.enumerated().contains(where: { $0.offset != slot && $0.element == source }) {
            throw AssignmentError.duplicateDevice
        }
        if case .demo(let number) = source, !(1...4).contains(number) { throw AssignmentError.invalidDemo }
        sources[slot] = source
    }
    public func available(_ source: InputSelection, for slot: Int) -> Bool {
        !sources.enumerated().contains { $0.offset != slot && $0.element == source }
    }
}
