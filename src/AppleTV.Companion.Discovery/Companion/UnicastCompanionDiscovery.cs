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
/// Discovers Companion Link services by querying mDNS directly at a known device address.
/// </summary>
// pyatv/core/mdns.py (unicast) — line 487-503 as of pyatv 0.18.0
public sealed class UnicastCompanionDiscovery : ICompanionDiscovery
	{
	// pyatv/core/mdns.py (unicast default port) — line 491 as of pyatv 0.18.0
	private const int MDNS_PORT = 5353;

	private readonly IPAddress _address;

	/// <summary>Initializes a new instance of the <see cref="UnicastCompanionDiscovery"/> class.</summary>
	/// <param name="address">The known IPv4 address to query.</param>
	public UnicastCompanionDiscovery (IPAddress address)
		{
		if (address.AddressFamily != AddressFamily.InterNetwork)
			{
			throw new ArgumentException ("Only IPv4 mDNS unicast discovery is supported.", nameof (address));
			}
		_address = address;
		}

	/// <inheritdoc/>
	public async Task<IReadOnlyList<CompanionDiscoveryResult>> ScanAsync (TimeSpan timeout, CancellationToken cancellationToken = default)
		{
		List<byte[]> queries = DnsServiceQueries.CreateServiceQueries (
			new[] { CompanionServiceInfo.SERVICE_TYPE },
			QueryType.Ptr);
		ServiceParser parser = new ServiceParser ();

		using UdpClient client = new UdpClient (AddressFamily.InterNetwork);
		client.Client.Bind (new IPEndPoint (IPAddress.Any, 0));
		using CancellationTokenSource timeoutCts = new CancellationTokenSource (timeout);
		using CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource (cancellationToken, timeoutCts.Token);
		using CancellationTokenRegistration registration = linkedCts.Token.Register (static state => ((UdpClient)state!).Close (), client);

		Task sendTask = SendQueriesAsync (client, new IPEndPoint (_address, MDNS_PORT), queries, timeout, linkedCts.Token);
		Task receiveTask = ReceiveResponsesAsync (client, parser, linkedCts.Token);
		try
			{
			await Task.WhenAll (sendTask, receiveTask).ConfigureAwait (false);
			}
		catch (OperationCanceledException)
			{
			}
		catch (ObjectDisposedException)
			{
			}
		catch (SocketException ex) when (ex.SocketErrorCode is SocketError.OperationAborted or SocketError.Interrupted)
			{
			}

		return parser.Parse ()
			.Where (service => string.Equals (service.Type, CompanionServiceInfo.SERVICE_TYPE, StringComparison.OrdinalIgnoreCase))
			.Select (CompanionServiceInfo.ToDiscoveryResult)
			.ToList ();
		}

	private static async Task SendQueriesAsync (UdpClient client, IPEndPoint endpoint, List<byte[]> queries, TimeSpan timeout, CancellationToken cancellationToken)
		{
		for (int iteration = 0; iteration < Math.Ceiling (timeout.TotalSeconds); iteration++)
			{
			foreach (byte[] query in queries)
				{
				try
					{
					await client.SendAsync (query, query.Length, endpoint).ConfigureAwait (false);
					}
				catch (ObjectDisposedException)
					{
					return;
					}
				catch (SocketException)
					{
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

	private static async Task ReceiveResponsesAsync (UdpClient client, ServiceParser parser, CancellationToken cancellationToken)
		{
		try
			{
			while (!cancellationToken.IsCancellationRequested)
				{
#pragma warning disable CA2016
				UdpReceiveResult response = await client.ReceiveAsync ().ConfigureAwait (false);
#pragma warning restore CA2016
				try
					{
					parser.AddMessage (new DnsMessage ().Unpack (response.Buffer));
					}
				catch (Exception)
					{
					}
				}
			}
		catch (ObjectDisposedException)
			{
			}
		catch (SocketException ex) when (ex.SocketErrorCode is SocketError.OperationAborted or SocketError.Interrupted)
			{
			}
		}
	}
