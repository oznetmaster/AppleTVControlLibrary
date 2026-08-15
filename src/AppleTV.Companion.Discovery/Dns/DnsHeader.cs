// Copyright (c) 2026 Neil Colvin. Licensed under the MIT License.
// See LICENSE file in the repository root for full license text.

namespace AppleTvControlLibrary.Discovery.Dns;

/// <summary>Represents the header to a DNS message.</summary>
// pyatv/support/dns.py (DnsHeader) — line 278-302 as of pyatv 0.18.0
internal sealed class DnsHeader (ushort id, ushort flags, ushort qdcount, ushort ancount, ushort nscount, ushort arcount)
	{
	public ushort Id
		{
		get;
		} = id;

	public ushort Flags
		{
		get;
		} = flags;

	public ushort Qdcount
		{
		get;
		} = qdcount;

	public ushort Ancount
		{
		get;
		} = ancount;

	public ushort Nscount
		{
		get;
		} = nscount;

	public ushort Arcount
		{
		get;
		} = arcount;

	// pyatv/support/dns.py (unpack_read) — line 291-298 as of pyatv 0.18.0
	public static DnsHeader UnpackRead (DnsBufferReader buffer)
		{
		ushort id = buffer.ReadUInt16BE ();
		ushort flags = buffer.ReadUInt16BE ();
		ushort qdcount = buffer.ReadUInt16BE ();
		ushort ancount = buffer.ReadUInt16BE ();
		ushort nscount = buffer.ReadUInt16BE ();
		ushort arcount = buffer.ReadUInt16BE ();
		return new DnsHeader (id, flags, qdcount, ancount, nscount, arcount);
		}

	// pyatv/support/dns.py (pack) — line 300-302 as of pyatv 0.18.0
	public byte[] Pack ()
		{
		byte[] result = new byte[12];
		WriteUInt16BE (result, 0, Id);
		WriteUInt16BE (result, 2, Flags);
		WriteUInt16BE (result, 4, Qdcount);
		WriteUInt16BE (result, 6, Ancount);
		WriteUInt16BE (result, 8, Nscount);
		WriteUInt16BE (result, 10, Arcount);
		return result;
		}

	private static void WriteUInt16BE (byte[] buffer, int offset, ushort value)
		{
		buffer[offset] = (byte)(value >> 8);
		buffer[offset + 1] = (byte)(value & 0xFF);
		}
	}
