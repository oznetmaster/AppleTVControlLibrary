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

## Connect and send a command

Pairing and connection orchestration is demonstrated by the WPF reference host in
`src/AppleTv.Remote.Wpf`. After pairing, create a `CompanionApi`, complete pair verification,
call `ConnectAsync()`, and send commands through the API.

```csharp
await api.ConnectAsync ();
await api.SendHidCommandAsync (down: true, command: HidCommand.Select);
await api.SendHidCommandAsync (down: false, command: HidCommand.Select);
```

The API is async-first. Synchronous methods are retained only as obsolete source-compatibility
wrappers; new code should use the corresponding `Async` methods.

See [Pairing and Credentials](pairing.md) for persistence requirements.
