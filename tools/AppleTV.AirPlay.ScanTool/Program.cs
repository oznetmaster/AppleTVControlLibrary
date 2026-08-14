// Copyright (c) 2026 Neil Colvin. Licensed under the MIT License.
// See LICENSE file in the repository root for full license text.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using AppleTvControlLibrary.Auth;
using AppleTvControlLibrary.Discovery.AirPlay;
using AppleTvControlLibrary.Mrp.AirPlay;
using AppleTvControlLibrary.Mrp.AirPlay.Auth;
using AppleTvControlLibrary.Mrp.AirPlay.Http;

namespace AppleTvControlLibrary.AirPlay.ScanTool;

/// <summary>
/// Standalone CLI that runs a real AirPlay mDNS scan against the local network and prints
/// every discovered device, so results can be verified against real Apple TV hardware without
/// spinning up Visual Studio's test runner.
/// </summary>
/// <remarks>
/// Usage:
///   AppleTvControlLibrary.AirPlay.ScanTool [timeoutSeconds]
///     Scan only (default).
///   AppleTvControlLibrary.AirPlay.ScanTool pair &lt;address&gt; &lt;port&gt;
///     Run AirPlay pair-setup against the given address/port. Prompts for the on-screen PIN,
///     then prints the resulting credentials string (see <see cref="HapCredentials.ToString"/>)
///     so it can be reused with the "connect" mode below.
///   AppleTvControlLibrary.AirPlay.ScanTool connect &lt;address&gt; &lt;port&gt; &lt;credentialsString&gt;
///     Also connect to the given address/port, run AirPlay pair-verify with previously-saved
///     credentials (see <see cref="HapCredentials.Parse"/>/<see cref="HapCredentials.ToString"/>),
///     and bring up the remote-control (MRP tunnel) channel via <see cref="Ap2Session"/>, so the
///     AP2/HAP/channel plumbing can be exercised against real hardware, not just compiled.
/// </remarks>
internal static class Program
	{
	private static async Task<int> Main (string[] args)
		{
		if (args.Length > 0 && string.Equals (args[0], "pair", StringComparison.OrdinalIgnoreCase))
			{
			return await RunPairAsync (args).ConfigureAwait (false);
			}

		if (args.Length > 0 && string.Equals (args[0], "connect", StringComparison.OrdinalIgnoreCase))
			{
			return await RunConnectAsync (args).ConfigureAwait (false);
			}

		return await RunScanAsync (args).ConfigureAwait (false);
		}

	private static async Task<int> RunScanAsync (string[] args)
		{
		TimeSpan timeout = TimeSpan.FromSeconds (5);
		if (args.Length > 0 && double.TryParse (args[0], out double seconds))
			{
			timeout = TimeSpan.FromSeconds (seconds);
			}

		Console.WriteLine ($"Scanning for AirPlay devices (\"{AirPlayServiceInfo.SERVICE_TYPE}\") for {timeout.TotalSeconds}s...");

		using CancellationTokenSource cts = new CancellationTokenSource ();
		Console.CancelKeyPress += (_, e) =>
			{
			e.Cancel = true;
			cts.Cancel ();
			};

		MulticastAirPlayDiscovery discovery = new MulticastAirPlayDiscovery ();
		IReadOnlyList<AirPlayDiscoveryResult> results;
		try
			{
			results = await discovery.ScanAsync (timeout, cts.Token).ConfigureAwait (false);
			}
		catch (Exception ex)
			{
			Console.Error.WriteLine ($"Scan failed: {ex}");
			return 1;
			}

		if (results.Count == 0)
			{
			Console.WriteLine ("No AirPlay devices found.");
			return 0;
			}

		Console.WriteLine ($"Found {results.Count} device(s):");
		foreach (AirPlayDiscoveryResult result in results)
			{
			Console.WriteLine ("----------------------------------------");
			Console.WriteLine ($"Name:               {result.Name}");
			Console.WriteLine ($"Address:            {result.Address}");
			Console.WriteLine ($"Port:               {result.Port}");
			Console.WriteLine ($"UniqueId:           {result.UniqueId ?? "(none)"}");
			Console.WriteLine ($"PairingRequirement: {result.PairingRequirement}");
			Console.WriteLine ("Properties:");
			foreach (KeyValuePair<string, string> property in result.Properties)
				{
				Console.WriteLine ($"  {property.Key} = {property.Value}");
				}
			}

		return 0;
		}

	private static async Task<int> RunPairAsync (string[] args)
		{
		if (args.Length < 3)
			{
			Console.Error.WriteLine ("Usage: pair <address> <port>");
			return 1;
			}

		string address = args[1];
		int port = int.Parse (args[2]);

		Console.WriteLine ($"Connecting to {address}:{port} for AirPlay pair-setup...");

		using CancellationTokenSource cts = new CancellationTokenSource ();
		Console.CancelKeyPress += (_, e) =>
			{
			e.Cancel = true;
			cts.Cancel ();
			};

		HttpConnection connection;
		try
			{
			connection = await HttpConnection.ConnectAsync (address, port, cts.Token).ConfigureAwait (false);
			}
		catch (Exception ex)
			{
			Console.Error.WriteLine ($"Connect failed: {ex}");
			return 1;
			}

		try
			{
			SrpAuthHandler srp = new SrpAuthHandler ();
			AirPlayHapPairSetupProcedure pairing = new AirPlayHapPairSetupProcedure (connection, srp);

			await pairing.StartPairingAsync ().ConfigureAwait (false);
			Console.WriteLine ("Pairing started; check the device's screen for a PIN.");
			Console.Write ("Enter PIN: ");
			string? pinText = Console.ReadLine ();
			if (!int.TryParse (pinText, out int pin))
				{
				Console.Error.WriteLine ("Invalid PIN.");
				return 1;
				}

			HapCredentials credentials = await pairing.FinishPairingAsync (pin, "AppleTV.AirPlay.ScanTool").ConfigureAwait (false);
			Console.WriteLine ("Pairing succeeded. Credentials:");
			Console.WriteLine (credentials.ToString ());
			return 0;
			}
		catch (Exception ex)
			{
			Console.Error.WriteLine ($"Pairing failed: {ex}");
			return 1;
			}
		finally
			{
			connection.Dispose ();
			}
		}

	private static async Task<int> RunConnectAsync (string[] args)
		{
		if (args.Length < 4)
			{
			Console.Error.WriteLine ("Usage: connect <address> <port> <credentialsString>");
			return 1;
			}

		string address = args[1];
		int port = int.Parse (args[2]);
		HapCredentials credentials = HapCredentials.Parse (args[3]);

		Console.WriteLine ($"Connecting to {address}:{port}...");

		using CancellationTokenSource cts = new CancellationTokenSource ();
		Console.CancelKeyPress += (_, e) =>
			{
			e.Cancel = true;
			cts.Cancel ();
			};

		Ap2Session session = new Ap2Session (address, port, credentials);
		try
			{
			await session.ConnectAsync (cts.Token).ConfigureAwait (false);
			Console.WriteLine ("Pair-verify succeeded, control connection established.");

			await session.SetupRemoteControlAsync (cts.Token).ConfigureAwait (false);
			Console.WriteLine ("Remote-control (data) channel established. Ready to tunnel MRP.");

			using AirPlayMrpConnection mrpConnection = new AirPlayMrpConnection (session);
			mrpConnection.Listener = new LoggingListener ();
			mrpConnection.ConnectionLost += ex => Console.WriteLine ($"Connection lost: {ex}");
			mrpConnection.Connect ();

			Console.WriteLine ("AirPlayMrpConnection attached to data channel. Press Ctrl+C to exit.");
			await Task.Delay (Timeout.Infinite, cts.Token).ConfigureAwait (false);
			}
		catch (OperationCanceledException)
			{
			Console.WriteLine ("Cancelled.");
			}
		catch (Exception ex)
			{
			Console.Error.WriteLine ($"Connect failed: {ex}");
			return 1;
			}
		finally
			{
			session.Dispose ();
			}

		return 0;
		}

	private sealed class LoggingListener : Mrp.Connection.IMrpConnectionListener
		{
		public void MessageReceived (byte[] data) => Console.WriteLine ($"Received {data.Length} byte protobuf message.");
		}
	}
