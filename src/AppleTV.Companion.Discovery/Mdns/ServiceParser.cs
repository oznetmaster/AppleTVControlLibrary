// Copyright (c) 2026 Neil Colvin. Licensed under the MIT License.
// See LICENSE file in the repository root for full license text.

using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

using AppleTvControlLibrary.Discovery.Dns;

namespace AppleTvControlLibrary.Discovery.Mdns;

/// <summary>Parses zeroconf/DNS-SD services from records collected across DNS messages.</summary>
// pyatv/core/mdns.py (ServiceParser) — line 106-174 as of pyatv 0.18.0
public sealed class ServiceParser
	{
	/// <summary>The DNS-SD device-info service type.</summary>
	// pyatv/core/mdns.py (DEVICE_INFO_SERVICE) — line 57 as of pyatv 0.18.0
	public const string DEVICE_INFO_SERVICE = "_device-info._tcp.local";

	private readonly Dictionary<string, Dictionary<QueryType, List<DnsResource>>> _table = new Dictionary<string, Dictionary<QueryType, List<DnsResource>>> ();
	private readonly Dictionary<string, string> _ptrs = new Dictionary<string, string> ();
	private List<Service>? _cache;

	/// <summary>Adds a message with records to parse.</summary>
	/// <param name="message">The DNS message whose answers and additional resources should be added.</param>
	/// <returns>This instance, for chaining.</returns>
	// pyatv/core/mdns.py (add_message) — line 115-129 as of pyatv 0.18.0
	public ServiceParser AddMessage (DnsMessage message)
		{
		this._cache = null;

		List<DnsResource> records = new List<DnsResource> (message.Answers);
		records.AddRange (message.Resources);

		foreach (DnsResource record in records)
			{
			// CA1865 (StartsWith(char)) is not available on net472; this must build on both TFMs.
#pragma warning disable CA1865
			if (record.QType == QueryType.Ptr && record.QName.StartsWith ("_", StringComparison.Ordinal))
#pragma warning restore CA1865
				{
				this._ptrs[record.QName] = (string)record.Rd;
				}
			else
				{
				if (!this._table.TryGetValue (record.QName, out Dictionary<QueryType, List<DnsResource>>? entry))
					{
					entry = new Dictionary<QueryType, List<DnsResource>> ();
					this._table[record.QName] = entry;
					}

				if (!entry.TryGetValue (record.QType, out List<DnsResource>? list))
					{
					list = new List<DnsResource> ();
					entry[record.QType] = list;
					}

				if (!list.Contains (record))
					{
					list.Add (record);
					}
				}
			}

		return this;
		}

	/// <summary>Parses previously added records and returns the discovered services.</summary>
	/// <returns>The parsed services.</returns>
	// pyatv/core/mdns.py (parse) — line 131-174 as of pyatv 0.18.0
	public IReadOnlyList<Service> Parse ()
		{
		if (this._cache is not null)
			{
			return this._cache;
			}

		Dictionary<string, Service> results = new Dictionary<string, Service> ();

		foreach (KeyValuePair<string, Dictionary<QueryType, List<DnsResource>>> pair in this._table)
			{
			string serviceQname = pair.Key;
			Dictionary<QueryType, List<DnsResource>> device = pair.Value;

			ServiceInstanceName serviceName;
			try
				{
				serviceName = ServiceInstanceName.SplitName (serviceQname);
				}
			catch (ArgumentException)
				{
				continue;
				}

			SrvRecord? srvRd = FirstRd<SrvRecord> (device, QueryType.Srv);
			string? target = srvRd?.Target;

			List<DnsResource> targetRecords = target is not null && this._table.TryGetValue (target, out Dictionary<QueryType, List<DnsResource>>? targetEntry) && targetEntry.TryGetValue (QueryType.A, out List<DnsResource>? aRecords)
				? aRecords
				: new List<DnsResource> ();

			IPAddress? address = null;
			foreach (DnsResource record in targetRecords)
				{
				IPAddress candidate = IPAddress.Parse ((string)record.Rd);
				if (!IsLinkLocal (candidate))
					{
					address = candidate;
					break;
					}
				}

			Dictionary<string, byte[]>? txt = FirstRdClass<Dictionary<string, byte[]>> (device, QueryType.Txt);

			results[serviceQname] = new Service (
				serviceName.PtrName,
				serviceName.Instance ?? string.Empty,
				address,
				srvRd?.Port ?? 0,
				DecodeProperties (txt ?? new Dictionary<string, byte[]> ()));
			}

		// pyatv/core/mdns.py (placeholders for PTRs to unknown services) — line 167-172 as of pyatv 0.18.0
		foreach (KeyValuePair<string, string> ptr in this._ptrs)
			{
			string realName = ptr.Value;
			if (!results.ContainsKey (realName))
				{
				string[] labels = realName.Split ('.');
				results[realName] = new Service (ptr.Key, labels[0], null, 0, new Dictionary<string, string> ());
				}
			}

		this._cache = new List<Service> (results.Values);
		return this._cache;
		}

	private static T? FirstRd<T> (Dictionary<QueryType, List<DnsResource>> device, QueryType type)
		where T : struct
		{
		if (device.TryGetValue (type, out List<DnsResource>? list) && list.Count > 0 && list[0].Rd is T value)
			{
			return value;
			}

		return null;
		}

	private static T? FirstRdClass<T> (Dictionary<QueryType, List<DnsResource>> device, QueryType type)
		where T : class
		{
		if (device.TryGetValue (type, out List<DnsResource>? list) && list.Count > 0 && list[0].Rd is T value)
			{
			return value;
			}

		return null;
		}

	private static bool IsLinkLocal (IPAddress address)
		{
		byte[] bytes = address.GetAddressBytes ();
		return bytes.Length == 4 && bytes[0] == 169 && bytes[1] == 254;
		}

	/// <summary>Decodes a bytes value, converting non-breaking-spaces to spaces before decoding.</summary>
	/// <param name="value">The raw TXT record value.</param>
	/// <returns>The decoded string.</returns>
	// pyatv/core/mdns.py (decode_value) — line 60-70 as of pyatv 0.18.0
	public static string DecodeValue (byte[] value)
		{
		try
			{
			byte[] replaced = ReplaceSequence (value, new byte[] { 0xC2, 0xA0 }, (byte)' ');
			replaced = ReplaceSequence (replaced, new byte[] { 0x00, 0xA0 }, (byte)' ');
			return Encoding.UTF8.GetString (replaced);
			}
		catch (Exception)
			{
			return BitConverter.ToString (value);
			}
		}

	private static byte[] ReplaceSequence (byte[] source, byte[] pattern, byte replacement)
		{
		List<byte> result = new List<byte> (source.Length);
		int i = 0;
		while (i < source.Length)
			{
			bool matches = i + pattern.Length <= source.Length;
			if (matches)
				{
				for (int j = 0; j < pattern.Length; j++)
					{
					if (source[i + j] != pattern[j])
						{
						matches = false;
						break;
						}
					}
				}

			if (matches)
				{
				result.Add (replacement);
				i += pattern.Length;
				}
			else
				{
				result.Add (source[i]);
				i += 1;
				}
			}

		return result.ToArray ();
		}

	// pyatv/core/mdns.py (_decode_properties) — line 73-76 as of pyatv 0.18.0
	private static Dictionary<string, string> DecodeProperties (Dictionary<string, byte[]> properties)
		{
		Dictionary<string, string> result = new Dictionary<string, string> (StringComparer.OrdinalIgnoreCase);
		foreach (KeyValuePair<string, byte[]> pair in properties)
			{
			result[pair.Key] = DecodeValue (pair.Value);
			}

		return result;
		}
	}
