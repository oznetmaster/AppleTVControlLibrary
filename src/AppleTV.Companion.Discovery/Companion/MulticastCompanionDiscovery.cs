// Copyright (c) 2026 Neil Colvin. Licensed under the MIT License.
// See LICENSE file in the repository root for full license text.

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
// pyatv/core/mdns.py (MulticastDnsSdClientProtocol, multicast) — line 324-531 as of pyatv 0.18.0
public sealed class MulticastCompanionDiscovery : ICompanionDiscovery
	{
	// pyatv/core/mdns.py (multicast default address) — line 509 as of pyatv 0.18.0
	private const string MULTICAST_ADDRESS = "224.0.0.251";

	// pyatv/core/mdns.py (multicast default port) — line 510 as of pyatv 0.18.0, pyatv/core/mdns.py (unicast default port) — line 491 as of pyatv 0.18.0
	private const int MULTICAST_PORT = 5353;

	/// <inheritdoc/>
	public async Task<IReadOnlyList<CompanionDiscoveryResult>> ScanAsync (TimeSpan timeout, CancellationToken cancellationToken = default)
		{
		List<byte[]> queries = DnsServiceQueries.CreateServiceQueries (
			new[] { CompanionServiceInfo.SERVICE_TYPE },
			QueryType.Ptr);

		ServiceParser parser = new ServiceParser ();
		IPEndPoint groupEndpoint = new IPEndPoint (IPAddress.Parse (MULTICAST_ADDRESS), MULTICAST_PORT);

		using UdpClient client = new UdpClient (AddressFamily.InterNetwork);
		client.Client.SetSocketOption (SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
		client.Client.Bind (new IPEndPoint (IPAddress.Any, 0));
		client.JoinMulticastGroup (IPAddress.Parse (MULTICAST_ADDRESS));

		using CancellationTokenSource timeoutCts = new CancellationTokenSource (timeout);
		using CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource (cancellationToken, timeoutCts.Token);

		// UdpClient.ReceiveAsync() has no CancellationToken overload on all targeted TFMs, so the
		// receive loop cannot observe cancellation on its own. Closing the socket when the token
		// fires forces the pending ReceiveAsync() call to complete (with an ObjectDisposedException),
		// which is caught below. Without this, a scan with no responses would never return.
		using CancellationTokenRegistration registration = linkedCts.Token.Register (static state => ((UdpClient)state!).Close (), client);

		// pyatv/core/mdns.py (_resend_loop resends queries once per second for the duration) — line 385-408 as of pyatv 0.18.0
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
		catch (ObjectDisposedException)
			{
			// Expected when the socket is closed by the cancellation registration above.
			}
		catch (SocketException ex) when (ex.SocketErrorCode == SocketError.OperationAborted || ex.SocketErrorCode == SocketError.Interrupted)
			{
			// See ReceiveLoopAsync: Windows can surface the cancellation-triggered socket close
			// as a SocketException instead of ObjectDisposedException.
			}

		IReadOnlyList<Service> services = parser.Parse ();
		return services
			.Where (service => string.Equals (service.Type, CompanionServiceInfo.SERVICE_TYPE, StringComparison.OrdinalIgnoreCase))
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
					// pyatv/core/mdns.py (log and continue; a send failure to one target
					// shouldn't abort the scan) — line 414-415 as of pyatv 0.18.0
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
				// UdpClient.ReceiveAsync(CancellationToken) is not available on net472; this must build on both TFMs.
#pragma warning disable CA2016
				UdpReceiveResult result = await client.ReceiveAsync ().ConfigureAwait (false);
#pragma warning restore CA2016
				try
					{
					DnsMessage message = new DnsMessage ().Unpack (result.Buffer);
					parser.AddMessage (message);
					}
				catch (Exception)
					{
					// pyatv/core/mdns.py (suppress decode errors, but keep listening) — line 430-438 as of pyatv 0.18.0
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
		catch (SocketException ex) when (ex.SocketErrorCode == SocketError.OperationAborted || ex.SocketErrorCode == SocketError.Interrupted)
			{
			// On Windows, closing the underlying socket while ReceiveAsync() is pending surfaces
			// as a SocketException("The I/O operation has been aborted...", WSA_OPERATION_ABORTED)
			// rather than ObjectDisposedException. This is the same "socket closed to end the
			// scan" condition handled above, just a different exception shape on this platform.
			}
		}
	}
