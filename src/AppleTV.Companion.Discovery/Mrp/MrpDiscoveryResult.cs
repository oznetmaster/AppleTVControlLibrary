// Copyright (c) 2026 Neil Colvin. Licensed under the MIT License.
// See LICENSE file in the repository root for full license text.

using System.Collections.Generic;
using System.Net;

namespace AppleTvControlLibrary.Discovery.Mrp;

/// <summary>Represents a discovered MRP (Media Remote Protocol) service.</summary>
public sealed class MrpDiscoveryResult
	{
	/// <summary>Initializes a new instance of the <see cref="MrpDiscoveryResult"/> class.</summary>
	public MrpDiscoveryResult (
		string name,
		IPAddress? address,
		int port,
		string? uniqueId,
		bool isEnabled,
		MrpPairingRequirement pairingRequirement,
		IReadOnlyDictionary<string, string> properties)
		{
		this.Name = name;
		this.Address = address;
		this.Port = port;
		this.UniqueId = uniqueId;
		this.IsEnabled = isEnabled;
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

	/// <summary>Gets the MRP port.</summary>
	public int Port
		{
		get;
		}

	/// <summary>
	/// Gets a stable unique identifier for the device (the "UniqueIdentifier" TXT record), or
	/// <see langword="null"/> if not present.
	/// </summary>
	// pyatv/helpers.py (get_unique_id, MEDIAREMOTE_SERVICE branch) — line 69-70 as of pyatv 0.18.0
	public string? UniqueId
		{
		get;
		}

	/// <summary>
	/// Gets a value indicating whether MRP is considered enabled on this device. pyatv disables MRP
	/// once <c>SystemBuildVersion</c>'s leading numeric build component is 19 or higher (tvOS 15+,
	/// where Companion Link superseded MRP for remote control).
	/// </summary>
	// pyatv/protocols/mrp/__init__.py (mrp_service_handler) — line 1029-1035 as of pyatv 0.18.0
	public bool IsEnabled
		{
		get;
		}

	/// <summary>Gets whether/how pairing is required for this service.</summary>
	public MrpPairingRequirement PairingRequirement
		{
		get;
		}

	/// <summary>Gets the raw decoded TXT record properties.</summary>
	public IReadOnlyDictionary<string, string> Properties
		{
		get;
		}
	}
