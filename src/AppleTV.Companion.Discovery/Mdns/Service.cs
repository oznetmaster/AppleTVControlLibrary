// Copyright (c) 2026 Neil Colvin. Licensed under the MIT License.
// See LICENSE file in the repository root for full license text.

using System.Collections.Generic;
using System.Net;

namespace AppleTvControlLibrary.Discovery.Mdns;

/// <summary>Represents an MDNS service.</summary>
/// <remarks>Initializes a new instance of the <see cref="Service"/> class.</remarks>
// pyatv/core/mdns.py (Service) — line 39-46 as of pyatv 0.18.0
public sealed class Service (string type, string name, IPAddress? address, int port, IReadOnlyDictionary<string, string> properties)
	{

	/// <summary>Gets the service type (e.g. "_companion-link._tcp.local").</summary>
	public string Type
		{
		get;
		} = type;

	/// <summary>Gets the service instance name (e.g. "Living Room").</summary>
	public string Name
		{
		get;
		} = name;

	/// <summary>Gets the resolved, non-link-local address of the service, if any.</summary>
	public IPAddress? Address
		{
		get;
		} = address;

	/// <summary>Gets the service port.</summary>
	public int Port
		{
		get;
		} = port;

	/// <summary>Gets the TXT record properties, keyed case-insensitively.</summary>
	public IReadOnlyDictionary<string, string> Properties
		{
		get;
		} = properties;
	}
