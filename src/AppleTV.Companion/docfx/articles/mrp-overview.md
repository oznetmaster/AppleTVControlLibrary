# MRP Overview

`AppleTvControlLibrary.Mrp` is a .NET client library for Apple TV's **MRP** (Media Remote Protocol),
used to receive now-playing metadata (title, artist, album, artwork, playback position) and to send
playback commands (play/pause, skip, volume). Apple TV no longer exposes MRP over a raw TCP socket;
this library speaks MRP tunneled over an AirPlay 2 connection, matching pyatv's current transport.

The implementation is scoped to MRP-over-AirPlay. It does not implement Companion Link, AirPlay 2
media/audio/video streaming, RAOP, or DMAP/DACP.

## Packages

| Package | Purpose |
|---|---|
| `AppleTvControlLibrary.Mrp` | MRP protocol, AirPlay 2 tunnel, protobuf messages, player/queue state tracking, and high-level commands. |
| `AppleTv.Hap` (shared, not separately packaged for MRP) | HAP pair-setup/pair-verify and ChaCha20-Poly1305 crypto, shared with the Companion Link library. |

## Relationship to Companion Link

Companion Link and MRP are separate tvOS protocols with different responsibilities: Companion Link
carries HID input, touch, and coarse media/volume/power control; MRP carries rich now-playing
metadata and fine-grained playback control. A host application that wants both remote input and
now-playing UI typically pairs and connects to both protocols independently - see
`src/AppleTv.Remote.Mrp.Wpf` and `src/AppleTv.Remote.Wpf` for separate reference hosts, one per
protocol.

## Protocol formats

MRP messages are length-prefixed Protocol Buffers frames (`ProtocolMessage`), not OPACK. Pairing
and verification reuse the same HAP TLV8 messages and SRP/Ed25519/X25519 primitives as Companion
Link, via the shared `AppleTv.Hap` library.

See the [MRP API reference](../mrp-api/index.md).
