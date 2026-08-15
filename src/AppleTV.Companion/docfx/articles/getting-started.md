# Getting Started

Install `AppleTvControlLibrary.All` when an application needs both Companion Link and mDNS
discovery. Install the individual packages when the host supplies a device address or discovery
implementation itself.

```powershell
# Complete Companion Link and discovery stack
 dotnet add package AppleTvControlLibrary.All

# Or install either independent library
 dotnet add package AppleTvControlLibrary
 dotnet add package AppleTvControlLibrary.Discovery
```

## Discover a device

```csharp
using AppleTvControlLibrary.Discovery.Companion;

var discovery = new MulticastCompanionDiscovery ();
var devices = await discovery.ScanAsync (TimeSpan.FromSeconds (5));
```

When a host already knows an Apple TV IPv4 address but not its current Companion TCP port, query
that device directly over the fixed mDNS UDP port. The discovery result contains the advertised
Companion endpoint.

```csharp
var discovery = new UnicastCompanionDiscovery (IPAddress.Parse ("192.0.2.10"));
var devices = await discovery.ScanAsync (TimeSpan.FromSeconds (5));
```

To look up a known mDNS service instance name, use
<xref:AppleTvControlLibrary.Discovery.Companion.MulticastCompanionDiscovery.DiscoveryAsync*>. It
stops the multicast scan once the exact name has been resolved.

```csharp
CompanionDiscoveryResult? device = await MulticastCompanionDiscovery.DiscoveryAsync (
	 "Living Room", TimeSpan.FromSeconds (5));
```

## Connect and send a command

Pairing and connection orchestration is demonstrated by the WPF reference host in
`src/AppleTv.Remote.Wpf`. After pairing, create a <xref:AppleTvControlLibrary.Protocol.CompanionApi>,
complete pair verification, call <xref:AppleTvControlLibrary.Protocol.CompanionApi.ConnectAsync*>,
and send commands through the API.

```csharp
await api.ConnectAsync ();
await api.SendHidCommandAsync (down: true, command: HidCommand.Select);
await api.SendHidCommandAsync (down: false, command: HidCommand.Select);
```

The API is async-first. Synchronous methods are retained only as obsolete source-compatibility
wrappers; new code should use the corresponding `Async` methods.

See [Pairing and Credentials](pairing.md) for persistence requirements.
