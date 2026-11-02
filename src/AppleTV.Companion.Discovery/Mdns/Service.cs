// Copyright (c) 2026 Neil Colvin. Licensed under the MIT License.
// See LICENSE file in the repository root for full license text.

using System.Collections.Generic;
using System.Net;

namespace AppleTvControlLibrary.Discovery.Mdns;

/// <summary>Represents an MDNS service.</summary>
/// <remarks>Initializes a new instance of the <see cref="Service"/> class.</remarks>
/// <param name="type">The DNS-SD service type.</param>
/// <param name="name">The advertised service or device name.</param>
/// <param name="address">The discovered IP address, if available.</param>
/// <param name="port">The advertised service port.</param>
/// <param name="properties">The DNS-SD TXT properties advertised by the service.</param>
// pyatv/core/mdns.py (Service) — line 39-46 as of pyatv 0.18.0
public sealed class Service (string type, string name, IPAddress? address, int port, IReadOnlyDictionary<string, string> properties)
	{

	/// <summary>Gets the service type (e.g. "_companion-link._tcp.local").</summary>
	/// <value>The service type (e.g. "_companion-link._tcp.local").</value>
	public string Type
		{
		get;
		} = type;

	/// <summary>Gets the service instance name (e.g. "Living Room").</summary>
	/// <value>The service instance name (e.g. "Living Room").</value>
	public string Name
		{
		get;
		} = name;

	/// <summary>Gets the resolved, non-link-local address of the service, if any.</summary>
	/// <value>The resolved, non-link-local address of the service, if any.</value>
	public IPAddress? Address
		{
		get;
		} = address;

	/// <summary>Gets the service port.</summary>
	/// <value>The service port.</value>
	public int Port
		{
		get;
		} = port;

	/// <summary>Gets the TXT record properties, keyed case-insensitively.</summary>
	/// <value>The TXT record properties, keyed case-insensitively.</value>
	public IReadOnlyDictionary<string, string> Properties
		{
		get;
		} = properties;
	}
