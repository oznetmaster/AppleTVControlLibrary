// Copyright (c) 2026 Neil Colvin. Licensed under the MIT License.
// See LICENSE file in the repository root for full license text.

namespace AppleTvControlLibrary.Discovery.Dns;

/// <summary>Represents a parsed DNS SRV record's RDATA.</summary>
/// <remarks>Initializes a new instance of the <see cref="SrvRecord"/> struct.</remarks>
/// <param name="priority">The SRV record priority; lower values are preferred.</param>
/// <param name="weight">The SRV record weight among records of equal priority.</param>
/// <param name="port">The advertised service port.</param>
/// <param name="target">The target hostname of the service.</param>
// pyatv/support/dns.py (parse_srv_dict) — line 234-246 as of pyatv 0.18.0
public readonly struct SrvRecord (ushort priority, ushort weight, ushort port, string target)
	{

	/// <summary>Gets the record priority.</summary>
	/// <value>The record priority.</value>
	public ushort Priority
		{
		get;
		} = priority;

	/// <summary>Gets the record weight.</summary>
	/// <value>The record weight.</value>
	public ushort Weight
		{
		get;
		} = weight;

	/// <summary>Gets the target service port.</summary>
	/// <value>The target service port.</value>
	public ushort Port
		{
		get;
		} = port;

	/// <summary>Gets the target host name.</summary>
	/// <value>The target host name.</value>
	public string Target
		{
		get;
		} = target;
	}
