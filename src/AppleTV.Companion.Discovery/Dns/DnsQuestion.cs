// Copyright (c) 2026 Neil Colvin. Licensed under the MIT License.
// See LICENSE file in the repository root for full license text.

namespace AppleTvControlLibrary.Discovery.Dns;

/// <summary>Represents a DNS query.</summary>
/// <remarks>Initializes a new instance of the <see cref="DnsQuestion"/> class.</remarks>
/// <param name="qname">The DNS record name.</param>
/// <param name="qtype">The DNS record type.</param>
/// <param name="qclass">The DNS record class.</param>
// pyatv/support/dns.py (DnsQuestion) — line 305-329 as of pyatv 0.18.0
public sealed class DnsQuestion (string qname, QueryType qtype, ushort qclass)
	{

	/// <summary>Gets the query name.</summary>
	/// <value>The query name.</value>
	public string QName
		{
		get;
		} = qname;

	/// <summary>Gets the query type.</summary>
	/// <value>The query type.</value>
	public QueryType QType
		{
		get;
		} = qtype;

	/// <summary>Gets the query class.</summary>
	/// <value>The query class.</value>
	public ushort QClass
		{
		get;
		} = qclass;

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
		byte[] qname = DnsWireFormat.QNameEncode (QName);
		byte[] result = new byte[qname.Length + 4];
		System.Array.Copy (qname, result, qname.Length);
		result[qname.Length] = (byte)((ushort)QType >> 8);
		result[qname.Length + 1] = (byte)((ushort)QType & 0xFF);
		result[qname.Length + 2] = (byte)(QClass >> 8);
		result[qname.Length + 3] = (byte)(QClass & 0xFF);
		return result;
		}
	}
