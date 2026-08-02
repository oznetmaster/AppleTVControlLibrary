# Changelog

All notable changes to this project are documented in this file.

## [Unreleased]

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
