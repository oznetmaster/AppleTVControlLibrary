# Overview

AppleTvControlLibrary is a .NET implementation of the Apple TV **Companion Link** and **MRP**
protocols. Companion Link is used for encrypted sessions handling remote input, touch control,
media control, power-state updates, app launching, and user-account switching. MRP (Media Remote
Protocol) is used for now-playing metadata, playback control, and volume.

The implementation does not cover AirPlay 2, RAOP, or DMAP/DACP.

## Packages

| Package | Purpose |
|---|---|
| `AppleTvControlLibrary` | Companion Link protocol, pairing, framing, encryption, OPACK, TLV8, and high-level commands. |
| `AppleTvControlLibrary.Mrp` | MRP protocol client, pairing, framing, and protobuf-based messaging. |
| `AppleTvControlLibrary.Discovery` | mDNS/DNS-SD discovery for both Companion Link (`_companion-link._tcp`) and MRP services. |
| `AppleTvControlLibrary.All` | Convenience meta-package that restores all of the independent libraries. |

The protocol and discovery packages are intentionally independent. A host with a known device
address can use the Companion Link or MRP package without multicast discovery; a host can also use
the discovery API independently. Shared cryptographic and pairing logic used by both protocols
lives in `AppleTv.Hap`.

## Protocol formats

Companion Link messages use OPACK, not JSON. Pairing and verification messages use TLV8. MRP
messages use length-prefixed [Protocol Buffers](https://protobuf.dev/) ("protobuf"), Google's
language-neutral binary serialization format. The WPF reference hosts use JSON only to persist
their local paired-device credential models.

See the [Companion Link API reference](../api/index.md), [MRP API reference](../mrp-api/index.md),
[Discovery](discovery.md), and [Discovery API reference](../discovery-api/index.md).
