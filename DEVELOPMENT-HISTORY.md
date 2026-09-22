# Development and validation history

See the [product changelog](CHANGELOG.md) for shipped changes. This document preserves test, CI and build history. Dated development entries describe work at that time, not a published product version or completed acceptance. Version headings identify the release alongside which development work was recorded.

## Where changes belong

- Product changelog and product release notes: shipped behavior, API, compatibility, fixes and runtime dependencies. Mention validation briefly when it helps explain a fix.
- This history: test coverage, CI, build tooling, work on pending versions. Split mixed entries so the product effect remains easy to find.
- Testing and workflow guides: current setup and operating instructions.
- Test-only or documentation-only changes do not require a product release.

<!-- development-history -->

## Offline release workflow option - 2026-09-15 (no package release)

- Allow an explicit manual release when local hardware or the self-hosted runner is unavailable, with the reason and exact source recorded in the workflow summary.
- Keep hosted source validation mandatory and preserve all build, test and packaging steps. No runtime, API or package-version changes.

## CI validation - 2026-09-15 (no package release)

- Revalidate the current default-branch source after successful release workflows, including version commits created by GitHub Actions.
- Allow maintainers to configure exact-source, App-specific checks that must pass before publishing through `RELEASE_REQUIRED_CHECKS`; missing, failed or unconfirmed checks block the release.

## Test and development tooling - 2026-09-15 (no library release)

- Generate XML documentation under each framework's build output. Parallel framework builds no longer overwrite or corrupt the same tracked XML file. Library runtime code and public APIs are unchanged.

- Add dedicated pull-request and branch CI tests on net472 and .NET 10, excluding live tests and retaining per-runtime results. Test/CI-only change; no library behavior or package release.

## [2.2.0] - 2026-08-14

- Converted allocation-heavy hex decode, DNS TXT/name parsing, mDNS flag-string parsing, and
  HTTP/RTSP header parsing to use `Span<T>`/`ReadOnlySpan<T>` instead of `Substring` and per-value
  byte-array copies (`AppleTvControlLibrary.Discovery`, `AppleTvControlLibrary` HAP layer, and
  `AppleTvControlLibrary.Mrp`). This is an internal efficiency pass only; no public API or wire
  behavior changed, and all existing tests pass unmodified on both `net472` and `net10.0`.

## [2.0.0] - 2026-08-05

- `tests/AppleTv.Mrp.Tests` and `tests/AppleTv.Mrp.FakeDevice`, a full MSTest suite (multi-targeted
  `net472`/`net10.0`) covering MRP pairing, protocol framing, player-state tracking, artwork
  fetch/fallback, push updates, and power-state derivation against an in-process fake Apple TV.

- `tests/AppleTv.Hap.Tests`, unit tests for the shared HAP pairing/crypto library.

## [1.1.2] - 2026-08-03

- Updated the test projects to MSTest 4.3.3 and its current assertion APIs.

- Replaced obsolete MSTest `DataTestMethod` attributes with `TestMethod` while preserving the
  existing `DataRow` test coverage, eliminating MSTEST0044 analyzer warnings.

## [1.1.0] - 2026-08-03

- Concurrent fake-device tests covering response correlation for overlapping OPACK exchanges and
  touch-swipe traffic interleaved with status queries.

## [1.0.0] - 2026-08-02

- A fake Companion Link device, ported from pyatv's test fixtures, used to validate pairing,
  verification, and session bring-up without physical hardware.
## 2026-09-22 — Compatibility and dependency audit

- Update NUnit test SDK/analyzers and document exact test dependencies in tests/README.md.
- Add eight offline credential-file compatibility cases across both desktop sample models; run the same source on net472 and .NET 10.
- Audit JSON surfaces, logging and stable direct/transitive dependencies. Binary protocol codecs remain necessary; no raw JSON API, Newtonsoft or log4net is present in the library.
