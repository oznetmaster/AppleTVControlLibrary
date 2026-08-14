// Copyright (c) 2026 Neil Colvin. Licensed under the MIT License.
// See LICENSE file in the repository root for full license text.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using AppleTvControlLibrary.Discovery.Mrp;

namespace AppleTvControlLibrary.Mrp.ScanTool;

/// <summary>
/// Standalone CLI that runs a real MRP (Media Remote Protocol) mDNS scan against the local
/// network and prints every discovered device, so results can be verified against real Apple TV
/// hardware without spinning up Visual Studio's test runner.
/// </summary>
internal static class Program
	{
	private static async Task<int> Main (string[] args)
		{
		TimeSpan timeout = TimeSpan.FromSeconds (5);
		if (args.Length > 0 && double.TryParse (args[0], out double seconds))
			{
			timeout = TimeSpan.FromSeconds (seconds);
			}

		Console.WriteLine ($"Scanning for MRP devices (\"{MrpServiceInfo.SERVICE_TYPE}\") for {timeout.TotalSeconds}s...");

		using CancellationTokenSource cts = new CancellationTokenSource ();
		Console.CancelKeyPress += (_, e) =>
			{
			e.Cancel = true;
			cts.Cancel ();
			};

		MulticastMrpDiscovery discovery = new MulticastMrpDiscovery ();
		IReadOnlyList<MrpDiscoveryResult> results;
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
			Console.WriteLine ("No MRP devices found.");
			return 0;
			}

		Console.WriteLine ($"Found {results.Count} device(s):");
		foreach (MrpDiscoveryResult result in results)
			{
			Console.WriteLine ("----------------------------------------");
			Console.WriteLine ($"Name:               {result.Name}");
			Console.WriteLine ($"Address:            {result.Address}");
			Console.WriteLine ($"Port:               {result.Port}");
			Console.WriteLine ($"UniqueId:           {result.UniqueId ?? "(none)"}");
			Console.WriteLine ($"IsEnabled:          {result.IsEnabled}");
			Console.WriteLine ($"PairingRequirement: {result.PairingRequirement}");
			Console.WriteLine ("Properties:");
			foreach (KeyValuePair<string, string> property in result.Properties)
				{
				Console.WriteLine ($"  {property.Key} = {property.Value}");
				}
			}

		return 0;
		}
	}
