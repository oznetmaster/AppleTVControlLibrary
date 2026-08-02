// Copyright (c) 2026 Neil Colvin. Licensed under the MIT License.
// See LICENSE file in the repository root for full license text.

namespace AppleTvControlLibrary.Discovery.Dns;

/// <summary>Represents a DNS query.</summary>
// pyatv/support/dns.py (DnsQuestion) — line 305-329 as of pyatv 0.18.0
public sealed class DnsQuestion
	{
	/// <summary>Initializes a new instance of the <see cref="DnsQuestion"/> class.</summary>
	public DnsQuestion (string qname, QueryType qtype, ushort qclass)
		{
		this.QName = qname;
		this.QType = qtype;
		this.QClass = qclass;
		}

	/// <summary>Gets the query name.</summary>
	public string QName
		{
		get;
		}

	/// <summary>Gets the query type.</summary>
	public QueryType QType
		{
		get;
		}

	/// <summary>Gets the query class.</summary>
	public ushort QClass
		{
		get;
		}

	/// <summary>Creates a <see cref="DnsQuestion"/> from a data stream.</summary>
	/// <param name="buffer">The buffer to read from.</param>
	/// <returns>The parsed question.</returns>
	// pyatv/support/dns.py (unpack_read) — line 312-321 as of pyatv 0.18.0
	public static DnsQuestion UnpackRead (DnsBufferReader buffer)
		{
		string qname = DnsWireFormat.ParseDomainName (buffer);
		QueryType qtype = (QueryType)buffer.ReadUInt16BE ();
		ushort qclass = buffer.ReadUInt16BE ();
		return new DnsQuestion (qname, qtype, qclass);
		}

	/// <summary>Encodes the question data as needed for a DNS query or response.</summary>
	/// <returns>The packed question bytes.</returns>
	// pyatv/support/dns.py (pack) — line 323-329 as of pyatv 0.18.0
	public byte[] Pack ()
		{
		byte[] qname = DnsWireFormat.QNameEncode (this.QName);
		byte[] result = new byte[qname.Length + 4];
		System.Array.Copy (qname, result, qname.Length);
		result[qname.Length] = (byte)((ushort)this.QType >> 8);
		result[qname.Length + 1] = (byte)((ushort)this.QType & 0xFF);
		result[qname.Length + 2] = (byte)(this.QClass >> 8);
		result[qname.Length + 3] = (byte)(this.QClass & 0xFF);
		return result;
		}
	}
