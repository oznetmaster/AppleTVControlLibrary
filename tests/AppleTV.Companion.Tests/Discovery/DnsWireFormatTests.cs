using System;
using System.Collections.Generic;
using System.Text;

using AppleTvControlLibrary.Discovery.Dns;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AppleTV.Companion.Tests.Discovery;

/// <summary>
/// Targeted tests for the DNS-SD wire-format port, since pyatv 0.18.0 has no dedicated
/// test_dns.py test-vector file to port from.
/// </summary>
[TestClass]
public class DnsWireFormatTests
	{
	// pyatv/support/dns.py (qname_encode) — line 71-135 as of pyatv 0.18.0 - basic multi-label name with root terminator
	[TestMethod]
	public void QNameEncode_SimpleName_EncodesLengthPrefixedLabels ()
		{
		byte[] encoded = DnsWireFormat.QNameEncode ("_companion-link._tcp.local");

		byte[] expected = Concat (
			new byte[] { (byte)"_companion-link".Length },
			Encoding.UTF8.GetBytes ("_companion-link"),
			new byte[] { (byte)"_tcp".Length },
			Encoding.UTF8.GetBytes ("_tcp"),
			new byte[] { (byte)"local".Length },
			Encoding.UTF8.GetBytes ("local"),
			new byte[] { 0 });

		CollectionAssert.AreEqual (expected, encoded);
		}

	// pyatv/support/dns.py (parse_domain_name) — line 149-196 as of pyatv 0.18.0 - round trip through ParseDomainName
	[TestMethod]
	public void ParseDomainName_RoundTripsSimpleName ()
		{
		byte[] encoded = DnsWireFormat.QNameEncode ("Office._companion-link._tcp.local");
		DnsBufferReader reader = new DnsBufferReader (encoded);

		string decoded = DnsWireFormat.ParseDomainName (reader);

		Assert.AreEqual ("Office._companion-link._tcp.local", decoded);
		}

	// pyatv/support/dns.py (parse_string) — line 138-146 as of pyatv 0.18.0
	[TestMethod]
	public void ParseString_ReadsLengthPrefixedData ()
		{
		byte[] data = new byte[] { 3, (byte)'a', (byte)'b', (byte)'c' };
		DnsBufferReader reader = new DnsBufferReader (data);

		byte[] result = DnsWireFormat.ParseString (reader);

		CollectionAssert.AreEqual (new byte[] { (byte)'a', (byte)'b', (byte)'c' }, result);
		}

	// pyatv/support/dns.py (parse_txt_dict) — line 199-231 as of pyatv 0.18.0 - key with no value and key=value pair
	[TestMethod]
	public void ParseTxtDict_HandlesKeyWithNoValueAndKeyValuePair ()
		{
		List<byte> data = new List<byte> ();
		AppendString (data, "flag");
		AppendString (data, "rpmrtid=ABC123");

		DnsBufferReader reader = new DnsBufferReader (data.ToArray ());
		Dictionary<string, byte[]> result = DnsWireFormat.ParseTxtDict (reader, data.Count);

		CollectionAssert.AreEqual (Array.Empty<byte> (), result["flag"]);
		CollectionAssert.AreEqual (Encoding.ASCII.GetBytes ("ABC123"), result["rpmrtid"]);
		}

	// pyatv/support/dns.py (parse_srv_dict) — line 234-246 as of pyatv 0.18.0
	[TestMethod]
	public void ParseSrvRecord_ReadsPriorityWeightPortAndTarget ()
		{
		List<byte> data = new List<byte> ();
		AppendUInt16BE (data, 0);
		AppendUInt16BE (data, 0);
		AppendUInt16BE (data, 49152);
		data.AddRange (DnsWireFormat.QNameEncode ("device.local"));

		DnsBufferReader reader = new DnsBufferReader (data.ToArray ());
		SrvRecord srv = DnsWireFormat.ParseSrvRecord (reader);

		Assert.AreEqual (0, srv.Priority);
		Assert.AreEqual (0, srv.Weight);
		Assert.AreEqual (49152, srv.Port);
		Assert.AreEqual ("device.local", srv.Target);
		}

	private static void AppendString (List<byte> buffer, string value)
		{
		byte[] bytes = Encoding.ASCII.GetBytes (value);
		buffer.Add ((byte)bytes.Length);
		buffer.AddRange (bytes);
		}

	private static void AppendUInt16BE (List<byte> buffer, ushort value)
		{
		buffer.Add ((byte)(value >> 8));
		buffer.Add ((byte)value);
		}

	private static byte[] Concat (params byte[][] arrays)
		{
		List<byte> result = new List<byte> ();
		foreach (byte[] array in arrays)
			{
			result.AddRange (array);
			}

		return result.ToArray ();
		}
	}
