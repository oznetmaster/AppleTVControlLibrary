// Copyright (c) 2026 Neil Colvin. Licensed under the MIT License.
// See LICENSE file in the repository root for full license text.

using System.Collections.Generic;
using System.Net;

namespace AppleTvControlLibrary.Discovery.AirPlay;

/// <summary>Represents a discovered AirPlay service.</summary>
/// <remarks>Initializes a new instance of the <see cref="AirPlayDiscoveryResult"/> class.</remarks>
public sealed class AirPlayDiscoveryResult (
	string name,
	IPAddress? address,
	int port,
	string? uniqueId,
	AirPlayPairingRequirement pairingRequirement,
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

	/// <summary>Gets the AirPlay port.</summary>
	public int Port
		{
		get;
		} = port;

	/// <summary>
	/// Gets a stable unique identifier for the device (the "deviceid" TXT record), or
	/// <see langword="null"/> if not present.
	/// </summary>
	// pyatv/helpers.py (get_unique_id, AIRPLAY_SERVICE branch) — line 69-70 as of pyatv 0.18.0
	public string? UniqueId
		{
		get;
		} = uniqueId;

	/// <summary>Gets whether/how pairing is required for this device.</summary>
	public AirPlayPairingRequirement PairingRequirement
		{
		get;
		} = pairingRequirement;

	/// <summary>Gets the raw decoded TXT record properties.</summary>
	public IReadOnlyDictionary<string, string> Properties
		{
		get;
		} = properties;
	}
