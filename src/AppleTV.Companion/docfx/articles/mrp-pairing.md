# MRP Pairing and Credentials

MRP-over-AirPlay pairing reuses the same HAP (HomeKit Accessory Protocol) pair-setup (SRP) and
pair-verify (Ed25519/X25519, ChaCha20-Poly1305) flows as Companion Link, driven over AirPlay's
HTTP/RTSP control channel instead of Companion's OPACK framing. The shared `AppleTv.Hap` library
(`SrpAuthHandler`, HAP TLV8 messages) is used directly by `AppleTv.Mrp` rather than being
reimplemented.

## Pairing flow

1. Discover the device's AirPlay service via `MulticastAirPlayDiscovery`.
2. Start pair-setup: `AirPlayHapPairSetupProcedure` drives the SRP exchange over an
	`HttpConnection` to the device's AirPlay HTTP endpoint, prompting for the on-screen PIN.
3. Persist the resulting long-term keypair/credentials (see `CredentialStore` in
	`src/AppleTv.Remote.Mrp.Wpf/Storage/CredentialStore.cs` for a JSON-backed example).
4. On subsequent connections, perform pair-verify (`AirPlayHapPairVerifyProcedure`) using the
	persisted credentials to establish an `Ap2Session`, then open an `AirPlayMrpConnection` data
	stream channel tunneled through that session for the encrypted MRP protobuf traffic.

## Credential storage

This library does not provide credential storage; `src/AppleTv.Remote.Mrp.Wpf`'s `CredentialStore`
is a reference implementation only. Hosts are expected to provide their own secure credential
storage appropriate to their platform and threat model, exactly as recommended for Companion Link
credentials.
