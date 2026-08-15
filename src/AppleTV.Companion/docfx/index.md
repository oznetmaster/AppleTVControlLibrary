# AppleTvControlLibrary

`AppleTvControlLibrary` is a .NET client library for the Apple TV **Companion Link** and **MRP**
protocols. Companion Link is the encrypted channel used by tvOS remotes and the Apple TV Remote
app for remote input, touch, media control, volume, power state, app launching, and account
switching. MRP (Media Remote Protocol) is used for now-playing metadata, playback control, and
volume.

Apple, Apple TV, tvOS, AirPlay, and Siri are trademarks of Apple Inc. This project is an
independent, unofficial implementation and is not affiliated with, endorsed by, sponsored by, or
approved by Apple Inc.

## What this library provides

- Companion Link pairing, pair verification, encrypted framing, OPACK, and TLV8.
- HID remote buttons and touch-surface gestures.
- Media control and capability-aware volume operations.
- Push-based system power-state updates.
- Optional app listing/launching and user-account switching.
- MRP pairing, framing, and protobuf-based now-playing and playback control.
- mDNS/DNS-SD discovery for both Companion Link and MRP through a separately usable discovery library.
- .NET Framework 4.7.2 and .NET 10 support.

## Documentation sections

- [Overview](articles/overview.md)
- [Getting started](articles/getting-started.md)
- [Pairing and credentials](articles/pairing.md)
- [Compatibility and limitations](articles/compatibility.md)
- [Companion Link API reference](api/index.md)
- [Discovery API reference](discovery-api/index.md)
- [MRP overview](articles/mrp-overview.md)
- [MRP getting started](articles/mrp-getting-started.md)
- [MRP pairing and credentials](articles/mrp-pairing.md)
- [MRP compatibility and limitations](articles/mrp-compatibility.md)
- [MRP API reference](mrp-api/index.md)
