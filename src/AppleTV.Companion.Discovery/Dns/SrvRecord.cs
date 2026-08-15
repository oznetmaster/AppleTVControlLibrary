// Copyright (c) 2026 Neil Colvin. Licensed under the MIT License.
// See LICENSE file in the repository root for full license text.

namespace AppleTvControlLibrary.Discovery.Dns;

/// <summary>Represents a parsed DNS SRV record's RDATA.</summary>
/// <remarks>Initializes a new instance of the <see cref="SrvRecord"/> struct.</remarks>
// pyatv/support/dns.py (parse_srv_dict) — line 234-246 as of pyatv 0.18.0
public readonly struct SrvRecord (ushort priority, ushort weight, ushort port, string target)
	{

	/// <summary>Gets the record priority.</summary>
	public ushort Priority
		{
		get;
		} = priority;

	/// <summary>Gets the record weight.</summary>
	public ushort Weight
		{
		get;
		} = weight;

	/// <summary>Gets the target service port.</summary>
	public ushort Port
		{
		get;
		} = port;

	/// <summary>Gets the target host name.</summary>
	public string Target
		{
		get;
		} = target;
	}
