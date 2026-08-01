using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

using AppleTvControlLibrary.Discovery.Dns;
using AppleTvControlLibrary.Discovery.Mdns;

namespace AppleTvControlLibrary.Discovery.Companion;

/// <summary>
/// Discovers Companion Link devices by sending DNS-SD PTR queries to the mDNS multicast
/// group and collecting responses.
/// </summary>
// pyatv/core/mdns.py:324-531 (MulticastDnsSdClientProtocol, multicast)
public sealed class MulticastCompanionDiscovery : ICompanionDiscovery
	{
	// pyatv/core/mdns.py:509 (multicast default address)
	private const string MulticastAddress = "224.0.0.251";

	// pyatv/core/mdns.py:510 (multicast default port), pyatv/core/mdns.py:491 (unicast default port)
	private const int MulticastPort = 5353;

	/// <inheritdoc/>
	public async Task<IReadOnlyList<CompanionDiscoveryResult>> ScanAsync (TimeSpan timeout, CancellationToken cancellationToken = default)
		{
		List<byte[]> queries = DnsServiceQueries.CreateServiceQueries (
			new[] { CompanionServiceInfo.ServiceType },
			QueryType.Ptr);

		ServiceParser parser = new ServiceParser ();
		IPEndPoint groupEndpoint = new IPEndPoint (IPAddress.Parse (MulticastAddress), MulticastPort);

		using UdpClient client = new UdpClient (AddressFamily.InterNetwork);
		client.Client.SetSocketOption (SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
		client.Client.Bind (new IPEndPoint (IPAddress.Any, 0));
		client.JoinMulticastGroup (IPAddress.Parse (MulticastAddress));

		using CancellationTokenSource timeoutCts = new CancellationTokenSource (timeout);
		using CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource (cancellationToken, timeoutCts.Token);

		// pyatv/core/mdns.py:385-408 (_resend_loop resends queries once per second for the duration)
		Task sendTask = ResendLoopAsync (client, groupEndpoint, queries, timeout, linkedCts.Token);
		Task receiveTask = ReceiveLoopAsync (client, parser, linkedCts.Token);

		try
			{
			await Task.WhenAll (sendTask, receiveTask).ConfigureAwait (false);
			}
		catch (OperationCanceledException)
			{
			// Expected once the timeout elapses.
			}

		client.Close ();

		IReadOnlyList<Service> services = parser.Parse ();
		return services
			.Where (service => string.Equals (service.Type, CompanionServiceInfo.ServiceType, StringComparison.OrdinalIgnoreCase))
			.Select (CompanionServiceInfo.ToDiscoveryResult)
			.ToList ();
		}

	private static async Task ResendLoopAsync (UdpClient client, IPEndPoint target, List<byte[]> queries, TimeSpan timeout, CancellationToken cancellationToken)
		{
		int iterations = (int)Math.Ceiling (timeout.TotalSeconds);
		for (int i = 0; i < iterations; i++)
			{
			foreach (byte[] query in queries)
				{
				try
					{
					await client.SendAsync (query, query.Length, target).ConfigureAwait (false);
					}
				catch (ObjectDisposedException)
					{
					return;
					}
				catch (SocketException)
					{
					// pyatv/core/mdns.py:414-415 (log and continue; a send failure to one target
					// shouldn't abort the scan)
					}
				}

			try
				{
				await Task.Delay (TimeSpan.FromSeconds (1), cancellationToken).ConfigureAwait (false);
				}
			catch (OperationCanceledException)
				{
				return;
				}
			}
		}

	private static async Task ReceiveLoopAsync (UdpClient client, ServiceParser parser, CancellationToken cancellationToken)
		{
		try
			{
			while (!cancellationToken.IsCancellationRequested)
				{
				UdpReceiveResult result = await client.ReceiveAsync ().ConfigureAwait (false);
				try
					{
					DnsMessage message = new DnsMessage ().Unpack (result.Buffer);
					parser.AddMessage (message);
					}
				catch (Exception)
					{
					// pyatv/core/mdns.py:430-438 (suppress decode errors, but keep listening)
					}
				}
			}
		catch (ObjectDisposedException)
			{
			// Socket was closed to end the scan.
			}
		catch (OperationCanceledException)
			{
			// Expected once the timeout elapses.
			}
		}
	}
