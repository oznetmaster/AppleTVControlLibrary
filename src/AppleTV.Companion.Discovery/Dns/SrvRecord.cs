// Copyright (c) 2026 Neil Colvin. Licensed under the MIT License.
// See LICENSE file in the repository root for full license text.

namespace AppleTvControlLibrary.Discovery.Dns;

/// <summary>Represents a parsed DNS SRV record's RDATA.</summary>
// pyatv/support/dns.py (parse_srv_dict) — line 234-246 as of pyatv 0.18.0
public readonly struct SrvRecord
	{
	/// <summary>Initializes a new instance of the <see cref="SrvRecord"/> struct.</summary>
	public SrvRecord (ushort priority, ushort weight, ushort port, string target)
		{
		this.Priority = priority;
		this.Weight = weight;
		this.Port = port;
		this.Target = target;
		}

	/// <summary>Gets the record priority.</summary>
	public ushort Priority
		{
		get;
		}

	/// <summary>Gets the record weight.</summary>
	public ushort Weight
		{
		get;
		}

	/// <summary>Gets the target service port.</summary>
	public ushort Port
		{
		get;
		}

	/// <summary>Gets the target host name.</summary>
	public string Target
		{
		get;
		}
	}
