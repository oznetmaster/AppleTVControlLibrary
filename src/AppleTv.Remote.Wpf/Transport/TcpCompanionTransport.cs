// Copyright (c) 2026 Neil Colvin. Licensed under the MIT License.
// See LICENSE file in the repository root for full license text.

using System;
using System.Net.Sockets;
using System.Threading;

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
public sealed class TcpCompanionTransport : IDisposable
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
	public static TcpCompanionTransport Connect (string host, int port, CompanionConnection connection, CompanionProtocol protocol)
		{
		TcpClient client = new TcpClient ();
		client.Connect (host, port);

		TcpCompanionTransport transport = new TcpCompanionTransport (client, connection);
		protocol.Sender = transport.Send;
		transport._readThread.Start ();
		return transport;
		}

	private void Send (byte[] frame)
		{
		if (this._disposed)
			{
			throw new ObjectDisposedException (nameof (TcpCompanionTransport));
			}

		System.Diagnostics.Debug.WriteLine ($"[TcpCompanionTransport] Sending {frame.Length} bytes");
		NetworkStream stream = this._client.GetStream ();
		lock (stream)
			{
			stream.Write (frame, 0, frame.Length);
			}
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
			}
		catch (Exception ex)
			{
			// Unexpected: the socket failed while still supposedly connected and not disposed.
			// Previously this exception was invisible; surface it so a broken read doesn't
			// masquerade as a downstream protocol timeout.
			System.Diagnostics.Debug.WriteLine ($"[TcpCompanionTransport] Read loop failed unexpectedly: {ex}");
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
	}
