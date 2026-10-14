// swift-tools-version: 6.0
import PackageDescription
let package = Package(name: "Goggles3Multi", platforms: [.macOS(.v14)], products: [
    .executable(name: "Goggles3Multi", targets: ["Goggles3Multi"]),
    .executable(name: "goggles-multi-usb", targets: ["GogglesRNDISProbe"])
], targets: [
    .target(name: "GogglesUSBSupport"),
    .target(name: "MultiCore"),
    .executableTarget(name: "GogglesRNDISProbe", linkerSettings: [.linkedFramework("IOKit"), .linkedFramework("CoreFoundation")]),
    .executableTarget(name: "Goggles3Multi", dependencies: ["GogglesUSBSupport", "MultiCore"]),
    .testTarget(name: "MultiCoreTests", dependencies: ["MultiCore"])
])
