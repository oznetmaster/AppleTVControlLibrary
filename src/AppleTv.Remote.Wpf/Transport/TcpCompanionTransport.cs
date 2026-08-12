// Copyright (c) 2026 Neil Colvin. Licensed under the MIT License.
// See LICENSE file in the repository root for full license text.

using System;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

using AppleTvControlLibrary.Connection;
using AppleTvControlLibrary.Protocol;

namespace AppleTvControlLibrary.Remote.Wpf.Transport;

/// <summary>
/// Bridges a real TCP socket to the transport-agnostic <see cref="CompanionConnection"/> /
/// <see cref="CompanionProtocol"/> pair: writes go straight to the <see cref="NetworkStream"/>,
/// and a dedicated background thread reads and feeds bytes back into
/// <see cref="CompanionConnection.ReceiveData"/>.
/// </summary>
/// <remarks>
/// <see cref="CompanionConnection"/>/<see cref="CompanionProtocol"/> are intentionally socket-free
/// by design (see the porting brief's WP3 remarks) so they can be driven by any transport; this
/// class is that transport for the WPF remote app.
/// </remarks>
public sealed class TcpCompanionTransport : IDisposable, IAsyncDisposable
	{
	private readonly TcpClient _client;
	private readonly CompanionConnection _connection;
	private readonly Thread _readThread;
	private volatile bool _disposed;

	private TcpCompanionTransport (TcpClient client, CompanionConnection connection)
		{
		this._client = client;
		this._connection = connection;

		this._readThread = new Thread (this.ReadLoop)
			{
			IsBackground = true,
			Name = "CompanionLink-Read",
			};
		}

	/// <summary>
	/// Connects to a Companion Link device and wires up the given <see cref="CompanionProtocol"/>
	/// so that <see cref="CompanionProtocol.SendOpack"/> (and auth frame sends) write to the
	/// socket, and inbound bytes are fed back into <paramref name="connection"/>.
	/// </summary>
	/// <param name="host">The device's address (typically <c>CompanionDiscoveryResult.Address</c>).</param>
	/// <param name="port">The device's Companion Link port (typically <c>CompanionDiscoveryResult.Port</c>).</param>
	/// <param name="connection">The connection instance to feed inbound frames into.</param>
	/// <param name="protocol">The protocol instance whose outbound frames should be written to the socket.</param>
	/// <returns>A connected <see cref="TcpCompanionTransport"/>.</returns>
	[Obsolete ("Use ConnectAsync instead.")]
	public static TcpCompanionTransport Connect (string host, int port, CompanionConnection connection, CompanionProtocol protocol)
		{
		return ConnectAsync (host, port, connection, protocol).ConfigureAwait (false).GetAwaiter ().GetResult ();
		}

	/// <summary>Asynchronously connects and wires a TCP transport to a Companion protocol.</summary>
	public static async Task<TcpCompanionTransport> ConnectAsync (string host, int port, CompanionConnection connection, CompanionProtocol protocol, CancellationToken cancellationToken = default)
		{
		TcpClient client = new TcpClient ();
		using CancellationTokenRegistration registration = cancellationToken.Register (static state => ((TcpClient)state!).Close (), client);
		try
			{
			await client.ConnectAsync (host, port).ConfigureAwait (false);
			cancellationToken.ThrowIfCancellationRequested ();
			}
		catch
			{
			client.Dispose ();
			throw;
			}

		TcpCompanionTransport transport = new TcpCompanionTransport (client, connection);
		protocol.AsyncSender = transport.SendAsync;
		transport._readThread.Start ();
		return transport;
		}

	private async Task SendAsync (byte[] frame)
		{
		if (this._disposed)
			{
			throw new ObjectDisposedException (nameof (TcpCompanionTransport));
			}

		System.Diagnostics.Debug.WriteLine ($"[TcpCompanionTransport] Sending {frame.Length} bytes");
		await this._client.GetStream ().WriteAsync (frame, 0, frame.Length).ConfigureAwait (false);
		}

	private void ReadLoop ()
		{
		System.Diagnostics.Debug.WriteLine ($"[TcpCompanionTransport] Read loop starting on thread {Environment.CurrentManagedThreadId} ({Thread.CurrentThread.Name})");
		NetworkStream stream = this._client.GetStream ();
		byte[] buffer = new byte[4096];

		try
			{
			while (!this._disposed)
				{
				int read = stream.Read (buffer, 0, buffer.Length);
				if (read == 0)
					{
					System.Diagnostics.Debug.WriteLine ("[TcpCompanionTransport] Remote closed the connection (0-byte read)");
					// pyatv/protocols/companion/connection.py (connection_lost, exc is None) — line 161-167
					// as of pyatv 0.18.0: a 0-byte read is a clean remote close, reported without an exception.
					this._connection.Fault (null);
					return;
					}

				System.Diagnostics.Debug.WriteLine ($"[TcpCompanionTransport] Received {read} bytes");
				byte[] received = new byte[read];
				Array.Copy (buffer, received, read);

				try
					{
					this._connection.ReceiveData (received);
					}
				catch (Exception ex)
					{
					// Frame reassembly/decrypt/dispatch failures must not silently kill the read
					// loop (which would otherwise present as every subsequent exchange timing
					// out with no explanation). Log and keep reading.
					System.Diagnostics.Debug.WriteLine ($"[TcpCompanionTransport] ReceiveData processing failed for a {read}-byte chunk: {ex}");
					}

				}
			}
		catch (Exception ex) when (this._disposed || !this._client.Connected)
			{
			// Expected once Dispose() closes the socket while a read is in progress.
			System.Diagnostics.Debug.WriteLine ($"[TcpCompanionTransport] Read loop stopped (disposed={this._disposed}, connected={this._client.Connected}): {ex.GetType ().Name}: {ex.Message}");
			// pyatv/protocols/companion/connection.py (connection_lost) — line 161-167 as of pyatv 0.18.0:
			// only report as an unexpected fault if this wasn't our own Dispose() closing the socket.
			if (!this._disposed)
				{
				this._connection.Fault (ex);
				}
			}
		catch (Exception ex)
			{
			// Unexpected: the socket failed while still supposedly connected and not disposed.
			// Previously this exception was invisible; surface it so a broken read doesn't
			// masquerade as a downstream protocol timeout.
			System.Diagnostics.Debug.WriteLine ($"[TcpCompanionTransport] Read loop failed unexpectedly: {ex}");
			this._connection.Fault (ex);
			}
		finally
			{
			System.Diagnostics.Debug.WriteLine ($"[TcpCompanionTransport] Read loop exiting on thread {Environment.CurrentManagedThreadId} (disposed={this._disposed})");
			}
		}

	/// <inheritdoc/>
	public void Dispose ()
		{
		if (this._disposed)
			{
			return;
			}

		this._disposed = true;

		try
			{
			this._client.Close ();
			}
		catch (Exception)
			{
			// Best-effort close; the read thread will observe the closed socket and exit.
			}
		}

	/// <summary>Asynchronously closes the socket and waits for the read loop to stop.</summary>
	public async ValueTask DisposeAsync ()
		{
		Dispose ();
		await Task.Run (() => this._readThread.Join ()).ConfigureAwait (false);
		}
	}
