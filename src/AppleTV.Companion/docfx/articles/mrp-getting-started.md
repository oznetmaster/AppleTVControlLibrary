# MRP Getting Started

```csharp
using AppleTvControlLibrary.Discovery.AirPlay;
using AppleTvControlLibrary.Mrp.PlayerState;
using AppleTvControlLibrary.Mrp.Protocol;
using AppleTvControlLibrary.Mrp.RemoteControl;

// 1. Discover AirPlay-capable devices that can tunnel MRP.
var discovery = new MulticastAirPlayDiscovery ();
var results = await discovery.ScanAsync (TimeSpan.FromSeconds (5));

// 2. Pair (once per device). See MrpDeviceManager in
//    src/AppleTv.Remote.Mrp.Wpf/Services/MrpDeviceManager.cs for a complete
//    pair-setup/pair-verify orchestration example over AirPlay's HTTP/RTSP control channel.

// 3. After pairing, connect and observe now-playing state.
MrpPlayerStateManager playerStateManager = /* obtained from your connection setup */;
MrpRemoteControl remoteControl = /* obtained from your connection setup */;

playerStateManager.StateUpdated += (sender, args) =>
{
	 var metadata = playerStateManager.Metadata;
	 Console.WriteLine ($"Now playing: {metadata?.Title} - {metadata?.Artist}");
};

await remoteControl.PlayAsync ();
```

## Async APIs

Like the Companion Link library, MRP's protocol and remote-control operations are asynchronous.
Use the `Async` method variants for connection, pairing, and command dispatch. See
<xref:AppleTvControlLibrary.Mrp.PlayerState.MrpPlayerStateManager> and
<xref:AppleTvControlLibrary.Mrp.RemoteControl.MrpRemoteControl> for the full API surface.

See `src/AppleTv.Remote.Mrp.Wpf` in the source repository for a complete reference host
application (discover, pair, connect, and drive a now-playing UI).
