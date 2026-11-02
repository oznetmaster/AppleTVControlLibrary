// Copyright (c) 2026 Neil Colvin. Licensed under the MIT License.
// See LICENSE file in the repository root for full license text.

using System.Collections.Generic;
using System.Net;

namespace AppleTvControlLibrary.Discovery.Mrp;

/// <summary>Represents a discovered MRP (Media Remote Protocol) service.</summary>
/// <remarks>Initializes a new instance of the <see cref="MrpDiscoveryResult"/> class.</remarks>
/// <param name="name">The advertised service or device name.</param>
/// <param name="address">The discovered IP address, if available.</param>
/// <param name="port">The advertised service port.</param>
/// <param name="uniqueId">The advertised unique device identifier, if available.</param>
/// <param name="isEnabled">Whether the MRP service is enabled.</param>
/// <param name="pairingRequirement">The pairing requirement advertised by the service.</param>
/// <param name="properties">The DNS-SD TXT properties advertised by the service.</param>
public sealed class MrpDiscoveryResult (
	string name,
	IPAddress? address,
	int port,
	string? uniqueId,
	bool isEnabled,
	MrpPairingRequirement pairingRequirement,
	IReadOnlyDictionary<string, string> properties)
	{

	/// <summary>Gets the mDNS service instance name (e.g. "Living Room").</summary>
	/// <value>The mDNS service instance name (e.g. "Living Room").</value>
	public string Name
		{
		get;
		} = name;

	/// <summary>Gets the resolved address of the device, if known.</summary>
	/// <value>The resolved address of the device, if known.</value>
	public IPAddress? Address
		{
		get;
		} = address;

	/// <summary>Gets the MRP port.</summary>
	/// <value>The MRP port.</value>
	public int Port
		{
		get;
		} = port;

	/// <summary>
	/// Gets a stable unique identifier for the device (the "UniqueIdentifier" TXT record), or
	/// <see langword="null"/> if not present.
	/// </summary>
	/// <value>
	/// A stable unique identifier for the device (the "UniqueIdentifier" TXT record), or
	/// <see langword="null"/> if not present.
	/// </value>
	// pyatv/helpers.py (get_unique_id, MEDIAREMOTE_SERVICE branch) — line 69-70 as of pyatv 0.18.0
	public string? UniqueId
		{
		get;
		} = uniqueId;

	/// <summary>
	/// Gets a value indicating whether MRP is considered enabled on this device. pyatv disables MRP
	/// once <c>SystemBuildVersion</c>'s leading numeric build component is 19 or higher (tvOS 15+,
	/// where Companion Link superseded MRP for remote control).
	/// </summary>
	/// <value>
	/// A value indicating whether MRP is considered enabled on this device. pyatv disables MRP
	/// once <c>SystemBuildVersion</c>'s leading numeric build component is 19 or higher (tvOS 15+,
	/// where Companion Link superseded MRP for remote control).
	/// </value>
	// pyatv/protocols/mrp/__init__.py (mrp_service_handler) — line 1029-1035 as of pyatv 0.18.0
	public bool IsEnabled
		{
		get;
		} = isEnabled;

	/// <summary>Gets whether/how pairing is required for this service.</summary>
	/// <value>Whether/how pairing is required for this service.</value>
	public MrpPairingRequirement PairingRequirement
		{
		get;
		} = pairingRequirement;

	/// <summary>Gets the raw decoded TXT record properties.</summary>
	/// <value>The raw decoded TXT record properties.</value>
	public IReadOnlyDictionary<string, string> Properties
		{
		get;
		} = properties;
	}
