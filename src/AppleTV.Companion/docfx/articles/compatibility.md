# Compatibility and Limitations

## Supported runtimes

- .NET Framework 4.7.2
- .NET 10

The protocol layer uses the same BouncyCastle-based cryptographic path on both target frameworks.
If a `net472` deployment uses Mono, validate that runtime separately, especially socket options and
`Span<T>`/`ValueTask` behavior.

## Hardware coverage

Testing has focused on Apple TV 4K models. Pre-4K Apple TV hardware, including Apple TV HD/A1625,
is not part of the current real-hardware test matrix. It may work, but is not guaranteed until
validated on that hardware.

## Optional Companion capabilities

App lists and switchable-account lists are optional. An empty result is a normal device/tvOS
outcome and should not prevent a session from working.

The protocol can list switchable accounts and request a switch, but it does not report which
account is currently active. A host only knows that state after it successfully switches an
account itself; a change made through the device UI or another client is not pushed to Companion.

## Out of scope

- Now-playing metadata (MRP)
- AirPlay 2, RAOP, and DMAP/DACP
- Absolute channel selection
- A native mute command; hosts can implement mute with supported volume operations when the device
  advertises volume capability
