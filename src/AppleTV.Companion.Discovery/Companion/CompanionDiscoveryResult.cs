// Copyright (c) 2026 Neil Colvin. Licensed under the MIT License.
// See LICENSE file in the repository root for full license text.

using System.Collections.Generic;
using System.Net;

namespace AppleTvControlLibrary.Discovery.Companion;

/// <summary>Represents a discovered Companion Link service.</summary>
/// <remarks>Initializes a new instance of the <see cref="CompanionDiscoveryResult"/> class.</remarks>
public sealed class CompanionDiscoveryResult (
	string name,
	IPAddress? address,
	int port,
	string? uniqueId,
	CompanionPairingRequirement pairingRequirement,
	IReadOnlyDictionary<string, string> properties)
	{

	/// <summary>Gets the mDNS service instance name (e.g. "Living Room").</summary>
	public string Name
		{
		get;
		} = name;

	/// <summary>Gets the resolved address of the device, if known.</summary>
	public IPAddress? Address
		{
		get;
		} = address;

	/// <summary>Gets the Companion Link port.</summary>
	public int Port
		{
		get;
		} = port;

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
		} = uniqueId;

	/// <summary>Gets whether/how pairing is required for this device.</summary>
	public CompanionPairingRequirement PairingRequirement
		{
		get;
		} = pairingRequirement;

	/// <summary>Gets the raw decoded TXT record properties.</summary>
	public IReadOnlyDictionary<string, string> Properties
		{
		get;
		} = properties;
	}
