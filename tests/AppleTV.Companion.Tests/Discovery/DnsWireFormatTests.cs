// Copyright (c) 2026 Neil Colvin. Licensed under the MIT License.
// See LICENSE file in the repository root for full license text.

using System;
using System.Collections.Generic;
using System.Text;

using AppleTvControlLibrary.Discovery.Dns;

using NUnit.Framework;

namespace AppleTV.Companion.Tests.Discovery;

/// <summary>
/// Targeted tests for the DNS-SD wire-format port, since pyatv 0.18.0 has no dedicated
/// test_dns.py test-vector file to port from.
/// </summary>
[TestFixture]
[FixtureLifeCycle (LifeCycle.InstancePerTestCase)]
public class DnsWireFormatTests
	{
	// pyatv/support/dns.py (qname_encode) — line 71-135 as of pyatv 0.18.0 - basic multi-label name with root terminator
	[Test]
	public void QNameEncode_SimpleName_EncodesLengthPrefixedLabels ()
		{
		var encoded = DnsWireFormat.QNameEncode ("_companion-link._tcp.local");

		var expected = Concat (
			[(byte)"_companion-link".Length],
			Encoding.UTF8.GetBytes ("_companion-link"),
			[(byte)"_tcp".Length],
			Encoding.UTF8.GetBytes ("_tcp"),
			[(byte)"local".Length],
			Encoding.UTF8.GetBytes ("local"),
			[0]);

		Assert.That (encoded, Is.EqualTo (expected));
		}

	// pyatv/support/dns.py (parse_domain_name) — line 149-196 as of pyatv 0.18.0 - round trip through ParseDomainName
	[Test]
	public void ParseDomainName_RoundTripsSimpleName ()
		{
		var encoded = DnsWireFormat.QNameEncode ("Office._companion-link._tcp.local");
		DnsBufferReader reader = new DnsBufferReader (encoded);

		var decoded = DnsWireFormat.ParseDomainName (reader);

		Assert.That (decoded, Is.EqualTo ("Office._companion-link._tcp.local"));
		}

	// pyatv/support/dns.py (parse_string) — line 138-146 as of pyatv 0.18.0
	[Test]
	public void ParseString_ReadsLengthPrefixedData ()
		{
		byte[] data = [3, (byte)'a', (byte)'b', (byte)'c'];
		DnsBufferReader reader = new DnsBufferReader (data);

		var result = DnsWireFormat.ParseString (reader);

		Assert.That (result, Is.EqualTo (new byte[] { (byte)'a', (byte)'b', (byte)'c' }));
		}

	// pyatv/support/dns.py (parse_txt_dict) — line 199-231 as of pyatv 0.18.0 - key with no value and key=value pair
	[Test]
	public void ParseTxtDict_HandlesKeyWithNoValueAndKeyValuePair ()
		{
		List<byte> data = new List<byte> ();
		AppendString (data, "flag");
		AppendString (data, "rpmrtid=ABC123");

		DnsBufferReader reader = new DnsBufferReader (data.ToArray ());
		Dictionary<string, byte[]> result = DnsWireFormat.ParseTxtDict (reader, data.Count);

		Assert.That (result["flag"], Is.EqualTo (Array.Empty<byte> ()));
		Assert.That (result["rpmrtid"], Is.EqualTo (Encoding.ASCII.GetBytes ("ABC123")));
		}

	// pyatv/support/dns.py (parse_srv_dict) — line 234-246 as of pyatv 0.18.0
	[Test]
	public void ParseSrvRecord_ReadsPriorityWeightPortAndTarget ()
		{
		List<byte> data = new List<byte> ();
		AppendUInt16BE (data, 0);
		AppendUInt16BE (data, 0);
		AppendUInt16BE (data, 49152);
		data.AddRange (DnsWireFormat.QNameEncode ("device.local"));

		DnsBufferReader reader = new DnsBufferReader (data.ToArray ());
		SrvRecord srv = DnsWireFormat.ParseSrvRecord (reader);

		Assert.That (srv.Priority, Is.EqualTo (0));
		Assert.That (srv.Weight, Is.EqualTo (0));
		Assert.That (srv.Port, Is.EqualTo (49152));
		Assert.That (srv.Target, Is.EqualTo ("device.local"));
		}

	private static void AppendString (List<byte> buffer, string value)
		{
		var bytes = Encoding.ASCII.GetBytes (value);
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
		foreach (var array in arrays)
			{
			result.AddRange (array);
			}

		return result.ToArray ();
		}
	}