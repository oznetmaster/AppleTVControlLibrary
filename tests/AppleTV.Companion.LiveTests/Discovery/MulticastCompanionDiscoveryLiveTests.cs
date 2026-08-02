// Copyright (c) 2026 Neil Colvin. Licensed under the MIT License.
// See LICENSE file in the repository root for full license text.

using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

using AppleTvControlLibrary.Discovery.Companion;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AppleTvControlLibrary.LiveTests.Discovery;

/// <summary>
/// Live, socket-backed tests for <see cref="MulticastCompanionDiscovery"/>: a
/// <see cref="FakeMdnsResponder"/> joins the real mDNS multicast group on loopback and answers
/// PTR/SRV/TXT/A queries exactly as a real Apple TV would, so the production discovery code
/// path (multicast join, resend loop, receive loop, cancellation-triggered socket close) is
/// exercised end-to-end instead of only against pre-decoded records.
/// </summary>
[TestClass]
public sealed class MulticastCompanionDiscoveryLiveTests
	{
	/// <summary>
	/// Scanning against a responding fake device should return exactly that device, with the
	/// TXT-derived unique id and pairing requirement decoded correctly.
	/// </summary>
	[TestMethod]
	public async Task ScanAsync_RespondingDevice_IsDiscovered ()
		{
		using FakeMdnsResponder responder = new FakeMdnsResponder (
			serviceType: CompanionServiceInfo.SERVICE_TYPE,
			instanceName: "Live Test Room",
			hostName: "live-test-atv.local",
			address: IPAddress.Loopback,
			port: 49152,
			txtProperties: new System.Collections.Generic.Dictionary<string, string>
				{
				["rpmrtid"] = "AAAAAAAAAAAA",
				["rpfl"] = "0x4000",
				});
		responder.Start ();

		MulticastCompanionDiscovery discovery = new MulticastCompanionDiscovery ();
		System.Collections.Generic.IReadOnlyList<CompanionDiscoveryResult> results =
			await discovery.ScanAsync (TimeSpan.FromSeconds (3)).ConfigureAwait (false);

		CompanionDiscoveryResult? found = null;
		foreach (CompanionDiscoveryResult result in results)
			{
			if (string.Equals (result.Name, "Live Test Room", StringComparison.Ordinal))
				{
				found = result;
				break;
				}
			}

		Assert.IsNotNull (found, "Expected the fake device to be discovered by a live mDNS scan.");
		Assert.AreEqual (49152, found!.Port);
		Assert.AreEqual ("AAAAAAAAAAAA", found.UniqueId);
		Assert.AreEqual (CompanionPairingRequirement.Mandatory, found.PairingRequirement);
		Assert.AreEqual (IPAddress.Loopback, found.Address);
		}

	/// <summary>
	/// A scan with no responder running must still return (empty) within roughly the requested
	/// timeout, guarding against the "scan never returns" regression where the receive loop's
	/// pending ReceiveAsync() wasn't unblocked by cancellation.
	/// </summary>
	[TestMethod]
	public async Task ScanAsync_NoResponder_ReturnsWithinTimeout ()
		{
		MulticastCompanionDiscovery discovery = new MulticastCompanionDiscovery ();

		DateTime start = DateTime.UtcNow;
		System.Collections.Generic.IReadOnlyList<CompanionDiscoveryResult> results =
			await discovery.ScanAsync (TimeSpan.FromSeconds (2)).ConfigureAwait (false);
		TimeSpan elapsed = DateTime.UtcNow - start;

		Assert.IsTrue (elapsed < TimeSpan.FromSeconds (5), $"Scan took {elapsed}, expected it to return promptly after its 2s timeout.");
		}

	/// <summary>
	/// Cancelling the scan's token before the timeout elapses must also unblock the receive
	/// loop promptly (the same code path exercised by a user-cancelled scan in the UI).
	/// </summary>
	[TestMethod]
	public async Task ScanAsync_CancelledEarly_ReturnsPromptly ()
		{
		MulticastCompanionDiscovery discovery = new MulticastCompanionDiscovery ();
		using CancellationTokenSource cts = new CancellationTokenSource ();

		Task<System.Collections.Generic.IReadOnlyList<CompanionDiscoveryResult>> scanTask =
			discovery.ScanAsync (TimeSpan.FromSeconds (30), cts.Token);

		await Task.Delay (TimeSpan.FromMilliseconds (250)).ConfigureAwait (false);
		DateTime cancelledAt = DateTime.UtcNow;
		cts.Cancel ();

		await scanTask.ConfigureAwait (false);
		TimeSpan elapsed = DateTime.UtcNow - cancelledAt;

		Assert.IsTrue (elapsed < TimeSpan.FromSeconds (5), $"Scan took {elapsed} to return after cancellation, expected it to unblock promptly.");
		}
	}
