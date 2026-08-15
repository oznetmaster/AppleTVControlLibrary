# Discovery

`AppleTvControlLibrary.Discovery` is a separate package that implements mDNS/DNS-SD discovery for
the Companion Link, MRP, and AirPlay services used by this library. It has no dependency on the
protocol libraries and can be used independently, for example to build a device picker without
also depending on `AppleTvControlLibrary` or `AppleTvControlLibrary.Mrp`.

API documentation is published at
[oznetmaster.github.io/AppleTVControlLibrary](https://oznetmaster.github.io/AppleTVControlLibrary/),
covering the Companion Link, MRP, and Discovery libraries.

## Install

```powershell
dotnet add package AppleTvControlLibrary.Discovery
```

## Service types

| Service | mDNS service type | Discovery type |
|---|---|---|
| Companion Link | `_companion-link._tcp.local` | <xref:AppleTvControlLibrary.Discovery.Companion.ICompanionDiscovery> |
| AirPlay (used to tunnel MRP) | `_airplay._tcp.local` | <xref:AppleTvControlLibrary.Discovery.AirPlay.IAirPlayDiscovery> |
| MRP | `_mediaremotetv._tcp.local` | <xref:AppleTvControlLibrary.Discovery.Mrp.IMrpDiscovery> |

Each service is isolated behind its own interface so a host can swap in a different discovery
implementation per service without affecting the others.

## Multicast discovery

Multicast discovery sends a DNS-SD PTR query to the mDNS multicast group (`224.0.0.251:5353`) and
collects responses until the requested timeout elapses, or until a specific device name has been
fully resolved.

```csharp
using AppleTvControlLibrary.Discovery.Companion;

var discovery = new MulticastCompanionDiscovery ();
var devices = await discovery.ScanAsync (TimeSpan.FromSeconds (5));
```

The same pattern applies to <xref:AppleTvControlLibrary.Discovery.AirPlay.MulticastAirPlayDiscovery>
and <xref:AppleTvControlLibrary.Discovery.Mrp.MulticastMrpDiscovery>.

To resolve a single, known device by its mDNS service instance name and stop scanning as soon as
it is found, use each discovery type's static `DiscoveryAsync` method, for example
<xref:AppleTvControlLibrary.Discovery.Companion.MulticastCompanionDiscovery.DiscoveryAsync*>.

## Unicast discovery

When a host already knows an Apple TV's IPv4 address but not its current Companion TCP port, query
that device directly instead of joining the multicast group:

```csharp
using AppleTvControlLibrary.Discovery.Companion;

var discovery = new UnicastCompanionDiscovery (IPAddress.Parse ("192.0.2.10"));
var devices = await discovery.ScanAsync (TimeSpan.FromSeconds (5));
```

## Static discovery

When a host supplies its own discovery mechanism, or multicast is unavailable or unreliable (for
example on some Mono or embedded hosts), <xref:AppleTvControlLibrary.Discovery.Companion.StaticCompanionDiscovery>
returns a single, pre-configured device without performing any network scan.

## TXT record stability

Some Companion Link TXT record fields (`rpHA`, `rpHN`, `rpAD`, `rpHI`, `rpBA`) rotate periodically
as a privacy measure. Do not use them as a stable device identifier; prefer the discovery result's
unique identifier, or a value persisted from the pairing flow instead.

See the [Discovery API reference](../discovery-api/index.md).
