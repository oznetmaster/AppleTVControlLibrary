// Copyright (c) 2026 Neil Colvin. Licensed under the MIT License.
// See LICENSE file in the repository root for full license text.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Text;

namespace AppleTvControlLibrary.Discovery.Dns;

/// <summary>Low-level encode/decode helpers for the DNS wire format used by DNS-SD.</summary>
public static class DnsWireFormat
	{
	/// <summary>
	/// Encodes a QNAME without using name compression. Labels are UTF-8 encoded, NFC
	/// normalized, truncated to 63 bytes without splitting multi-byte code points, and
	/// terminated with an empty root label.
	/// </summary>
	/// <param name="name">The dotted name, or a service instance name, to encode.</param>
	/// <returns>The encoded QNAME bytes.</returns>
	// pyatv/support/dns.py (qname_encode) — line 71-135 as of pyatv 0.18.0
	public static byte[] QNameEncode (string name)
		{
		List<string> labels;
		try
			{
			ServiceInstanceName srvName = ServiceInstanceName.SplitName (name);
			labels = [];
			if (!string.IsNullOrEmpty (srvName.Instance))
				{
				labels.Add (srvName.Instance!);
				}

			labels.AddRange (srvName.PtrName.Split ('.'));
			}
		catch (ArgumentException)
			{
			labels = [.. name.Split ('.')];
			}

		// pyatv/support/dns.py (ensure a trailing empty label for the root domain) — line 100-102 as of pyatv 0.18.0
		if (labels.Count == 0 || labels[labels.Count - 1] != string.Empty)
			{
			labels.Add (string.Empty);
			}

		List<byte> encoded = [];
		foreach (string rawLabel in labels)
			{
			// pyatv/support/dns.py (NFC normalization per RFC 6763 section 4.1.3) — line 106-107 as of pyatv 0.18.0
			string label = rawLabel.Normalize (NormalizationForm.FormC);
			byte[] encodedLabel = Encoding.UTF8.GetBytes (label);

			// pyatv/support/dns.py (truncate at 63 bytes without splitting a codepoint) — line 111-118 as of pyatv 0.18.0
			while (encodedLabel.Length > 63)
				{
				string truncated = label[..^1];
				label = truncated;
				encodedLabel = Encoding.UTF8.GetBytes (label);
				}

			encoded.Add ((byte)encodedLabel.Length);
			if (encodedLabel.Length == 0)
				{
				// pyatv/support/dns.py (empty label ends the name) — line 130-133 as of pyatv 0.18.0
				break;
				}

			encoded.AddRange (encodedLabel);
			}

		return [.. encoded];
		}

	/// <summary>Unpacks a DNS character-string: a single length byte followed by data.</summary>
	/// <param name="buffer">The buffer to read from.</param>
	/// <returns>The raw character-string bytes.</returns>
	// pyatv/support/dns.py (parse_string) — line 138-146 as of pyatv 0.18.0
	public static byte[] ParseString (DnsBufferReader buffer)
		{
		byte length = buffer.ReadByte ();
		return buffer.ReadBytes (length);
		}

	/// <summary>
	/// Unpacks a domain name, handling DNS name compression (RFC 1035 sections 3.1, 4.1.4).
	/// </summary>
	/// <param name="buffer">The buffer to read from.</param>
	/// <returns>The decoded, dot-joined domain name.</returns>
	// pyatv/support/dns.py (parse_domain_name) — line 149-196 as of pyatv 0.18.0
	public static string ParseDomainName (DnsBufferReader buffer)
		{
		List<string> labels = [];
		int? compressionOffset = null;

		while (buffer.HasData)
			{
			byte length = buffer.ReadByte ();
			if (length == 0)
				{
				break;
				}

			// pyatv/support/dns.py (top two bits are a name-compression flag) — line 171-173 as of pyatv 0.18.0
			int lengthFlags = (length & 0xC0) >> 6;
			if (lengthFlags is not 0 and not 0b11)
				{
				throw new InvalidOperationException ("Reserved DNS name compression flag encountered");
				}

			if (lengthFlags == 0b11)
				{
				// pyatv/support/dns.py (mask upper bits, combine with next byte for offset) — line 175-186 as of pyatv 0.18.0
				int highBits = length & 0x3F;
				byte lowBits = buffer.ReadByte ();
				int newOffset = (highBits << 8) | lowBits;
				compressionOffset ??= buffer.Position;

				buffer.Position = newOffset;
				}
			else
				{
				byte[] label = buffer.ReadBytes (length);
				string decodedLabel;
				if (label.Length >= 4 && label[0] == 'x' && label[1] == 'n' && label[2] == '-' && label[3] == '-')
					{
					// pyatv/support/dns.py (ACE-prefixed labels are IDNA decoded) — line 189-190 as of pyatv 0.18.0
					decodedLabel = new IdnMapping ().GetUnicode (Encoding.ASCII.GetString (label));
					}
				else
					{
					decodedLabel = Encoding.UTF8.GetString (label);
					}

				labels.Add (decodedLabel);
				}
			}

		if (compressionOffset.HasValue)
			{
			buffer.Position = compressionOffset.Value;
			}

		return string.Join (".", labels);
		}

	/// <summary>Parses DNS-SD TXT records into a case-insensitive key/value map.</summary>
	/// <param name="buffer">The buffer to read from.</param>
	/// <param name="length">The total byte length of the TXT record data.</param>
	/// <returns>The decoded properties, keyed case-insensitively.</returns>
	// pyatv/support/dns.py (parse_txt_dict) — line 208-231 as of pyatv 0.18.0
	public static Dictionary<string, byte[]> ParseTxtDict (DnsBufferReader buffer, int length)
		{
		Dictionary<string, byte[]> output = new Dictionary<string, byte[]> (StringComparer.OrdinalIgnoreCase);
		int stopPosition = buffer.Position + length;
		while (buffer.Position < stopPosition)
			{
			byte[] chunk = ParseString (buffer);
			int equalsIndex = Array.IndexOf (chunk, (byte)'=');
			if (equalsIndex < 0)
				{
				// pyatv/support/dns.py (missing "=" means present with no value) — line 214-217 as of pyatv 0.18.0
				string decodedChunk = Encoding.ASCII.GetString (chunk);
				output[decodedChunk] = [];
				}
			else
				{
				byte[] keyBytes = new byte[equalsIndex];
				Array.Copy (chunk, 0, keyBytes, 0, equalsIndex);
				if (keyBytes.Length == 0)
					{
					// pyatv/support/dns.py (missing keys are skipped) — line 220-222 as of pyatv 0.18.0
					continue;
					}

				byte[] valueBytes = new byte[chunk.Length - equalsIndex - 1];
				Array.Copy (chunk, equalsIndex + 1, valueBytes, 0, valueBytes.Length);

				string decodedKey;
				try
					{
					decodedKey = Encoding.GetEncoding (
						"us-ascii",
						EncoderFallback.ExceptionFallback,
						DecoderFallback.ExceptionFallback).GetString (keyBytes);
					}
				catch (DecoderFallbackException)
					{
					// pyatv/support/dns.py (non-ASCII keys are skipped) — line 226-228 as of pyatv 0.18.0
					continue;
					}

				output[decodedKey] = valueBytes;
				}
			}

		return output;
		}

	/// <summary>Parses a DNS SRV record's RDATA.</summary>
	/// <param name="buffer">The buffer to read from.</param>
	/// <returns>The parsed priority, weight, port, and target.</returns>
	// pyatv/support/dns.py (parse_srv_dict) — line 234-246 as of pyatv 0.18.0
	public static SrvRecord ParseSrvRecord (DnsBufferReader buffer)
		{
		ushort priority = buffer.ReadUInt16BE ();
		ushort weight = buffer.ReadUInt16BE ();
		ushort port = buffer.ReadUInt16BE ();
		string target = ParseDomainName (buffer);
		return new SrvRecord (priority, weight, port, target);
		}

	/// <summary>
	/// Parses the RDATA of a DNS resource record according to its type. Falls back to raw
	/// bytes for unhandled types.
	/// </summary>
	/// <param name="type">The record type.</param>
	/// <param name="buffer">The buffer to read from.</param>
	/// <param name="length">The RDATA length in bytes.</param>
	/// <returns>The decoded RDATA, whose runtime type depends on <paramref name="type"/>.</returns>
	// pyatv/support/dns.py (QueryType.parse_rdata) — line 258-275 as of pyatv 0.18.0
	public static object ParseRData (QueryType type, DnsBufferReader buffer, int length)
		{
		switch (type)
			{
			case QueryType.A:
				if (length != 4)
					{
					throw new ArgumentException ($"An A record must have exactly 4 bytes of data (not {length})");
					}

				byte[] addressBytes = buffer.ReadBytes (4);
				return new IPAddress (addressBytes).ToString ();
			case QueryType.Ptr:
				return ParseDomainName (buffer);
			case QueryType.Txt:
				return ParseTxtDict (buffer, length);
			case QueryType.Srv:
				return ParseSrvRecord (buffer);
			default:
				return buffer.ReadBytes (length);
			}
		}
	}
