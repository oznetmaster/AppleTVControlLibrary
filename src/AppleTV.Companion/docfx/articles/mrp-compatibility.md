# MRP Compatibility and Limitations

## Supported platforms

- .NET Framework 4.7.2 (desktop, including Mono hosts - validate independently)
- .NET 10

`AppleTvControlLibrary.Mrp` targets the same `net472`/`net10.0` matrix as the Companion Link
library, using the same `Span<T>`/`Memory<T>` polyfills and compiler-attribute shims so a single
C# 13 codebase compiles on both targets with no `#if` branching in the protocol layer.

## Transport

MRP is tunneled over AirPlay 2 only. The library does not implement:

- A raw-TCP MRP transport (retired; see `archive/mrp-tcp-transport/README.md`).
- AirPlay 2 media/audio/video streaming - only the MRP control channel is tunneled.
- Companion Link (HID input, touch, coarse volume/power control) - pair and connect to the
  Companion Link library separately for that functionality.

## Hardware compatibility

This library has not been independently validated against real Apple TV hardware for MRP in the
same breadth as the Companion Link library's 4K-model test matrix. Treat MRP-over-AirPlay support
as validated primarily against the in-process fake device (`tests/AppleTv.Mrp.FakeDevice`) and the
pairing integration tests in `tests/AppleTv.Mrp.Tests`, and please report any issues you hit
against real hardware.

## Protocol reference and correctness

`AppleTvControlLibrary.Mrp` is a from-scratch C# port of pyatv's MRP implementation
(`pyatv/protocols/mrp`), including its protobuf message definitions. Protocol constants carry
citation comments to the pyatv source file and line they were ported from. See
[ATTRIBUTIONS.md](https://github.com/oznetmaster/AppleTVControlLibrary/blob/master/ATTRIBUTIONS.md)
and [THIRD-PARTY-NOTICES.txt](https://github.com/oznetmaster/AppleTVControlLibrary/blob/master/THIRD-PARTY-NOTICES.txt)
for licensing details.
