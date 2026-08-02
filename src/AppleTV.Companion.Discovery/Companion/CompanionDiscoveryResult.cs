// Copyright (c) 2026 Neil Colvin. Licensed under the MIT License.
// See LICENSE file in the repository root for full license text.

using System.Collections.Generic;
using System.Net;

namespace AppleTvControlLibrary.Discovery.Companion;

/// <summary>Represents a discovered Companion Link service.</summary>
public sealed class CompanionDiscoveryResult
	{
	/// <summary>Initializes a new instance of the <see cref="CompanionDiscoveryResult"/> class.</summary>
	public CompanionDiscoveryResult (
		string name,
		IPAddress? address,
		int port,
		string? uniqueId,
		CompanionPairingRequirement pairingRequirement,
		IReadOnlyDictionary<string, string> properties)
		{
		this.Name = name;
		this.Address = address;
		this.Port = port;
		this.UniqueId = uniqueId;
		this.PairingRequirement = pairingRequirement;
		this.Properties = properties;
		}

	/// <summary>Gets the mDNS service instance name (e.g. "Living Room").</summary>
	public string Name
		{
		get;
		}

	/// <summary>Gets the resolved address of the device, if known.</summary>
	public IPAddress? Address
		{
		get;
		}

	/// <summary>Gets the Companion Link port.</summary>
	public int Port
		{
		get;
		}

	/// <summary>
	/// Gets a stable unique identifier for the device (the "rpmrtid" TXT record), or
	/// <see langword="null"/> if not present.
	/// </summary>
	/// <remarks>
	/// Only "rpmrtid" is safe to persist as an identifier. Do not key persisted credentials
	/// off other TXT records such as "rpHA", "rpHN", "rpAD", "rpHI", or "rpBA" - pyatv
	/// observes that those rotate as a privacy measure.
	/// </remarks>
	// pyatv/helpers.py (get_unique_id, COMPANION_SERVICE branch) — line 73-76 as of pyatv 0.18.0
	public string? UniqueId
		{
		get;
		}

	/// <summary>Gets whether/how pairing is required for this device.</summary>
	public CompanionPairingRequirement PairingRequirement
		{
		get;
		}

	/// <summary>Gets the raw decoded TXT record properties.</summary>
	public IReadOnlyDictionary<string, string> Properties
		{
		get;
		}
	}
