# Changelog

All notable changes to this project are documented in this file.

## [1.1.3] - 2026-08-03

### Changed

- `CompanionApi.SystemStatusChanged` now raises on every pushed `SystemStatus`/`TVSystemStatus`
  state change (e.g. `Awake` to `Screensaver`), instead of only at the collapsed on/off boundary
  pyatv itself notifies on. `CurrentSystemStatus` always held the granular value; callers that only
  care about on/off can still derive it by comparing against `SystemStatus.Asleep`, while callers
  wanting finer detail are no longer prevented from observing it.

## [1.1.2] - 2026-08-03

### Changed

- Updated the test projects to MSTest 4.3.3 and its current assertion APIs.
- Removed unused direct NuGet dependencies while retaining the pinned `plist-cil` 2.2.0 package
  required for .NET Framework 4.7.2 compatibility.

### Fixed

- Replaced obsolete MSTest `DataTestMethod` attributes with `TestMethod` while preserving the
  existing `DataRow` test coverage, eliminating MSTEST0044 analyzer warnings.

## [1.1.1] - 2026-08-03

### Added

- `UnicastCompanionDiscovery`, which queries a known Apple TV IPv4 address over mDNS and returns
  the advertised Companion Link TCP endpoint and service metadata.
- `MulticastCompanionDiscovery.DiscoveryAsync`, which looks up an exact mDNS service instance name
  and completes once the matching Companion Link service is resolved.

### Fixed

- WPF startup auto-connect now handles a stale saved endpoint: it looks up the stored service name,
  verifies the discovered `rpmrtid` matches the paired device, persists only the refreshed address
  and port, and retries connection once. Unverified or ambiguous devices are not accepted.

## [1.1.0] - 2026-08-03

### Added

- Async-first Companion Link APIs for connection/session lifecycle, OPACK exchanges, HID and touch
  input, media control, text input, subscriptions, app/account operations, and power state.
- Cancellable asynchronous TCP connection setup for the WPF reference host.
- Concurrent fake-device tests covering response correlation for overlapping OPACK exchanges and
  touch-swipe traffic interleaved with status queries.

### Changed

- The WPF reference host and remote command-line tool now use the asynchronous library APIs.
- Outbound protocol sends are serialized; pending exchanges now fail promptly when the connection
  faults and use asynchronously scheduled task continuations.

### Deprecated

- Existing synchronous Companion API and protocol methods remain for source compatibility but are
  obsolete. Use the corresponding `Async` methods in new code.

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
