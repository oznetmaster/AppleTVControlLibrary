# Overview

AppleTvControlLibrary is a .NET implementation of the Apple TV **Companion Link** protocol. It
supports encrypted Companion Link sessions used for remote input, touch control, media control,
power-state updates, app launching, and user-account switching.

The implementation is scoped to Companion Link. It does not implement MRP, AirPlay 2, RAOP, or
DMAP/DACP.

## Packages

| Package | Purpose |
|---|---|
| `AppleTvControlLibrary` | Companion Link protocol, pairing, framing, encryption, OPACK, TLV8, and high-level commands. |
| `AppleTvControlLibrary.Discovery` | mDNS/DNS-SD discovery for `_companion-link._tcp` services. |
| `AppleTvControlLibrary.All` | Convenience meta-package that restores both independent libraries. |

The protocol and discovery packages are intentionally independent. A host with a known device
address can use the Companion Link package without multicast discovery; a host can also use the
discovery API independently.

## Protocol formats

Companion Link messages use OPACK, not JSON. Pairing and verification messages use TLV8. The WPF
reference host uses JSON only to persist its local paired-device credential model.

See the [Companion Link API reference](../api/index.md) and [Discovery API reference](../discovery-api/index.md).
