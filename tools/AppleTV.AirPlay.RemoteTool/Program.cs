// Copyright (c) 2026 Neil Colvin. Licensed under the MIT License.
// See LICENSE file in the repository root for full license text.

using System;
using System.Threading;
using System.Threading.Tasks;

using AppleTvControlLibrary.Mrp.PlayerState;
using AppleTvControlLibrary.Mrp.RemoteControl;

using AppleTvControlLibrary.Discovery.AirPlay;
using AppleTvControlLibrary.Remote.Mrp.Wpf.Services;
using AppleTvControlLibrary.Remote.Mrp.Wpf.Storage;

namespace AppleTvControlLibrary.AirPlay.RemoteTool;

/// <summary>
/// Standalone CLI for exercising a real, already-paired MRP-over-AirPlay device end-to-end
/// (connect, HID buttons, media transport) without needing the WPF app or manual interaction.
/// Reads the same credential JSON format written by AppleTv.Remote.Mrp.Wpf's
/// <see cref="CredentialStore"/>/<see cref="StoredDevice"/>, so it can be pointed at an
/// already-paired device (e.g. "Office") to reproduce/diagnose issues seen in the WPF app.
/// </summary>
/// <remarks>
/// Modeled after AppleTV.Companion.RemoteTool, but reuses <see cref="MrpDeviceManager"/> directly
/// (rather than duplicating the connect/pair-verify logic) since the WPF project already exposes
/// the full AirPlay-tunneled MRP connect path.
/// </remarks>
internal static class Program
	{
	private static async Task<int> Main (string[] args)
		{
		if (args.Length < 2)
			{
			PrintUsage ();
			return 1;
			}

		string credentialsPath = args[0];
		string command = args[1].ToLowerInvariant ();

		if (!System.IO.File.Exists (credentialsPath))
			{
			Console.Error.WriteLine ($"Credentials file not found: {credentialsPath}");
			return 1;
			}

		string json = await System.IO.File.ReadAllTextAsync (credentialsPath).ConfigureAwait (false);
		StoredDevice stored = System.Text.Json.JsonSerializer.Deserialize<StoredDevice> (
			json, new System.Text.Json.JsonSerializerOptions { WriteIndented = true })
			?? throw new InvalidOperationException ($"Failed to deserialize {credentialsPath}");

		// Older credential files may have been saved before mDNS collision suffixes (e.g.
		// "Office (2)") were stripped at pairing time; normalize here too so this tool doesn't
		// bypass the CredentialStore.Load fix by deserializing the file directly.
		stored.Name = AirPlayServiceInfo.RemoveNameCollisionSuffix (stored.Name);

		Console.WriteLine ($"Connecting to {stored.Name} at {stored.Address}:{stored.Port}...");

		using MrpDeviceManager manager = new MrpDeviceManager ();
		manager.ConnectionClosed += (_, ex) => Console.WriteLine ($"[event] ConnectionClosed: {ex?.Message ?? "(clean)"}");

		try
			{
			await manager.ConnectAsync (stored).ConfigureAwait (false);
			Console.WriteLine ("Connect complete.");

			MrpRemoteControl remote = manager.RemoteControl!
				?? throw new InvalidOperationException ("Connected but no RemoteControl instance available");

			return await RunCommandAsync (manager, remote, command, args).ConfigureAwait (false);
			}
		catch (Exception ex)
			{
			Console.Error.WriteLine ($"Failed: {ex}");
			return 1;
			}
		}

	private static async Task<int> RunCommandAsync (MrpDeviceManager manager, MrpRemoteControl remote, string command, string[] args)
		{
		switch (command)
			{
			case "status":
				{
				Console.WriteLine ("Connected. (MRP has no equivalent of Companion's FetchAttentionState; use 'monitor' to observe pushed player state.)");
				return 0;
				}
			case "up":
				await remote.Up ().ConfigureAwait (false);
				Console.WriteLine ("Sent Up.");
				return 0;
			case "down":
				await remote.Down ().ConfigureAwait (false);
				Console.WriteLine ("Sent Down.");
				return 0;
			case "left":
				await remote.Left ().ConfigureAwait (false);
				Console.WriteLine ("Sent Left.");
				return 0;
			case "right":
				await remote.Right ().ConfigureAwait (false);
				Console.WriteLine ("Sent Right.");
				return 0;
			case "select":
				await remote.Select ().ConfigureAwait (false);
				Console.WriteLine ("Sent Select.");
				return 0;
			case "menu":
				await remote.Menu ().ConfigureAwait (false);
				Console.WriteLine ("Sent Menu.");
				return 0;
			case "home":
				await remote.Home ().ConfigureAwait (false);
				Console.WriteLine ("Sent Home.");
				return 0;
			case "topmenu":
				await remote.TopMenu ().ConfigureAwait (false);
				Console.WriteLine ("Sent TopMenu.");
				return 0;
			case "suspend":
				await remote.Suspend ().ConfigureAwait (false);
				Console.WriteLine ("Sent Suspend.");
				return 0;
			case "wakeup":
				await remote.Wakeup ().ConfigureAwait (false);
				Console.WriteLine ("Sent Wakeup.");
				return 0;
			case "turnon":
				await remote.TurnOn ().ConfigureAwait (false);
				Console.WriteLine ("Sent TurnOn (WAKE_DEVICE_MESSAGE).");
				return 0;
			case "turnoff":
				await remote.TurnOff ().ConfigureAwait (false);
				Console.WriteLine ("Sent TurnOff (home hold + select).");
				return 0;
			case "volumeup":
				await remote.VolumeUp ().ConfigureAwait (false);
				Console.WriteLine ("Sent VolumeUp.");
				return 0;
			case "volumedown":
				await remote.VolumeDown ().ConfigureAwait (false);
				Console.WriteLine ("Sent VolumeDown.");
				return 0;
			case "play":
				await remote.Play ().ConfigureAwait (false);
				Console.WriteLine ("Sent Play.");
				return 0;
			case "pause":
				await remote.Pause ().ConfigureAwait (false);
				Console.WriteLine ("Sent Pause.");
				return 0;
			case "playpause":
				await remote.PlayPause ().ConfigureAwait (false);
				Console.WriteLine ("Sent PlayPause.");
				return 0;
			case "stop":
				await remote.Stop ().ConfigureAwait (false);
				Console.WriteLine ("Sent Stop.");
				return 0;
			case "next":
				await remote.Next ().ConfigureAwait (false);
				Console.WriteLine ("Sent Next.");
				return 0;
			case "previous":
				await remote.Previous ().ConfigureAwait (false);
				Console.WriteLine ("Sent Previous.");
				return 0;
			case "monitor":
				{
				int seconds = args.Length > 2 && int.TryParse (args[2], out int s) ? s : 30;
				MrpPlayerStateManager? playerStateManager = GetPlayerStateManager (manager);
				if (playerStateManager is not null)
					{
					playerStateManager.Listener = new ConsolePlayerStateListener (playerStateManager);
					}

				// Diagnostic-only: logs every inbound frame by raw type number and hex payload,
				// including types with no vendored .proto (e.g. SET_READY_STATE_MESSAGE = 36,
				// UPDATE_ACTIVE_SYSTEM_ENDPOINT_MESSAGE = 77, or the unnamed 13/14/45), so nothing
				// is silently dropped while watching a screensaver on/off transition.
				if (manager.Protocol is not null)
					{
					manager.Protocol.RawMessageReceived += (type, data) =>
						Console.WriteLine ($"[raw] Type={type} Bytes={Convert.ToHexString (data)}");
					}

				Console.WriteLine ($"Monitoring pushed player state for {seconds}s (Ctrl+C to stop early)...");
				await Task.Delay (TimeSpan.FromSeconds (seconds)).ConfigureAwait (false);
				return 0;
				}
			case "sequence":
				return await RunWakeThenButtonsSequenceAsync (remote).ConfigureAwait (false);
			default:
				PrintUsage ();
				return 1;
			}
		}

	// Reproduces "power on works but no other button works" end-to-end: sends Wakeup, waits for
	// the device to settle, then exercises every HID button while printing any exception at each
	// step.
	private static async Task<int> RunWakeThenButtonsSequenceAsync (MrpRemoteControl remote)
		{
		Console.WriteLine ("Sending Wakeup...");
		await remote.Wakeup ().ConfigureAwait (false);

		for (int i = 0; i < 5; i++)
			{
			await Task.Delay (1000).ConfigureAwait (false);
			Console.WriteLine ($"  +{i + 1}s...");
			}

		(string Name, Func<Task> Action)[] steps =
			[
			("Up", () => remote.Up ()),
			("Down", () => remote.Down ()),
			("Left", () => remote.Left ()),
			("Right", () => remote.Right ()),
			("Select", () => remote.Select ()),
			("Menu", () => remote.Menu ()),
			("Home", () => remote.Home ()),
			("PlayPause", () => remote.PlayPause ()),
			];

		foreach ((string name, Func<Task> action) in steps)
			{
			try
				{
				Console.WriteLine ($"Sending {name}...");
				await action ().ConfigureAwait (false);
				Console.WriteLine ($"  {name} acknowledged.");
				}
			catch (Exception ex)
				{
				Console.WriteLine ($"  {name} FAILED: {ex.Message}");
				}

			await Task.Delay (500).ConfigureAwait (false);
			}

		return 0;
		}

	// MrpDeviceManager does not expose its internal MrpPlayerStateManager publicly; reflection is
	// used here purely for this diagnostic tool rather than widening the manager's public surface
	// for a single debug command.
	private static MrpPlayerStateManager? GetPlayerStateManager (MrpDeviceManager manager)
		{
		System.Reflection.FieldInfo? field = typeof (MrpDeviceManager).GetField (
			"_playerStateManager", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
		return field?.GetValue (manager) as MrpPlayerStateManager;
		}

	private static void PrintUsage ()
		{
		Console.WriteLine ("Usage: AppleTV.AirPlay.RemoteTool <credentials.json> <command> [args]");
		Console.WriteLine ();
		Console.WriteLine ("Commands:");
		Console.WriteLine ("  status                    Confirm the connection is up.");
		Console.WriteLine ("  up|down|left|right|select|menu|home|topmenu   Send a single HID key press.");
		Console.WriteLine ("  suspend|wakeup            Send suspend/wakeup.");
		Console.WriteLine ("  turnon                    Send WAKE_DEVICE_MESSAGE directly (pyatv turn_on()).");
		Console.WriteLine ("  volumeup|volumedown       Send a volume step.");
		Console.WriteLine ("  play|pause|playpause|stop Send a transport command.");
		Console.WriteLine ("  next|previous             Send a track-skip command.");
		Console.WriteLine ("  monitor [seconds]         Print pushed player-state updates for N seconds (default 30).");
		Console.WriteLine ("  sequence                  Reproduce wake-then-every-button end-to-end.");
		}

	private sealed class ConsolePlayerStateListener (MrpPlayerStateManager manager) : IMrpPlayerStateListener
		{
		public void StateUpdated ()
			{
			MrpPlayerState playing = manager.Playing;
			Console.WriteLine ($"[event] PlayerState -> Identifier={playing.Identifier} DisplayName={playing.DisplayName} PlaybackState={playing.PlaybackStateValue}");
			}
		}
	}
