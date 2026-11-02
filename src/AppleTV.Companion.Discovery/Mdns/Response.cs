// Copyright (c) 2026 Neil Colvin. Licensed under the MIT License.
// See LICENSE file in the repository root for full license text.

using System.Collections.Generic;

namespace AppleTvControlLibrary.Discovery.Mdns;

/// <summary>Represents the response to an MDNS request.</summary>
/// <remarks>Initializes a new instance of the <see cref="Response"/> class.</remarks>
/// <param name="services">The parsed DNS-SD services in this response.</param>
/// <param name="deepSleep">Whether the device was discovered in deep sleep.</param>
/// <param name="model">The advertised device model, if available.</param>
// pyatv/core/mdns.py (Response) — line 49-54 as of pyatv 0.18.0
public sealed class Response (IReadOnlyList<Service> services, bool deepSleep, string? model)
	{

	/// <summary>Gets the services discovered in this response.</summary>
	/// <value>The services discovered in this response.</value>
	public IReadOnlyList<Service> Services
		{
		get;
		} = services;

	/// <summary>Gets a value indicating whether the responding device appears to be a sleep proxy.</summary>
	/// <value>A value indicating whether the responding device appears to be a sleep proxy.</value>
	public bool DeepSleep
		{
		get;
		} = deepSleep;

	/// <summary>Gets the device model, from the "_device-info._tcp.local" service, if present.</summary>
	/// <value>The device model, from the "_device-info._tcp.local" service, if present.</value>
	// pyatv/core/mdns.py (DEVICE_INFO_SERVICE) — line 57 as of pyatv 0.18.0
	public string? Model
		{
		get;
		} = model;
	}
