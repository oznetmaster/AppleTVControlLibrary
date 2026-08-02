# AppleTvControlLibrary

A .NET client library for the Apple TV **Companion Link** protocol - the channel tvOS remotes and
the Apple TV Remote app use for HID input (remote buttons, touch), media transport control,
volume, and power state. It targets `net472` and `net10.0` from a single, non-conditional codebase.

This library implements Companion Link only. It does not implement MRP, AirPlay 2, RAOP, or
DMAP/DACP.

## Packages

| Package | Contents |
|---|---|
| `AppleTvControlLibrary` | Protocol, framing, crypto, pairing/verification, OPACK/TLV8 codecs, and the high-level `CompanionApi`. |
| `AppleTvControlLibrary.Discovery` | mDNS/DNS-SD discovery of Companion Link services, isolated behind `ICompanionDiscovery` so it can be swapped per host. |

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

## What it does not do

- No now-playing metadata (title, artist, artwork, position) - that is MRP territory and out of
  scope for this library.
- No app launching or app listing.
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
api.Connect ();
api.SendHidCommand (down: true, command: HidCommand.Select);
api.SendHidCommand (down: false, command: HidCommand.Select);
```

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

## Changelog

See [CHANGELOG.md](CHANGELOG.md).
