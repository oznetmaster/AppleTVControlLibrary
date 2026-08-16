# Changelog

All notable changes to this project are documented in this file.

## [2.2.3] - 2026-08-16

### Fixed

- Fixed a NuGet packaging bug: `AppleTvControlLibrary` and `AppleTvControlLibrary.Mrp` both reference
  the internal `AppleTv.Hap` project, which is not itself published as a NuGet package
  (`IsPackable=false`). Because the reference was a plain `ProjectReference`, NuGet emitted a
  dependency on a nonexistent `AppleTv.Hap` package in the generated `.nuspec`, which would fail to
  restore for any consumer installing from nuget.org. `AppleTv.Hap.dll` is now bundled directly into
  each package's `lib/net472` and `lib/net10.0` folders instead, and the phantom dependency entry is
  gone.
- Marking the `AppleTv.Hap` `ProjectReference` as `PrivateAssets=all` to fix the issue above also
  blocked transitive compile-time access to `AppleTv.Hap` types for repository projects that use its
  namespaces (`AppleTvControlLibrary.Auth`, `.Tlv8`, `.Opack`, `.Crypto`) directly. Added direct
  `AppleTv.Hap` project references to every affected downstream project (tests, CLI tools, WPF apps)
  so the solution builds cleanly again.
- Note: an earlier `v2.2.2` tag was cut for the first fix above but never completed a successful
  build/publish, so no `2.2.2` package was ever released; `2.2.3` is the first release to include
  this fix. Packaging only - no functional or public API changes.

## [2.2.1] - 2026-08-14

### Changed

- Removed further allocation-heavy `BitConverter`/LINQ/array-copy patterns identified in a follow-up
  review: OPACK float/double pack and unpack in `AppleTvControlLibrary` (HAP layer) now read/write
  directly through the `ReadOnlySpan<byte>`/`Span<byte>` via `MemoryMarshal`/`BinaryPrimitives`
  instead of allocating a temporary array per field; MRP protobuf varint encode/decode
  (`AppleTvControlLibrary.Mrp`) now uses a `stackalloc` buffer instead of recursive per-byte
  allocations; and HTTP/RTSP message formatting (`AppleTvControlLibrary.Mrp`) now encodes headers
  directly into the final combined buffer instead of an intermediate array. This is an internal
  efficiency pass only; no public API or wire behavior changed, and all existing tests pass
  unmodified on both `net472` and `net10.0`.

## [2.2.0] - 2026-08-14

### Changed

- Converted allocation-heavy hex decode, DNS TXT/name parsing, mDNS flag-string parsing, and
  HTTP/RTSP header parsing to use `Span<T>`/`ReadOnlySpan<T>` instead of `Substring` and per-value
  byte-array copies (`AppleTvControlLibrary.Discovery`, `AppleTvControlLibrary` HAP layer, and
  `AppleTvControlLibrary.Mrp`). This is an internal efficiency pass only; no public API or wire
  behavior changed, and all existing tests pass unmodified on both `net472` and `net10.0`.

## [2.1.1] - 2026-08-14

### Changed

- Restored the "all packages share one version" policy: every published NuGet package -
  `AppleTvControlLibrary`, `AppleTvControlLibrary.Discovery`, `AppleTvControlLibrary.All`, and
  `AppleTvControlLibrary.Mrp` - is now bumped to 2.1.1 together, and the release workflow's pack
  step once again forces the release tag's version onto every package (overriding each project's
  own `<Version>`), rather than letting each project's `.csproj` version drift independently. The
  brief per-project versioning scheme introduced to fix the `AppleTvControlLibrary.Mrp` 1.0.0/2.0.0
  mismatch (see `[2.0.1]` below) is superseded by this entry.

## [2.0.1] - 2026-08-14

### Fixed

- `AppleTvControlLibrary.Mrp`'s project version was corrected from `1.0.0` to `2.0.1`. The `v2.0.0`
  release ran under the release workflow's previous behavior of forcing the git tag version onto
  every NuGet package, so `AppleTvControlLibrary.Mrp` was actually published to NuGet as `2.0.0`,
  not `1.0.0` as the `[2.0.0]` entry below incorrectly stated. The `v2.1.0` release then packed and
  published a new, lower-numbered `1.0.0` package for `AppleTvControlLibrary.Mrp` from the stale
  project version, leaving both `1.0.0` and `2.0.0` listed on NuGet. This release bumps the project
  version past both to `2.0.1` so the package version history is monotonic again; no source or
  behavior change.

## [2.1.0] - 2026-08-14

### Changed

- `AppleTvControlLibrary.All` now installs all three independent libraries -
  `AppleTvControlLibrary` (Companion Link), `AppleTvControlLibrary.Mrp` (MRP), and
  `AppleTvControlLibrary.Discovery` (mDNS discovery for both protocols) - instead of only the
  Companion Link and Discovery packages. Bumped to 2.1.0.
- Fixed three XML doc `<see cref>` references in `AppleTvControlLibrary.Mrp` (`AirPlayMrpConnection`,
  `IMrpFrameConnection`, `MrpProtocol`) that could not be resolved and produced `InvalidCref` build
  warnings. Documentation-only change; no public API or behavior change.

## [2.0.0] - 2026-08-05

### Added

- `AppleTvControlLibrary.Mrp`, a new client library for Apple TV's MRP (Media Remote Protocol),
  tunneled over AirPlay 2, providing now-playing metadata (title, artist, album, artwork,
  position, playback rate) and playback control (play/pause/skip/seek, volume). Pairing and
  channel encryption reuse the shared `AppleTv.Hap` HAP pair-setup/pair-verify and
  ChaCha20-Poly1305 library, extracted from the Companion Link library so both protocols share one
  crypto/pairing implementation.
- `AppleTv.Remote.Mrp.Wpf`, a WPF reference host for MRP: pairing, connecting, and a now-playing UI
  with capability-gated transport controls.
- `tools/AppleTV.AirPlay.ScanTool` and `tools/AppleTV.AirPlay.RemoteTool`, command-line utilities
  for AirPlay discovery and MRP remote control.
- `tools/AppleTV.Mrp.ScanTool`, a command-line utility for locating MRP-over-AirPlay devices, built
  on the new `IMrpDiscovery`/`MulticastMrpDiscovery` mDNS discovery added to the existing
  `AppleTvControlLibrary.Discovery` package (no separate discovery package was needed).
- `tests/AppleTv.Mrp.Tests` and `tests/AppleTv.Mrp.FakeDevice`, a full MSTest suite (multi-targeted
  `net472`/`net10.0`) covering MRP pairing, protocol framing, player-state tracking, artwork
  fetch/fallback, push updates, and power-state derivation against an in-process fake Apple TV.
- `tests/AppleTv.Hap.Tests`, unit tests for the shared HAP pairing/crypto library.
- MRP documentation: new DocFX articles (overview, getting started, pairing and credentials,
  compatibility and limitations) and an MRP API reference section, published alongside the
  existing Companion Link documentation at the same GitHub Pages site.
- Root `README.md` now documents both libraries, the shared `AppleTv.Hap` dependency, both WPF
  reference hosts, and a combined tools-and-tests overview.

### Changed

- Extracted shared HAP crypto/pairing code (SRP, TLV8, Ed25519/X25519, ChaCha20-Poly1305 helpers)
  out of the Companion Link library into a new shared `AppleTv.Hap` library, consumed by both
  `AppleTvControlLibrary` and `AppleTvControlLibrary.Mrp`.
- Bumped `AppleTvControlLibrary`, `AppleTvControlLibrary.Discovery`, `AppleTvControlLibrary.All`,
  and `AppleTvControlLibrary.Mrp` to 2.0.0 to mark this repository-wide release. (Corrected
  2026-08-14: the `AppleTv.Mrp.csproj` file itself was left at `1.0.0`, but the release workflow's
  tag-forced versioning published the package to NuGet as `2.0.0`; see the `[2.0.1]` entry above.)
- The publish workflow now packs and publishes `AppleTvControlLibrary.Mrp` alongside the Companion
  Link packages, and builds/publishes `AppleTv.Remote.Mrp.Wpf` release assets alongside
  `AppleTv.Remote.Wpf`.

## [1.1.4] - 2026-08-04

### Added

- `CompanionApi.ConnectionClosed`, raised when the connection to the device is closed or lost,
  whether cleanly (the remote end closing the socket) or unexpectedly (a transport, decrypt, or
  dispatch failure). Mirrors pyatv's `DeviceListener.connection_lost`/`connection_closed`
  callbacks; inspect `ConnectionClosedEventArgs.Exception` (`null` for a clean close, non-null for
  an unexpected fault) to distinguish the two. This library does not implement automatic
  reconnection; consumers that want to reconnect must do so themselves in response to this event.
- `CompanionProtocol.ConnectionFaulted`, the lower-level event `CompanionApi.ConnectionClosed` is
  built on, for callers driving `CompanionProtocol` directly without `CompanionApi`.
- The WPF reference host now consumes `CompanionApi.ConnectionClosed` end-to-end:
  `AppleTvDeviceManager` exposes its own `ConnectionClosed` event and tears down its connection
  state when the underlying connection faults, and `MainViewModel` resets UI state accordingly. On
  an unexpected fault (not a user-initiated disconnect), the view model automatically retries the
  connection with a bounded, increasing backoff (2s/5s/10s/20s/30s), giving up with a "connect
  manually" status message if the device stays unreachable. A manual disconnect cancels any
  pending reconnect attempt.

### Fixed

- The WPF reference host's `TcpCompanionTransport` now faults the underlying `CompanionConnection`
  when the remote end closes the socket or the read loop fails unexpectedly, instead of only
  logging and silently exiting the read thread. This is what makes the new `ConnectionClosed`
  notification actually fire for socket-based connections.

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
