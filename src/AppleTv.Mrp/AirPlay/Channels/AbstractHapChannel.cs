// Copyright (c) 2026 Neil Colvin. Licensed under the MIT License.
// See LICENSE file in the repository root for full license text.

using System;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

using AppleTvControlLibrary.Mrp.AirPlay.Auth;

namespace AppleTvControlLibrary.Mrp.AirPlay.Channels;

/// <summary>
/// Base class for a TCP connection using HAP encryption and length-based segmenting, as used by
/// the AirPlay 2 event and data-stream channels.
/// </summary>
/// <remarks>
/// This is the transport-level piece; message-specific encode/decode and dispatch belong to
/// subclasses (see <see cref="DataStreamChannel"/>).
/// </remarks>
// pyatv/auth/hap_channel.py (AbstractHAPChannel) — line 16-71 as of pyatv 0.18.0
public abstract class AbstractHapChannel : IDisposable
	{
	private readonly HapSession _session = new ();

	// pyatv's asyncio transport serializes all writes onto a single event loop; this port has
	// no such guarantee since Send() is called both from the background read thread (replying to
	// "sync" frames) and from the async protocol send path. Without this lock, two concurrent
	// Send() calls can race on HapSession's outgoing nonce counter, encrypting two different
	// payloads with the same nonce — which breaks ChaCha20-Poly1305 and gets the connection
	// silently dropped by the device.
	private readonly object _sendLock = new ();
	private TcpClient? _client;
	private NetworkStream? _stream;
	private Thread? _readThread;
	private volatile bool _disposed;

	/// <summary>Initializes a new instance of the <see cref="AbstractHapChannel"/> class.</summary>
	/// <param name="outputKey">The key used to encrypt outgoing data.</param>
	/// <param name="inputKey">The key used to decrypt incoming data.</param>
	// pyatv/auth/hap_channel.py (__init__) — line 19-24 as of pyatv 0.18.0
	protected AbstractHapChannel (byte[] outputKey, byte[] inputKey) => _session.Enable (outputKey, inputKey);

	/// <summary>Gets the accumulated, decrypted-but-not-yet-consumed receive buffer.</summary>
	/// <remarks>Subclasses drain this in <see cref="HandleReceived"/> as complete messages become available.</remarks>
	protected System.Collections.Generic.List<byte> Buffer { get; } = [];

	/// <summary>Connect to the remote endpoint and start the background read loop.</summary>
	/// <param name="address">The remote address.</param>
	/// <param name="port">The remote port.</param>
	/// <param name="cancellationToken">A token used to cancel the connection attempt.</param>
	// pyatv/auth/hap_channel.py (setup_channel) — line 79-96 as of pyatv 0.18.0
	public async Task ConnectAsync (string address, int port, CancellationToken cancellationToken = default)
		{
		var client = new TcpClient ();
		using CancellationTokenRegistration registration = cancellationToken.Register (static state => ((TcpClient)state!).Close (), client);
		try
			{
			await client.ConnectAsync (address, port).ConfigureAwait (false);
			cancellationToken.ThrowIfCancellationRequested ();
			}
		catch
			{
			client.Dispose ();
			throw;
			}

		_client = client;
		_stream = client.GetStream ();
		_readThread = new Thread (ReadLoop)
			{
			IsBackground = true,
			Name = GetType ().Name + "-Read",
			};
		_readThread.Start ();
		}

	/// <summary>Send raw (pre-encryption) data to the remote device.</summary>
	/// <param name="data">The plaintext to encrypt and send.</param>
	// pyatv/auth/hap_channel.py (send) — line 65-70 as of pyatv 0.18.0
	protected internal void Send (byte[] data)
		{
		if (_stream is null)
			{
			throw new InvalidOperationException ("not connected");
			}

		lock (_sendLock)
			{
			byte[] encrypted = _session.Encrypt (data);
			_stream.Write (encrypted, 0, encrypted.Length);
			}
		}

	/// <summary>Handle data that has been decrypted and appended to <see cref="Buffer"/>.</summary>
	// pyatv/auth/hap_channel.py (handle_received) — abstract method, line 58-59 as of pyatv 0.18.0
	protected abstract void HandleReceived ();

	/// <summary>Called when the underlying connection is dropped, cleanly or otherwise.</summary>
	/// <param name="exception">The exception that caused the drop, or <see langword="null"/> for a clean close.</param>
	// pyatv/auth/hap_channel.py (connection_lost) — line 73-75 as of pyatv 0.18.0
	protected virtual void OnConnectionLost (Exception? exception)
		{
		}

	// pyatv/auth/hap_channel.py (data_received) — line 46-52 as of pyatv 0.18.0
	private void ReadLoop ()
		{
		if (_stream is null)
			{
			return;
			}

		byte[] readBuffer = new byte[4096];
		Exception? fault = null;
		try
			{
			while (!_disposed)
				{
				int read = _stream.Read (readBuffer, 0, readBuffer.Length);
				if (read == 0)
					{
					break;
					}

				byte[] received = new byte[read];
				Array.Copy (readBuffer, received, read);
				byte[] decrypted = _session.Decrypt (received);
				if (decrypted.Length > 0)
					{
					Buffer.AddRange (decrypted);
					HandleReceived ();
					}
				}
			}
		catch (Exception ex) when (!_disposed)
			{
			fault = ex;
			}
		catch
			{
			// Expected once Dispose() closes the socket while a read is in progress.
			}
		finally
			{
			OnConnectionLost (_disposed ? null : fault);
			}
		}

	/// <summary>Closes the channel.</summary>
	// pyatv/auth/hap_channel.py (close) — line 35-38 as of pyatv 0.18.0
	public void Dispose ()
		{
		if (_disposed)
			{
			return;
			}

		_disposed = true;
		_client?.Close ();

		if (_readThread is { IsAlive: true } && Environment.CurrentManagedThreadId != _readThread.ManagedThreadId)
			{
			_ = _readThread.Join (TimeSpan.FromSeconds (2));
			}

		GC.SuppressFinalize (this);
		}
	}
