using System.Collections.Generic;

namespace AppleTvControlLibrary.Discovery.Dns;

/// <summary>Helpers for building batched DNS-SD service queries.</summary>
public static class DnsServiceQueries
	{
	/// <summary>Well-known sleep proxy service used to detect sleeping devices.</summary>
	// pyatv/core/mdns.py:30 (SLEEP_PROXY_SERVICE)
	public const string SLEEP_PROXY_SERVICE = "_sleep-proxy._udp.local";

	// pyatv/core/mdns.py:28 (SERVICES_PER_MSG)
	private const int SERVICES_PER_MESSAGE = 3;

	/// <summary>Creates service request messages, batching services into groups of three.</summary>
	/// <param name="services">The service types to query for.</param>
	/// <param name="qtype">The query type to use for each question.</param>
	/// <returns>The packed query messages.</returns>
	// pyatv/core/mdns.py:79-92 (create_service_queries)
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
