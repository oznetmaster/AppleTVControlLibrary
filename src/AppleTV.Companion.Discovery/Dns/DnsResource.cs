// Copyright (c) 2026 Neil Colvin. Licensed under the MIT License.
// See LICENSE file in the repository root for full license text.

namespace AppleTvControlLibrary.Discovery.Dns;

/// <summary>Represents a DNS resource record.</summary>
// pyatv/support/dns.py (DnsResource) — line 332-358 as of pyatv 0.18.0
public sealed class DnsResource
	{
	/// <summary>Initializes a new instance of the <see cref="DnsResource"/> class.</summary>
	public DnsResource (string qname, QueryType qtype, ushort qclass, uint ttl, ushort rdLength, object rd)
		{
		this.QName = qname;
		this.QType = qtype;
		this.QClass = qclass;
		this.Ttl = ttl;
		this.RdLength = rdLength;
		this.Rd = rd;
		}

	/// <summary>Gets the record name.</summary>
	public string QName
		{
		get;
		}

	/// <summary>Gets the record type.</summary>
	public QueryType QType
		{
		get;
		}

	/// <summary>Gets the record class.</summary>
	public ushort QClass
		{
		get;
		}

	/// <summary>Gets the record time-to-live.</summary>
	public uint Ttl
		{
		get;
		}

	/// <summary>Gets the RDATA byte length as read from the wire.</summary>
	public ushort RdLength
		{
		get;
		}

	/// <summary>
	/// Gets the parsed RDATA. The runtime type depends on <see cref="QType"/>: a
	/// <see cref="string"/> for A/PTR, a <see cref="System.Collections.Generic.Dictionary{TKey,TValue}"/>
	/// of <see cref="string"/> to <see cref="byte"/>[] for TXT, a <see cref="SrvRecord"/> for
	/// SRV, or raw <see cref="byte"/>[] otherwise.
	/// </summary>
	public object Rd
		{
		get;
		}

	/// <summary>Creates a <see cref="DnsResource"/> from data in a data stream.</summary>
	/// <param name="buffer">The buffer to read from.</param>
	/// <returns>The parsed resource record.</returns>
	// pyatv/support/dns.py (unpack_read) — line 342-358 as of pyatv 0.18.0
	public static DnsResource UnpackRead (DnsBufferReader buffer)
		{
		string qname = DnsWireFormat.ParseDomainName (buffer);
		ushort rawQType = buffer.ReadUInt16BE ();
		ushort qclass = buffer.ReadUInt16BE ();
		uint ttl = buffer.ReadUInt32BE ();
		ushort rdLength = buffer.ReadUInt16BE ();

		object rd;
		if (System.Enum.IsDefined (typeof (QueryType), (int)rawQType))
			{
			QueryType qtype = (QueryType)rawQType;
			rd = DnsWireFormat.ParseRData (qtype, buffer, rdLength);
			return new DnsResource (qname, qtype, qclass, ttl, rdLength, rd);
			}

		rd = buffer.ReadBytes (rdLength);
		return new DnsResource (qname, (QueryType)rawQType, qclass, ttl, rdLength, rd);
		}
	}
