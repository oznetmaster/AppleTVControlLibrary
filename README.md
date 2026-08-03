# AppleTvControlLibrary

A .NET client library for the Apple TV **Companion Link** protocol - the channel tvOS remotes and
the Apple TV Remote app use for HID input (remote buttons, touch), media transport control,
volume, and power state. It targets `net472` and `net10.0` from a single, non-conditional codebase.

The protocol implementation is limited to Companion Link, with companion-service discovery
provided separately. It does not implement MRP, AirPlay 2, RAOP, or DMAP/DACP.

> **Trademark notice and disclaimer:** Apple, Apple TV, tvOS, AirPlay, and Siri are trademarks of
> Apple Inc., registered in the U.S. and other countries. This project is an independent,
> unofficial implementation of a reverse-engineered protocol and is **not affiliated with,
> endorsed by, sponsored by, or approved by Apple Inc.** in any way. "Apple TV" and other Apple
> product names are used solely to describe compatibility and interoperability. No Apple software,
> assets, or confidential documentation are included in or derived for this repository.

## Documentation

API documentation is published at [oznetmaster.github.io/AppleTVControlLibrary](https://oznetmaster.github.io/AppleTVControlLibrary/).

## Packages

| Package | Contents |
|---|---|
| `AppleTvControlLibrary` | Protocol, framing, crypto, pairing/verification, OPACK/TLV8 codecs, and the high-level `CompanionApi`. |
| `AppleTvControlLibrary.Discovery` | mDNS/DNS-SD discovery of Companion Link services, isolated behind `ICompanionDiscovery` so it can be swapped per host. |
| `AppleTvControlLibrary.All` | Convenience meta-package that installs both independent libraries; it contains no DLL of its own. |

`AppleTvControlLibrary` and `AppleTvControlLibrary.Discovery` are intentionally independent: a
host with a known Apple TV address can use the protocol library without multicast discovery, and
a host can use the discovery API independently. Install `AppleTvControlLibrary.All` when both are
wanted; it restores the two library packages automatically.

## Repository layout

| Path | Contents |
|---|---|
| `src/AppleTV.Companion` | Core Companion Link library and the DocFX documentation source. |
| `src/AppleTV.Companion.Discovery` | Independently usable mDNS/DNS-SD discovery library. |
| `src/AppleTV.Companion.All` | Meta-package project that restores both library packages. |
| `src/AppleTv.Remote.Wpf` | WPF reference host for scanning, pairing, connecting, and remote control. |
| `tests/AppleTV.Companion.Tests` | Unit and protocol test suite. |
| `tests/AppleTV.Companion.FakeDevice` | Fake Apple TV used by protocol and session integration tests. |
| `tests/AppleTV.Companion.LiveTests` | Opt-in tests for a real Apple TV. |
| `tools/AppleTV.Companion.RemoteTool` | Command-line remote-control utility. |
| `tools/AppleTV.Companion.ScanTool` | Command-line Companion Link discovery utility. |
| `.github` | Continuous integration, publishing, and GitHub Pages workflows. |

### WPF reference host

`src/AppleTv.Remote.Wpf` is a working desktop reference host rather than a reusable UI component.
It demonstrates the full application workflow: scan for devices, pair once, persist credentials,
reconnect, and control a selected Apple TV. Its UI includes directional and media controls,
capability-aware volume and mute controls, power-state updates, app launching, switchable-account
selection, and reactive text entry when the Apple TV keyboard gains focus.

<img src="https://oznetmaster.github.io/AppleTVControlLibrary/images/companion-link-remote.png" alt="Companion Link Remote WPF reference host" width="180" />

Use it as an integration example for credential storage, connection lifecycle handling, and the
high-level `CompanionApi`; hosts are expected to provide their own UI and secure credential-store
implementation.

## What it can do

- Discover Apple TVs advertising Companion Link over mDNS (`_companion-link._tcp`).
- Pair with a device (HAP pair-setup over SRP) and persist the resulting credentials.
- Establish an encrypted session (HAP pair-verify, ChaCha20-Poly1305) and bring up a Companion
  Link session (`_systemInfo`, `_touchStart`, `_sessionStart`, `TVRCSessionStart`, `_tiStart`).
- Send HID commands (directional pad, menu/home, volume, play/pause, Siri, etc.) and touch/swipe
  events.
- Send media-control commands (play/pause/skip, absolute volume) gated by the device's advertised
  capabilities.
- Track power state (asleep/awake/screensaver/idle) via pushed `SystemStatus`/`TVSystemStatus`
  events, and toggle power via sleep/wake HID commands.
- List launchable apps (`AppList()`) and launch an app by bundle identifier or deep-link URL
  (`LaunchApp(...)`). Treat an empty or missing app list as a normal outcome, not an error - some
  tvOS builds do not populate it, and callers should not hard-depend on the list being non-empty.
- List switchable user accounts (`AccountList()`) and switch the active account
  (`SwitchAccount(...)`). Same graceful-degradation rule as app listing: an empty or missing
  account list is a normal outcome, not an error. **There is no way to query which account is
  currently active** - `FetchUserAccountsEvent` returns only the switchable list, and nothing in
  `_systemInfo` or the `_iMC` event fills the gap. Callers can only know the current account after
  they themselves issue a successful `SwitchAccount(...)`; a switch made on the device itself
  (Control Center, the user icon, another client) is invisible to this library, with no event
  pushed for it.

## What it does not do

- No now-playing metadata (title, artist, artwork, position) - that is MRP territory and out of
  scope for this library.

- No absolute channel selection (only channel increment/decrement).
- No mute command exists on the wire; callers build it from `SetVolume(0.0)` plus a stashed
  previous level, gated on the device advertising volume control at all.

## Getting started

```csharp
using AppleTvControlLibrary.Discovery.Companion;
using AppleTvControlLibrary.Protocol;

// 1. Discover devices.
var discovery = new MulticastCompanionDiscovery ();
var results = await discovery.ScanAsync (TimeSpan.FromSeconds (5));

// 2. Pair (once per device) using AppleTvDeviceManager-style orchestration in your host app,
//    or drive SrpAuthHandler / CompanionProtocol directly for full control.

// 3. After pairing, connect and issue commands via CompanionApi.
await api.ConnectAsync ();
await api.SendHidCommandAsync (down: true, command: HidCommand.Select);
await api.SendHidCommandAsync (down: false, command: HidCommand.Select);
```

### Async APIs

Version 1.1.0 is async-first. Use the `Async` API variants for all protocol operations, including
connection, commands, event subscriptions, and discovery. The original synchronous APIs remain
available for source compatibility but are obsolete; migrate to their `Async` equivalents to avoid
blocking application threads.

See `src/AppleTv.Remote.Wpf` in the source repository for a complete reference host application
(scan, pair, connect, and drive a remote-control UI).

## Supported platforms

- .NET Framework 4.7.2 (desktop, including Mono hosts - validate independently; see notes below)
- .NET 10

`net472` support relies on `Span<T>`/`Memory<T>` polyfills and compiler-attribute shims so the
same C# 13 codebase compiles on both targets with no `#if` branching in the protocol layer. If a
`net472` build is destined for a Mono runtime rather than desktop .NET Framework, validate
`Span<T>`/`ValueTask` behavior and socket options on that runtime specifically - a green build on
Windows `net472` does not guarantee equivalent behavior on Mono.

## Hardware compatibility

This library has been tested against Apple TV 4K models only. Older, pre-4K devices (e.g. Apple TV
HD / A1625, and earlier generations) are **not part of the test matrix** and have not been
validated against real hardware. Companion Link should work the same way on those devices, and
nothing in the protocol implementation deliberately excludes them, but "should work" is not a
guarantee - treat pre-4K support as unverified until confirmed against real hardware, and please
report any issues you hit on that hardware.

## Protocol reference and correctness

This library is a from-scratch, byte-exact C# port of the Companion Link protocol as implemented
by [pyatv](https://github.com/postlund/pyatv) 0.18.0. Every protocol constant in the source carries
a comment citing the pyatv symbol it was ported from (see [ATTRIBUTIONS.md](ATTRIBUTIONS.md)), so
that a future re-vendor of pyatv can be reconciled with a diff rather than a re-derivation of the
whole protocol. See [THIRD-PARTY-NOTICES.txt](THIRD-PARTY-NOTICES.txt) for full license text of the
projects consulted during the port.

## License

MIT - see [LICENSE](LICENSE). Third-party attributions are listed in
[ATTRIBUTIONS.md](ATTRIBUTIONS.md) and [THIRD-PARTY-NOTICES.txt](THIRD-PARTY-NOTICES.txt).

## Trademarks

Apple, Apple TV, tvOS, AirPlay, and Siri are trademarks of Apple Inc. This project is not
affiliated with, endorsed by, or sponsored by Apple Inc. All product names, logos, and brands
referenced are property of their respective owners and are used here for identification purposes
only.

## Changelog

See [CHANGELOG.md](CHANGELOG.md).
