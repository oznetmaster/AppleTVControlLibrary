# Changelog

All notable changes to this project are documented in this file.

## [1.0.3] - 2026-08-03

### Fixed

- Updated the packaged README screenshot to use the GitHub Pages-hosted image so it renders on
  NuGet.org.
- Added explicit empty framework assets to `AppleTvControlLibrary.All`, eliminating the NU5128
  packaging warning while retaining its `net472` and `net10.0` dependency groups.

## [1.0.2] - 2026-08-03

### Changed

- Renamed the WPF reference host window from "Apple TV Remote" to "Companion Link Remote" to
  distinguish this independent application from Apple's product.

### Added

- Expanded the README with a repository layout, WPF reference-host guidance, and a current
  screenshot of the running Companion Link Remote application.

## [1.0.1] - 2026-08-02

### Fixed

- Made `AppleTvControlLibrary` and `AppleTvControlLibrary.Discovery` independent packages, as
  intended. The previously published Discovery package incorrectly declared a dependency on the
  Companion Link package.

### Added

- `AppleTvControlLibrary.All`, a NuGet-only convenience package that restores both independent
  libraries for consumers that want the complete Companion Link and mDNS discovery stack.

## [1.0.0] - 2026-08-02

### Added

- Companion Link discovery (`AppleTvControlLibrary.Discovery`) via mDNS/DNS-SD, isolated behind
  an interface so it can be swapped per host runtime.
- OPACK and TLV8 codecs, ported byte-for-byte from pyatv 0.18.0 with round-trip test coverage
  including sized-integer preservation and >255-byte TLV8 value chunking.
- Companion Link framing and ChaCha20-Poly1305 encryption/decryption, including the frame-type
  enum, big-endian length header, and zero-length-payload bypass.
- HAP pair-setup and pair-verify (SRP6a over a 3072-bit group, Ed25519, X25519, HKDF), matching
  pyatv's `ClientEncrypt-main` / `ServerEncrypt-main` key derivation.
- A fake Companion Link device, ported from pyatv's test fixtures, used to validate pairing,
  verification, and session bring-up without physical hardware.
- Companion Link session lifecycle (`_systemInfo`, `_touchStart`, `_sessionStart`,
  `TVRCSessionStart`, `_tiStart`) including the persistent, MAC-shaped `_i` identifier required
  for the device to keep pushing `TVSystemStatus` events and to avoid tvOS 18.4+ dropping the
  connection.
- HID command support (directional pad, menu/home, volume, playback, Siri, sleep/wake, etc.) and
  touch/swipe events.
- Media-control command support (play/pause/skip/absolute volume), gated on the
  `MediaControlFlags` advertised by the currently foregrounded app.
- Push-based power-state tracking via `SystemStatus`/`TVSystemStatus` events, replacing polling
  of `FetchAttentionState`.
- A WPF reference host application (`AppleTv.Remote.Wpf`) demonstrating discovery, pairing,
  connection, and remote-control UI wired to the library.
- Symbol-anchored citation comments on every ported protocol constant, referencing the pyatv
  0.18.0 file and symbol they were read from.

### Notes

- Companion Link only: no MRP, AirPlay 2, RAOP, or DMAP/DACP support is in scope.
- Validated against tvOS hardware in addition to the fake device; see project documentation for
  hardware-specific caveats (for example, `FetchAttentionState` behavior differences between
  Apple TV generations).
