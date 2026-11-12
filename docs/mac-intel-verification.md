# Intel Mac build verification — 2026-11-12

Álvaro ([@alantoad](https://github.com/alantoad)) contributed the Intel Mac build support and reported testing with his DJI Goggles 3 on an Intel Mac. His submitted source was based on v0.3.1 (725be1edf5ec94173811d4e086af505d95934d8c). Comparison with that tag found build/packaging, download-documentation and FFmpeg binary changes only; no app or USB protocol source changes.

The contribution was integrated as separate Intel-only build scripts and a freshly compiled Intel FFmpeg executable. The original Apple Silicon scripts, decoder and release assets remain unchanged. Current English sources remain based on main; the Spanish package uses the original v0.3.1 sources plus the reviewed Intel build scripts. No submitted build caches or Git configuration are included.

## Checks

- Both English 0.3.2 and Spanish 0.3.1 apps compiled for x86_64 with macOS 14 minimum.
- App, USB receiver and decoder all contain x86_64 only. No Homebrew or contributor-machine runtime library paths.
- FFmpeg rebuilt from the unmodified 8.0.3 source archive after checking the existing pinned SHA-256. Cross compilation is explicitly enabled when the host architecture differs.
- Strict deep ad-hoc signature verification passed for both app bundles.
- Intel decoder executed under Rosetta and decoded a generated 30-frame H.264 sample. The YUV output matched the reference decoder byte for byte.
- Intel VideoToolbox encoding produced a valid H.264 MP4 containing all 30 frames.
- English Intel app opened under Rosetta; idle controls and the tutorial were checked through the actual UI.
- English DMG checksum verification and read-only mount passed; it contains the complete app bundle. Spanish DMG verification also passed.
- The existing Swift test passed natively on Apple Silicon. The Intel test bundle compiled, but the installed Swift testing helper is arm64-only and could not load it; this is not reported as a passing Intel unit-test run.

No independent physical Intel Mac / Goggles USB test was available in this review. Álvaro's hardware result is a contributor report, distinct from the Rosetta checks above. Packages remain ad-hoc signed and are not notarized by Apple.
