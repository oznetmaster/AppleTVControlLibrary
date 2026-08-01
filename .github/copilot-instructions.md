# Apple TV Companion Link — C# Porting Brief

**Target:** a Companion Link protocol library, multi-targeted `net10.0` + `net472`, C# 13 or later.
**Toolchain:** Visual Studio, GitHub Copilot agent mode, Claude Opus 5, MSTest.
**Reference:** pyatv 0.18.0 (MIT, Pierre Ståhl). Every constant in this document was read from that source tree, not from documentation.
**Scope:** Companion Link only. No MRP, no AirPlay 2, no RAOP, no DMAP/DACP. tvOS 18+.

---

## 0. Ground rules

1. **Every protocol constant carries a citation comment** naming the pyatv file and line it came from:
```csharp
	// pyatv/protocols/companion/protocol.py:41
	private const string SrpOutputInfo = "ClientEncrypt-main";
```
	An uncited constant is presumed invented and must be removed.

2. **Do not invent field names, type bytes, or enum values.** If a value cannot be found in the vendored pyatv tree, stop and ask. Do not infer it from the protocol documentation at pyatv.dev — that page is stale and contains known errors (§6).

3. **Test vectors are ported, not authored.** Where pyatv has a test, port the test. Do not write new assertions that encode your own understanding of the format.

4. **No `#if NET472` in the protocol layer.** All crypto comes from BouncyCastle on both TFMs so there is exactly one code path. Framework differences are absorbed by polyfills at the project level, not by conditional compilation in protocol code.

> **Why rule 1 exists.** This is not about model capability — you are running Opus 5, the same model that produced this brief. It is about task shape. Companion Link is an undocumented protocol with no error surface: when a field name is wrong, the Apple TV completes the handshake and then goes quiet. Any model asked to "implement" rather than "port" will emit a plausible constant, write a test asserting that constant, and go green. The failure is self-consistent, so neither the test suite nor a review that isn't checking against source can catch it. The citation comment is what makes that review mechanical.

---

## 1. Repository setup

```
/src
  AppleTv.Companion/            net10.0;net472
  AppleTv.Companion.Discovery/  mDNS, separated so it can be swapped per host
/tests
  AppleTv.Companion.Tests/      net10.0;net472 — MSTest
  AppleTv.Companion.FakeDevice/ the ported fake Apple TV
/reference
  pyatv-0.18.0/                 vendored, read-only, NOT compiled
/.github/copilot-instructions.md
/THIRD-PARTY-NOTICES.txt        pyatv MIT notice + BouncyCastle
```

Vendoring pyatv into the tree is deliberate — it puts the source where `#file:` can reach it, so Copilot ports rather than generates.

```sh
pip download pyatv==0.18.0 --no-deps --no-binary :all: -d /tmp/pyatv
tar xzf /tmp/pyatv/pyatv-0.18.0.tar.gz -C reference/
```

Exclude `/reference` from the solution and from analysis so the `.py` files never reach the compiler but stay visible to Copilot's file search.

**Multi-target the test project as well.** Byte-exactness is the whole point of this library, and the `net472` leg runs different `Span<T>`, `BitConverter`, and encoding implementations underneath. A suite that only runs on `net10.0` will not exercise the polyfill path where the divergence would actually show up.

### net472 configuration

```xml
<TargetFrameworks>net10.0;net472</TargetFrameworks>
<LangVersion>13.0</LangVersion>
```

C# 13 on `net472` is unsupported-but-working. What you need:

- `System.Memory` — `Span<T>`, `Memory<T>`, `ReadOnlySequence<T>`
- `System.Buffers` — pooled arrays for the frame reassembly buffer
- `Microsoft.Bcl.AsyncInterfaces` — `IAsyncEnumerable<T>`, `IAsyncDisposable`
- `System.Threading.Tasks.Extensions` — `ValueTask<T>`
- Generated attribute shims: `IsExternalInit` (records, `init`), `RequiredMemberAttribute`, `CompilerFeatureRequiredAttribute`, `CollectionBuilderAttribute` (collection expressions on your own types), `InlineArrayAttribute`

`PolySharp` or `Polyfill` generates the attribute set; pick one and keep it consistent across both projects.

**Genuinely blocked on net472**, because they need runtime support rather than just an attribute: `allows ref struct` generic constraints, and ref struct interface implementations. Neither is needed here — the codecs work fine with `ReadOnlySpan<byte>` parameters and array returns. If Copilot reaches for them, that is a signal it is designing rather than porting.

**Dependencies:** `BouncyCastle.Cryptography` 2.6.2 for both TFMs, plus MSTest and the polyfill package. If you ILRepack this library into a consuming assembly, align the BouncyCastle version with anything else already merged in to avoid duplicate-type conflicts.

**Mono.** If the `net472` leg is destined for a Mono runtime rather than desktop .NET Framework, validate there specifically — `Span<T>` and `ValueTask` behaviour, and socket options in particular, are not guaranteed to match a Windows `net472` build. A green suite on Windows `net472` is necessary but not sufficient.

---

## 2. Work packages

Each has a green-test exit criterion. **Do not start the next until the current one is green.** One Copilot thread per package — the boundaries exist so no single session has to hold the whole protocol in context.

### WP1 — OPACK codec ⭐ start here

| | |
|---|---|
| **Source** | `pyatv/support/opack.py` (241 lines) |
| **Vectors** | `tests/support/test_opack.py` (440 lines, 41 tests) |
| **Exit** | All 41 ported tests green on **both** TFMs |
| **Hardware** | None |

Port the vectors as `[TestMethod]` with `[DataRow]` over hex-string/expected pairs — the pyatv tests are mostly single-assert round-trips and collapse neatly into data rows.

Traps, all confirmed from source:

- **Strings and byte arrays use different length ladders.** Strings `0x61`–`0x64` take 1, 2, 3, 4 length bytes (`noof_bytes = data[0] & 0xF`). Byte arrays `0x91`–`0x94` take 1, 2, 4, 8 (`noof_bytes = 1 << ((data[0] & 0xF) - 1)`). Not the same rule. Reading one off the other is the single most likely silent bug in the port.
- **Integers `0x30`–`0x33`** take `2 ** (data[0] & 0xF)` bytes → 1, 2, 4, 8. A third distinct ladder.
- **Sized-int round-tripping.** pyatv preserves the encoded width via an int subclass carrying a `size` attribute so re-encoding produces identical bytes. Model this explicitly in C# — a small readonly struct wrapping value plus width — or you will re-encode `0x30 0x05` as `0x0D` and lose byte-exactness against captured frames.
- **The object/pointer table is asymmetric between pack and unpack.** This is a genuine inconsistency in pyatv, not a misreading. `_pack` stores *encoded byte sequences*, dedupes on those, and adds an entry only when `len(packed_bytes) > 1` and the value was not already a hit. `_unpack` stores *decoded values* and dedupes on those. Critically, `_pack` adds lists and dicts to the table while `_unpack` sets `add_to_object_list = False` for them. Port each direction faithfully and separately. Do not unify them into one symmetric table — the encoder must match what Apple accepts, the decoder must match what Apple sends.
- Pointers: index < 0x21 → `0xA0 + index` as a single byte; otherwise `0xC1`–`0xC4` with 1, 2, 4, 8 little-endian index bytes.
- Collections: `0xD0 + n` list, `0xE0 + n` dict for n ≤ 14. At n ≥ 15 the low nibble is `0xF` and the collection is terminated with a `0x03` sentinel.
- Floats: unpack handles `0x35` (float32) and `0x36` (float64); pack always emits `0x36`.
- `0x06` absolute time: decode as a little-endian 8-byte integer, encoding unimplemented. Leave it that way.
- All multi-byte integers and lengths here are **little-endian**. The frame header in WP3 is big-endian. Do not mix them.

### WP2 — TLV8

| | |
|---|---|
| **Source** | `pyatv/auth/hap_tlv8.py` (158 lines) |
| **Exit** | Round-trip tests including a >255-byte value |
| **Hardware** | None |

One trap: a value longer than 255 bytes is emitted as **repeated entries with the same tag**, chunked at 255, and the reader concatenates them. Your reader must accumulate on duplicate tags rather than overwrite. Test with a 600-byte value specifically.

### WP3 — Connection, framing, ChaCha20

| | |
|---|---|
| **Source** | `pyatv/protocols/companion/connection.py` (168), `pyatv/support/chacha20.py` |
| **Exit** | Framing round-trips; encrypt/decrypt against known key and counter |
| **Hardware** | None |

- Header is 4 bytes: 1 byte frame type + **3 bytes big-endian** length.
- Frame types (`connection.py:21-39`): `Unknown=0, NoOp=1, PS_Start=3, PS_Next=4, PV_Start=5, PV_Next=6, U_OPACK=7, E_OPACK=8, P_OPACK=9, PA_Req=10, PA_Rsp=11, SessionStartRequest=16, SessionStartResponse=17, SessionData=18, FamilyIdentityRequest=32, FamilyIdentityResponse=33, FamilyIdentityUpdate=34`. Port the whole enum; only the first nine are used.
- **The length field includes the 16-byte auth tag when encryption is active** — `payload_length += AUTH_TAG_LENGTH` happens before the header is built.
- **Zero-length payloads are never encrypted**, even after encryption is enabled (`if self._chacha and payload_length > 0`). NoOp frames bypass the cipher.
- **AAD is the 4-byte header**, built before encryption, so it already carries the tag-inclusive length.
- Nonce is a **12-byte little-endian counter**, separate counters per direction, incremented after each use. Companion uses the 12-byte variant, not the 8-byte-with-4-zero-pad variant used elsewhere in pyatv.
- Receive path must handle partial frames: buffer until `4 + big-endian length` bytes are available. A three-byte big-endian read is the kind of thing that gets written as `BitConverter.ToInt32` on a padded array — check the endianness handling explicitly on the `net472` leg.

### WP4 — HAP pair-setup and pair-verify

| | |
|---|---|
| **Source** | `pyatv/auth/hap_srp.py` (233), `hap_pairing.py` (147), `protocols/companion/auth.py` (170) |
| **Exit** | Full handshake against the WP5 fake device |
| **Hardware** | None once the fake device exists |

Key derivation, confirmed at `protocol.py:40-42` and cross-checked against the server side at `server_auth.py:131-132`:

```
salt        = ""                    (empty string)
output key  = HKDF(shared, salt, "ClientEncrypt-main")
input key   = HKDF(shared, salt, "ServerEncrypt-main")
```

The server derives these inverted — a useful sanity check when the fake device talks to your client.

Message shapes, from `companion/auth.py`:

- Pair-setup M1: `PS_Start` with `{"_pd": TLV8{Method: 0x00, SeqNo: 0x01}, "_pwTy": 1}`
- M3: `PS_Next` with `{"_pd": TLV8{SeqNo: 0x03, PublicKey, Proof}, "_pwTy": 1}`
- M5: `PS_Next` with `{"_pd": TLV8{SeqNo: 0x05, EncryptedData}, "_pwTy": 1}`
- Pair-verify M1: `PV_Start` with `{"_pd": TLV8{SeqNo: 0x01, PublicKey}, "_auTy": 4}`
- M3: `PV_Next` with `{"_pd": TLV8{SeqNo: 0x03, EncryptedData}}` — note **no `_auTy`** on this one

`_auTy: 4` and `_pwTy: 1` are easy to drop or over-apply; the asymmetry above is what the source does.

BouncyCastle mapping: `Srp6Client` + `Srp6Utilities` (SHA-512, 3072-bit group), `Ed25519Signer`, `X25519Agreement`, `HkdfBytesGenerator`, `ChaCha20Poly1305`. Watch fixed-width zero-padding in the SRP proof computation — a leading-zero-stripped `BigInteger` is the classic failure and produces a proof mismatch with no diagnostic.

### WP5 — Fake Apple TV ⭐ the force multiplier

| | |
|---|---|
| **Source** | `tests/fake_device/companion.py` (593), `protocols/companion/server_auth.py` (229) |
| **Exit** | Client completes pair-setup then pair-verify against it, encrypted channel up |
| **Hardware** | None |

This is what makes the rest tractable. Without it every downstream failure is a silent timeout against real hardware; with it, WP4 and WP6 become ordinary red-green work that Copilot can iterate on unattended. If schedule pressure appears, cut features — do not cut this.

### WP6 — Protocol dispatch and API surface

| | |
|---|---|
| **Source** | `protocols/companion/protocol.py` (234), `api.py` (475) |
| **Exit** | HID commands and session lifecycle green against the fake device |
| **Hardware** | Fake device only |

E_OPACK envelope (`api.py:172-178`):
```
{ "_i": <command name>, "_t": <MessageType>, "_c": { …content… } }
```
`MessageType`: `Event=1, Request=2, Response=3` (`protocol.py:53-58`). Responses correlate by XID where present; **auth frames have no XID and correlate by frame type instead**, because parallel auth attempts are impossible. Implement both dispatch paths.

Session bring-up order — this is what the public documentation gets wrong:

1. `_systemInfo`
2. `_touchStart`
3. `_sessionStart` with `{"_srvT": "com.apple.tvremoteservices", "_sid": <random uint32>}`; the reply carries the device's `_sid` and the effective session id is `(remote_sid << 32) | local_sid`
4. `TVRCSessionStart` with `{"ProtocolVersionKey": "1.2"}` — **wrap in try/catch**; pyatv notes tvOS will not answer `FetchAttentionState` until a TV Remote Client session is registered with `tvremoted`, but older devices simply error on it
5. `_tiStart` with `{}`

Both `TVRCSessionStart` and `_tiStart` are real and both are sent. They are not alternatives.

`_systemInfo` payload (`api.py:191-209`), semi-random values and all:
```
_bf: 0, _cf: 512, _clFl: 128,
_i:     <stable client identifier>      ← see below
_idsID: <credentials client_id>
_pubID: <device id>
_sf: 256, _sv: "170.18",
model: <string>, name: <string>
```

> **`_i` is the field that will cost you a day.** pyatv's own comment: a null `_i` stops the device pushing `TVSystemStatus` power-state events. Worse, from tvOS 18.4 a non-persistent value gets the connection dropped seconds after a successful handshake. The value is MAC-shaped — pyatv falls back to the device id with colons stripped and lowercased. **Generate six random bytes once at pair time, hex-encode, persist alongside the credentials, never regenerate.** Persistence is therefore load-bearing, not optional: put the credential blob somewhere that survives an upgrade of the host application, and expose export/import on the public API from day one so a consumer can back it up. A library that silently regenerates the identifier presents as an intermittent network fault, and the fault will be chased in the wrong layer.

Command surface to implement (app launching is out of scope):
- `_hidC` — button presses, `_hBtS` 1 = down, 2 = up
- `_hidT` — touch events, `_cx`/`_cy` in 0–1000, `_tPh` 1 press / 3 hold / 4 release / 5 tap, `_ns` nanosecond timestamp
- `FetchAttentionState` — 1 asleep, 2 screensaver, 3 awake, 4 idle
- `_interest` / `_regEvents` / `_deregEvents` — event subscription
- `_sessionStop` — takes the **combined 64-bit** `sid`, not the local one

Read the current `_hidC` enum from source; it has grown past the 19 values the documentation lists (Guide and Control Center were added in 0.17.0).

### WP7 — Discovery

`_companion-link._tcp.local`. Keep it behind an interface. Multicast is the least portable part of the whole library: socket option availability and multicast group binding differ between .NET on Windows, .NET on Linux, and Mono, and a discovery implementation that works in a desktop test harness may not work on the eventual host. Isolating it means the protocol library stays testable even where discovery has to be replaced with a static address.

Do not key persisted credentials off the `rp*` TXT records; `rpHA`, `rpHN`, `rpAD`, `rpHI` and `rpBA` rotate as a privacy measure.

### WP8 — Real hardware

Not an agent task. Budget a full day just to get capture working — atvproxy is itself unreliable on tvOS 18.4+ (postlund suspects new validation it can't handle). Fallback that does work: run `pyatv --debug` against the same Apple TV and diff its logs against yours. Its output includes both the decoded OPACK dict and raw plaintext hex per frame, so you can validate your encoder byte-for-byte before encryption is ever involved.

**Test on Apple TV 4K 3rd gen (AppleTV14,1), not just 2nd gen.** There is an open pyatv issue where session setup completes on gen 3 / tvOS 26.5 but `FetchAttentionState` gets no response, while gen 2 on the identical build works. Do not let a consumer make power state load-bearing without a degraded path.

---

## 3. Known-silent failure modes

The protocol has effectively no error surface. When something is wrong the Apple TV completes the handshake, answers `_rT: 0`, and stops responding. Symptoms are identical across unrelated causes, so keep this table to hand:

| Symptom | Likely cause |
|---|---|
| Handshake OK, connection dropped after a few seconds | `_i` not stable across connections |
| No `TVSystemStatus` events ever arrive | `_i` was null in `_systemInfo` |
| `FetchAttentionState` never answers | `TVRCSessionStart` not sent, or gen-3/26.5 issue |
| Decrypt fails on second frame | nonce counter not incrementing, or shared across directions |
| Decrypt fails on first frame | AAD built after the tag-length adjustment, or wrong info string |
| Frames accepted then silence | OPACK encoder producing structurally valid but wrong output |
| Works on `net10.0`, fails on `net472` | endianness or `Span` slicing difference in the framing layer |

`_rT` is a response class, not an error code — setup commands answer `_rT: 0`, data commands answer `_rT: 1` with payload. A pyatv contributor lost time reading `_rT: 0` as failure.

---

## 4. Licensing

pyatv is MIT (Pierre Ståhl). Reuse in a proprietary product is fine provided copies carry the notice. Protocol *facts* — type bytes, field names, enum values — are facts about Apple's wire format and not pyatv's to license; a codec written from the format description carries no obligation, while structural transliteration of `opack.py` does. Attribute regardless: one `THIRD-PARTY-NOTICES.txt` entry.

**Source your SRP6a group parameters and TLV8 layout from pyatv, not from Apple's HAP PDF.** The freely downloadable HAP specification is the Non-Commercial edition and its terms are separate. If this ships commercially, keep a note of which source you used.

---

## 5. Documentation errata

pyatv.dev's protocol page is stale relative to the source and contains transcription errors. Background only; the vendored source is authoritative.

- 3- and 4-byte string length rows show `0x62`/`0x63` prefixes; should be `0x63`/`0x64`
- `0xC2` is listed twice; the second should be `0xC3`
- The endless-dictionary example `0xEF4163416403` decodes to `{"c":"d"}`, not `{"a":"b"}`
- Prose says pairing begins with `PA_Start`/`PA_Next`; the frame table and the source use `PS_*`/`PV_*`
- `_tiStart` and `_rT` are absent from the page entirely
- The `_hidC` enum on the page is out of date

---

## 6. Copilot working notes

**Session scope.** One thread per work package, ending on green tests. The packages are sized so no session needs to hold the whole protocol at once — this is about keeping the citation discipline intact, which is the first thing to degrade in a long thread.

**Getting it to read rather than recall.** Reference the pyatv sources explicitly with `#file:` at the start of each session. A session that has not read `opack.py` will still write an OPACK codec, and it will look right.

**Verification command.** Give it `dotnet test` explicitly so it closes the loop without you brokering each round. Confirm it is running both TFMs and not just the first — a `net10.0`-only green is a half-verified package.

**Opening prompt shape for WP1:**

> Port `#file:reference/pyatv-0.18.0/pyatv/support/opack.py` to `src/AppleTv.Companion/Opack/`. Then port all 41 tests from `#file:reference/pyatv-0.18.0/tests/support/test_opack.py` to MSTest, using `[DataRow]` where the pyatv tests are parameterised. Every type byte constant gets a comment citing the pyatv file and line. Target both `net10.0` and `net472` — no `#if` in the codec. Run `dotnet test` until green on both.

**The review step that cannot be delegated.** Before merging each package, check the protocol constants by hand against the vendored source. Roughly twenty minutes per package. This is the only defence against a constant that was invented, asserted, and passed — and it cannot be done by the thing that wrote the code, regardless of which model is behind it.
