// Copyright (c) 2026 Neil Colvin. Licensed under the MIT License.
// See LICENSE file in the repository root for full license text.

using System.Collections.Generic;

namespace AppleTvControlLibrary.Discovery.Dns;

/// <summary>Helpers for building batched DNS-SD service queries.</summary>
public static class DnsServiceQueries
	{
	/// <summary>Well-known sleep proxy service used to detect sleeping devices.</summary>
	// pyatv/core/mdns.py (SLEEP_PROXY_SERVICE) — line 30 as of pyatv 0.18.0
	public const string SLEEP_PROXY_SERVICE = "_sleep-proxy._udp.local";

	// pyatv/core/mdns.py (SERVICES_PER_MSG) — line 28 as of pyatv 0.18.0
	private const int SERVICES_PER_MESSAGE = 3;

	/// <summary>Creates service request messages, batching services into groups of three.</summary>
	/// <param name="services">The service types to query for.</param>
	/// <param name="qtype">The query type to use for each question.</param>
	/// <returns>The packed query messages.</returns>
	// pyatv/core/mdns.py (create_service_queries) — line 79-92 as of pyatv 0.18.0
	public static List<byte[]> CreateServiceQueries (IReadOnlyList<string> services, QueryType qtype)
		{
		List<byte[]> queries = new List<byte[]> ();
		int messageCount = (int)System.Math.Ceiling (services.Count / (double)SERVICES_PER_MESSAGE);
		for (int i = 0; i < messageCount; i++)
			{
			DnsMessage msg = new DnsMessage (0x35FF);
			int start = i * SERVICES_PER_MESSAGE;
			int end = System.Math.Min (start + 4, services.Count);
			for (int j = start; j < end; j++)
				{
				msg.Questions.Add (new DnsQuestion (services[j], qtype, 0x8001));
				}

			msg.Questions.Add (new DnsQuestion (SLEEP_PROXY_SERVICE, qtype, 0x8001));

			queries.Add (msg.Pack ());
			}

		return queries;
		}
	}
