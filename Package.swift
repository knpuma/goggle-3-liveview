// swift-tools-version: 6.0
import PackageDescription

let package = Package(
    name: "Goggles3Mac",
    platforms: [.macOS(.v14)],
    products: [
        .executable(name: "goggles-usb", targets: ["GogglesUSB"]),
        .executable(name: "goggles-rndis-probe", targets: ["GogglesRNDISProbe"]),
        .executable(name: "Goggles3Mac", targets: ["Goggles3Mac"])
    ],
    targets: [
        .target(name: "GogglesUSBSupport"),
        .executableTarget(
            name: "GogglesUSB",
            dependencies: ["GogglesUSBSupport"]
        ),
        .executableTarget(
            name: "GogglesRNDISProbe",
            linkerSettings: [
                .linkedFramework("IOKit"),
                .linkedFramework("CoreFoundation")
            ]
        ),
        .executableTarget(
            name: "Goggles3Mac",
            dependencies: ["GogglesUSBSupport"]
        ),
        .testTarget(
            name: "GogglesUSBSupportTests",
            dependencies: ["GogglesUSBSupport"]
        )
    ]
)
