// Copyright (c) 2026 Neil Colvin. Licensed under the MIT License.
// See LICENSE file in the repository root for full license text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;

using AppleTvControlLibrary.Auth;
using AppleTvControlLibrary.Connection;
using AppleTvControlLibrary.Protocol;
using AppleTvControlLibrary.Tlv8;

namespace AppleTvControlLibrary.RemoteTool;

/// <summary>
/// Standalone CLI for exercising a real, already-paired Companion Link device end-to-end
/// (connect, HID buttons, media control, power) without needing the WPF app or manual
/// interaction. Reads the same credential JSON format written by
/// AppleTv.Remote.Wpf's CredentialStore, so it can be pointed at an already-paired device
/// (e.g. "Office") to reproduce/diagnose issues seen in the WPF app.
/// </summary>
internal static class Program
	{
	private static async System.Threading.Tasks.Task<int> Main (string[] args)
		{
		if (args.Length < 2)
			{
			PrintUsage ();
			return 1;
			}

		string credentialsPath = args[0];
		string command = args[1].ToLowerInvariant ();

		if (!File.Exists (credentialsPath))
			{
			Console.Error.WriteLine ($"Credentials file not found: {credentialsPath}");
			return 1;
			}

		StoredDevice stored = LoadStoredDevice (credentialsPath);

		Console.WriteLine ($"Connecting to {stored.Name} at {stored.Address}:{stored.Port}...");
		using RawTcpTransport transport = new RawTcpTransport ();
		CompanionConnection connection = new CompanionConnection ();
		CompanionProtocol protocol = new CompanionProtocol (connection, new SrpAuthHandler ());
		transport.Attach (stored.Address, stored.Port, connection, protocol);

		try
			{
			PairVerify (stored, protocol, connection);
			Console.WriteLine ("Pair-verify complete, encryption enabled.");

			CompanionApi api = new CompanionApi (
				protocol,
				stored.ToCredentials (),
				stableIdentifier: stored.StableIdentifier,
				deviceId: Convert.ToHexString (stored.ToCredentials ().AtvId).ToLowerInvariant (),
				model: "AppleTV",
				name: stored.Name);

			api.SystemStatusChanged += (_, _) => Console.WriteLine ($"[event] SystemStatus -> {api.CurrentSystemStatus}");
			api.MediaControlCapabilitiesChanged += (_, _) => Console.WriteLine ($"[event] MediaControlCapabilities -> {(api.IsVolumeControlSupported ? "Volume supported" : "Volume not supported")}");

			Console.WriteLine ("Running Connect() bring-up...");
			api.Connect ();
			Console.WriteLine ($"Connect complete. CurrentSystemStatus={api.CurrentSystemStatus}");

			return RunCommand (api, command, args);
			}
		catch (Exception ex)
			{
			Console.Error.WriteLine ($"Failed: {ex}");
			return 1;
			}
		}

	private static int RunCommand (CompanionApi api, string command, string[] args)
		{
		switch (command)
			{
			case "status":
				{
				Console.WriteLine ($"FetchAttentionState() -> {api.FetchAttentionState ()}");
				return 0;
				}
			case "wake":
				{
				api.SendHidCommand (down: false, HidCommand.Wake);
				Console.WriteLine ("Sent Wake.");
				return 0;
				}
			case "sleep":
				{
				api.SendHidCommand (down: false, HidCommand.Sleep);
				Console.WriteLine ("Sent Sleep.");
				return 0;
				}
			case "button":
				{
				if (args.Length < 3 || !Enum.TryParse (args[2], ignoreCase: true, out HidCommand hid))
					{
					Console.Error.WriteLine ($"Usage: button <{string.Join ("|", Enum.GetNames (typeof (HidCommand)))}>");
					return 1;
					}

				Console.WriteLine ($"Sending {hid} down...");
				api.SendHidCommand (down: true, hid);
				Console.WriteLine ($"Sending {hid} up...");
				api.SendHidCommand (down: false, hid);
				Console.WriteLine ("Done.");
				return 0;
				}
			case "click":
				{
				Console.WriteLine ("Sending touch click (Select)...");
				api.SendClick (InputAction.SingleTap);
				Console.WriteLine ("Done.");
				return 0;
				}
			case "volume":
				{
				if (!api.IsVolumeControlSupported)
					{
					Console.WriteLine ("Device does not currently advertise volume control support (_mcF missing Volume flag).");
					return 0;
					}

				Console.WriteLine ($"GetVolume() -> {api.GetVolume ()}%");
				return 0;
				}
			case "monitor":
				{
				int seconds = args.Length > 2 && int.TryParse (args[2], out int s) ? s : 30;
				Console.WriteLine ($"Monitoring pushed events for {seconds}s (Ctrl+C to stop early)...");
				Thread.Sleep (TimeSpan.FromSeconds (seconds));
				Console.WriteLine ($"Final CurrentSystemStatus={api.CurrentSystemStatus}");
				return 0;
				}
			case "sequence":
				{
				// Reproduces the exact bug report: wake, wait, then try every button in turn,
				// printing status before/after each so a stuck-after-wake regression is visible
				// without needing the WPF app or a human watching the TV.
				return RunWakeThenButtonsSequence (api);
				}
			default:
				PrintUsage ();
				return 1;
			}
		}

	// Reproduces "power on works but no other button works" end-to-end: sends Wake, waits for
	// the device to settle, then exercises every HID button and the touch click path while
	// printing CurrentSystemStatus and any exception at each step.
	private static int RunWakeThenButtonsSequence (CompanionApi api)
		{
		Console.WriteLine ($"Initial CurrentSystemStatus={api.CurrentSystemStatus}");

		Console.WriteLine ("Sending Wake...");
		api.SendHidCommand (down: false, HidCommand.Wake);

		for (int i = 0; i < 10; i++)
			{
			Thread.Sleep (1000);
			Console.WriteLine ($"  +{i + 1}s: CurrentSystemStatus={api.CurrentSystemStatus}");
			}

		foreach (HidCommand hid in new[] { HidCommand.Up, HidCommand.Down, HidCommand.Left, HidCommand.Right, HidCommand.Select, HidCommand.Menu, HidCommand.Home, HidCommand.PlayPause })
			{
			try
				{
				Console.WriteLine ($"Sending {hid}...");
				api.SendHidCommand (down: true, hid);
				api.SendHidCommand (down: false, hid);
				Console.WriteLine ($"  {hid} acknowledged. CurrentSystemStatus={api.CurrentSystemStatus}");
				}
			catch (Exception ex)
				{
				Console.WriteLine ($"  {hid} FAILED: {ex.Message}");
				}

			Thread.Sleep (500);
			}

		try
			{
			Console.WriteLine ("Sending touch click...");
			api.SendClick (InputAction.SingleTap);
			Console.WriteLine ("  Touch click acknowledged.");
			}
		catch (Exception ex)
			{
			Console.WriteLine ($"  Touch click FAILED: {ex.Message}");
			}

		return 0;
		}

	// pyatv/protocols/companion/auth.py (CompanionPairVerifyProcedure) — line 120-158 as of pyatv 0.18.0
	private static void PairVerify (StoredDevice stored, CompanionProtocol protocol, CompanionConnection connection)
		{
		HapCredentials credentials = stored.ToCredentials ();

		SrpAuthHandler srp = new SrpAuthHandler ();
		(byte[] _, byte[] verifyPubKey) = srp.Initialize ();

		byte[] pv1 = Tlv8.Tlv8.WriteTlv (new Dictionary<int, byte[]>
			{
			{ (int)TlvValue.SeqNo, new byte[] { 1 } },
			{ (int)TlvValue.PublicKey, verifyPubKey },
			});
		Dictionary<object, object?> pv2Response = protocol.ExchangeAuth (FrameType.PV_Start, new Dictionary<string, object?> { ["_pd"] = pv1, ["_auTy"] = 4 });
		byte[] pv2 = (byte[])pv2Response["_pd"]!;
		Dictionary<int, byte[]> pv2Tlv = Tlv8.Tlv8.ReadTlv (pv2);
		byte[] serverVerifyPubKey = pv2Tlv[(int)TlvValue.PublicKey];
		byte[] serverEncryptedData = pv2Tlv[(int)TlvValue.EncryptedData];

		byte[] pv3EncryptedData = srp.Verify1 (credentials, serverVerifyPubKey, serverEncryptedData);
		byte[] pv3 = Tlv8.Tlv8.WriteTlv (new Dictionary<int, byte[]>
			{
			{ (int)TlvValue.SeqNo, new byte[] { 3 } },
			{ (int)TlvValue.EncryptedData, pv3EncryptedData },
			});
		Dictionary<object, object?> pv4Response = protocol.ExchangeAuth (FrameType.PV_Next, new Dictionary<string, object?> { ["_pd"] = pv3 });
		byte[] pv4 = (byte[])pv4Response["_pd"]!;
		Dictionary<int, byte[]> pv4Tlv = Tlv8.Tlv8.ReadTlv (pv4);
		if (pv4Tlv.ContainsKey ((int)TlvValue.Error))
			{
			throw new AuthenticationException ("Pair-verify failed");
			}

		(byte[] outputKey, byte[] inputKey) = srp.Verify2 (
			CompanionProtocol.SRP_SALT, CompanionProtocol.SRP_OUTPUT_INFO, CompanionProtocol.SRP_INPUT_INFO);
		connection.EnableEncryption (outputKey, inputKey);
		}

	private static StoredDevice LoadStoredDevice (string path)
		{
		string json = File.ReadAllText (path);
		return JsonSerializer.Deserialize<StoredDevice> (json, new JsonSerializerOptions { WriteIndented = true })
			?? throw new InvalidOperationException ($"Failed to deserialize {path}");
		}

	private static void PrintUsage ()
		{
		Console.WriteLine ("Usage: AppleTvControlLibrary.RemoteTool <credentials.json> <command> [args]");
		Console.WriteLine ();
		Console.WriteLine ("Commands:");
		Console.WriteLine ("  status              Fetch and print the current attention state.");
		Console.WriteLine ("  wake                Send HidCommand.Wake.");
		Console.WriteLine ("  sleep               Send HidCommand.Sleep.");
		Console.WriteLine ("  button <name>       Send a down/up pair for the named HidCommand (e.g. Up, Select, PlayPause).");
		Console.WriteLine ("  click               Send a touch click (Select HID command + touch Click).");
		Console.WriteLine ("  volume              Print the current volume, if supported.");
		Console.WriteLine ("  monitor [seconds]   Connect and print pushed SystemStatus/media-control events for a while.");
		Console.WriteLine ("  sequence            Repro of the wake-then-buttons bug: wake, wait 10s, then try every button.");
		}
	}

/// <summary>
/// Minimal on-disk representation compatible with AppleTv.Remote.Wpf.Storage.StoredDevice,
/// duplicated here (rather than referencing the WPF project) so this tool stays a plain
/// console app with no WPF/Windows-only dependency.
/// </summary>
internal sealed class StoredDevice
	{
	public string UniqueId
		{
		get;
		set;
		} = string.Empty;

	public string Name
		{
		get;
		set;
		} = string.Empty;

	public string Address
		{
		get;
		set;
		} = string.Empty;

	public int Port
		{
		get;
		set;
		}

	public string StableIdentifier
		{
		get;
		set;
		} = string.Empty;

	public byte[] Ltpk
		{
		get;
		set;
		} = Array.Empty<byte> ();

	public byte[] Ltsk
		{
		get;
		set;
		} = Array.Empty<byte> ();

	public byte[] AtvId
		{
		get;
		set;
		} = Array.Empty<byte> ();

	public byte[] ClientId
		{
		get;
		set;
		} = Array.Empty<byte> ();

	public HapCredentials ToCredentials () => new (this.Ltpk, this.Ltsk, this.AtvId, this.ClientId);
	}

/// <summary>
/// A minimal, tool-local TCP transport wiring <see cref="CompanionConnection"/>/
/// <see cref="CompanionProtocol"/> to a real socket, mirroring
/// AppleTv.Remote.Wpf.Transport.TcpCompanionTransport so this console tool has no
/// dependency on the WPF project.
/// </summary>
internal sealed class RawTcpTransport : IDisposable
	{
	private TcpClient? _client;
	private CompanionConnection? _connection;
	private Thread? _readThread;
	private volatile bool _disposed;

	public void Attach (string host, int port, CompanionConnection connection, CompanionProtocol protocol)
		{
		this._connection = connection;
		this._client = new TcpClient ();
		this._client.Connect (host, port);

		protocol.Sender = this.Send;

		this._readThread = new Thread (this.ReadLoop)
			{
			IsBackground = true,
			Name = "CompanionLink-Read",
			};
		this._readThread.Start ();
		}

	private void Send (byte[] frame)
		{
		if (this._disposed || this._client is null)
			{
			throw new ObjectDisposedException (nameof (RawTcpTransport));
			}

		NetworkStream stream = this._client.GetStream ();
		lock (stream)
			{
			stream.Write (frame, 0, frame.Length);
			}
		}

	private void ReadLoop ()
		{
		if (this._client is null || this._connection is null)
			{
			return;
			}

		NetworkStream stream = this._client.GetStream ();
		byte[] buffer = new byte[4096];

		try
			{
			while (!this._disposed)
				{
				int read = stream.Read (buffer, 0, buffer.Length);
				if (read == 0)
					{
					return;
					}

				byte[] received = new byte[read];
				Array.Copy (buffer, received, read);

				try
					{
					this._connection.ReceiveData (received);
					}
				catch (Exception ex)
					{
					Console.WriteLine ($"[RawTcpTransport] ReceiveData failed: {ex}");
					}
				}
			}
		catch (Exception) when (this._disposed)
			{
			}
		catch (Exception ex)
			{
			Console.WriteLine ($"[RawTcpTransport] Read loop failed: {ex}");
			}
		}

	public void Dispose ()
		{
		if (this._disposed)
			{
			return;
			}

		this._disposed = true;
		try
			{
			this._client?.Close ();
			}
		catch (Exception)
			{
			}
		}
	}
