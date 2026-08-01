namespace AppleTvControlLibrary.Discovery.Dns;

/// <summary>DNS resource record type identifiers used by DNS-SD.</summary>
// pyatv/support/dns.py:249-256 (QueryType)
public enum QueryType
	{
	/// <summary>Address record.</summary>
	A = 0x01,

	/// <summary>Domain name pointer.</summary>
	Ptr = 0x0C,

	/// <summary>Text record.</summary>
	Txt = 0x10,

	/// <summary>Service record.</summary>
	Srv = 0x21,

	/// <summary>Wildcard query for any record type.</summary>
	Any = 0xFF,
	}
