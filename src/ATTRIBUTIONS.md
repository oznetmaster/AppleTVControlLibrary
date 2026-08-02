# Attributions

This library is a from-scratch C# port of protocol behavior implemented by third-party open
source projects. No source files from those projects are compiled into this library or
distributed with it; the vendored copies used during development live in a local, git-ignored
`/reference` directory that is never committed or published. This file records what was
consulted and why. Full license text for each project is reproduced in
[THIRD-PARTY-NOTICES.txt](THIRD-PARTY-NOTICES.txt).

## pyatv

**Project:** [pyatv](https://github.com/postlund/pyatv) 0.18.0
**Author:** Pierre Ståhl
**License:** MIT

pyatv is the primary reference for this library. It is the most complete open-source
implementation of Apple's Companion Link protocol, and every protocol constant, message shape,
and state-machine behavior in this codebase - HID command values, media-control flags, OPACK and
TLV8 wire formats, HAP pair-setup/pair-verify key derivation, frame/encryption semantics, and
session bring-up order - was read directly from pyatv's source, not from public protocol
documentation (which is stale and contains known transcription errors relative to the source).

Per this project's own contribution rules, every protocol constant in the C# source carries a
comment citing the pyatv symbol it was read from (for example:
`// pyatv/protocols/companion/api.py (HidCommand) — line 35-56 as of pyatv 0.18.0`). This is a
maintenance index, not attribution boilerplate: it lets a future re-vendor of pyatv (or a review
of a changed value) be resolved with a diff and a grep instead of re-deriving the protocol from
scratch. Citing a symbol's origin is not the same as reproducing pyatv's source; no file in this
library is a transliteration of a pyatv file, and no pyatv source ships in this library's
package or repository.

## srptools

**Project:** [srptools](https://github.com/idlesign/srptools) 1.0.1
**Author:** Igor "idle sign" Starikov
**License:** BSD 3-Clause

pyatv's own SRP (Secure Remote Password) implementation depends on srptools for its group
parameters, padding, and hashing conventions. Where this library's `SrpAuthHandler` needed to
match pyatv's SRP behavior exactly (the 3072-bit group prime and generator, big-endian padding
conventions, and the hash construction used for the session key), those values and behaviors were
traced back to srptools rather than assumed, since pyatv's `hap_srp.py` itself only re-exports
them. See the citation comments in `src/AppleTV.Companion/Auth/SrpAuthHandler.cs` for exact
symbol references.

## BouncyCastle

**Project:** [BouncyCastle.Cryptography](https://www.bouncycastle.org/csharp/) (NuGet package)
**License:** MIT (Bouncy Castle License)

Used as the cryptography provider for SRP, Ed25519, X25519, HKDF, and ChaCha20-Poly1305 on both
`net472` and `net10.0`, so the protocol layer has exactly one crypto code path across target
frameworks. This is a normal NuGet dependency, not a ported/vendored source tree.
