namespace AppleTvControlLibrary.Discovery.Dns;

/// <summary>Represents a parsed DNS SRV record's RDATA.</summary>
// pyatv/support/dns.py:234-246 (parse_srv_dict)
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
