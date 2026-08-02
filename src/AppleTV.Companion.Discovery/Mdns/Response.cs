using System.Collections.Generic;

namespace AppleTvControlLibrary.Discovery.Mdns;

/// <summary>Represents the response to an MDNS request.</summary>
// pyatv/core/mdns.py (Response) — line 49-54 as of pyatv 0.18.0
public sealed class Response
	{
	/// <summary>Initializes a new instance of the <see cref="Response"/> class.</summary>
	public Response (IReadOnlyList<Service> services, bool deepSleep, string? model)
		{
		this.Services = services;
		this.DeepSleep = deepSleep;
		this.Model = model;
		}

	/// <summary>Gets the services discovered in this response.</summary>
	public IReadOnlyList<Service> Services
		{
		get;
		}

	/// <summary>Gets a value indicating whether the responding device appears to be a sleep proxy.</summary>
	public bool DeepSleep
		{
		get;
		}

	/// <summary>Gets the device model, from the "_device-info._tcp.local" service, if present.</summary>
	// pyatv/core/mdns.py (DEVICE_INFO_SERVICE) — line 57 as of pyatv 0.18.0
	public string? Model
		{
		get;
		}
	}
