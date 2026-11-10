# AppleTVControlLibrary 2.2.8

This patch corrects the audited reliability issues while retaining the existing public API and supported frameworks.

- Companion disposal faults pending exchanges immediately and defers gate cleanup until admitted sends and authentication exchanges exit. DisposeAsync shares one completion task and waits for that drain; caller-owned transport timeouts remain the responsibility of the application. MRP rejects cancellation before submitting a frame and reports cancelled response waits as cancellation rather than protocol timeouts. Cancellation after submission does not prove that a command was undone. Pairing storage remains application-owned.

## Validation

- Offline workstation tests passed in Debug and Release on .NET 10 and .NET Framework 4.7.2: AppleHap net10.0: 56; AppleHap net472: 56; AppleCompanion net10.0: 114; AppleCompanion net472: 114; AppleMrp net10.0: 87; AppleMrp net472: 87. No failures or skips.
- 56 selected tests passed in the shared 374-case processor run on Mono 6.12.0.107 (32-bit), with no failures or skips. The merged host's resolved dependency graph was tested; these are not eight independent release-package tests.
- Fake transports and credentials exercised ordering, cancellation, shutdown and failure recovery. No new household-device or cloud control was performed. Pairing storage is unchanged.
- Strict XML, formatting and style checks passed. Package and companion-app preparation is verified separately by the shared release pipeline.

See [the changelog](https://github.com/oznetmaster/AppleTVControlLibrary/blob/v2.2.8/CHANGELOG.md) and [validation provenance](https://github.com/oznetmaster/AppleTVControlLibrary/blob/v2.2.8/release-validation/v2.2.8.json).
