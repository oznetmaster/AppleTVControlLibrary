// Copyright (c) 2026 Neil Colvin. Licensed under the MIT License.
// See LICENSE file in the repository root for full license text.

using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace AppleTvControlLibrary.Mrp.AirPlay.Http;

/// <summary>
/// A TCP-backed HTTP/RTSP request/response connection used for the AirPlay 2 control channel.
/// Supports optional send/receive processors so HAP encryption can be layered on transparently
/// once pair-verify has completed.
/// </summary>
/// <remarks>
/// Only the client-side subset needed to drive AirPlay 2 session setup is ported; the
/// server-side pieces of pyatv's <c>support/http.py</c> (used for local file serving) are out
/// of scope here.
/// </remarks>
// pyatv/support/http.py (HttpConnection) — line 326-420 as of pyatv 0.18.0
public sealed class HttpConnection : IDisposable
	{
	private sealed class PendingRequest : IDisposable
		{
		public readonly SemaphoreSlim Signal = new (0, 1);
		public HttpResponse? Response;
		public bool ConnectionClosed;

		public void Dispose ()
			{
			Signal.Dispose ();
			}
		}

	private readonly TcpClient _client;
	private readonly NetworkStream _stream;
	private readonly LinkedList<PendingRequest> _requests = new ();
	private readonly object _requestsLock = new ();
	private readonly Thread _readThread;
	private readonly List<byte> _buffer = [];
	private volatile bool _disposed;

	private HttpConnection (TcpClient client)
		{
		_client = client;
		_stream = client.GetStream ();
		_readThread = new Thread (ReadLoop)
			{
			IsBackground = true,
			Name = "AirPlay-Http-Read",
			};
		}

	/// <summary>Gets or sets the processor applied to data as it is received (e.g. HAP decryption).</summary>
	// pyatv/support/http.py (receive_processor) — line 337-339 as of pyatv 0.18.0
	public Func<byte[], byte[]>? ReceiveProcessor { get; set; }

	/// <summary>Gets or sets the processor applied to data before it is sent (e.g. HAP encryption).</summary>
	// pyatv/support/http.py (send_processor) — line 340-342 as of pyatv 0.18.0
	public Func<byte[], byte[]>? SendProcessor { get; set; }

	/// <summary>Gets the local IP address of the connection.</summary>
	// pyatv/support/http.py (local_ip) — line 347-351 as of pyatv 0.18.0
	public string LocalIp { get; private set; } = string.Empty;

	/// <summary>Gets the remote IP address of the connection.</summary>
	// pyatv/support/http.py (remote_ip) — line 353-357 as of pyatv 0.18.0
	public string RemoteIp { get; private set; } = string.Empty;

	/// <summary>Open a new connection to a remote host.</summary>
	/// <param name="address">The remote address.</param>
	/// <param name="port">The remote port.</param>
	/// <param name="cancellationToken">A token used to cancel the connection attempt.</param>
	/// <returns>A connected <see cref="HttpConnection"/>.</returns>
	// pyatv/support/http.py (http_connect) — line 655-659 as of pyatv 0.18.0
	public static async Task<HttpConnection> ConnectAsync (string address, int port, CancellationToken cancellationToken = default)
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

		var connection = new HttpConnection (client);
		var localEndpoint = (System.Net.IPEndPoint)client.Client.LocalEndPoint!;
		var remoteEndpoint = (System.Net.IPEndPoint)client.Client.RemoteEndPoint!;
		connection.LocalIp = localEndpoint.Address.ToString ();
		connection.RemoteIp = remoteEndpoint.Address.ToString ();
		connection._readThread.Start ();
		return connection;
		}

	/// <summary>Send a GET request and wait for the response.</summary>
	/// <param name="path">The request path.</param>
	/// <param name="allowError">If <see langword="true"/>, non-2xx responses are returned rather than throwing.</param>
	/// <param name="cancellationToken">A token used to cancel the exchange.</param>
	// pyatv/support/http.py (HttpConnection.get) — line 419-421 as of pyatv 0.18.0
	public Task<HttpResponse> GetAsync (string path, bool allowError = false, CancellationToken cancellationToken = default) =>
		SendAndReceiveAsync ("GET", path, allowError: allowError, cancellationToken: cancellationToken);

	/// <summary>Send a POST request and wait for the response.</summary>
	/// <param name="path">The request path.</param>
	/// <param name="headers">Additional request headers.</param>
	/// <param name="body">The request body.</param>
	/// <param name="allowError">If <see langword="true"/>, non-2xx responses are returned rather than throwing.</param>
	/// <param name="cancellationToken">A token used to cancel the exchange.</param>
	// pyatv/support/http.py (HttpConnection.post) — line 423-433 as of pyatv 0.18.0
	public Task<HttpResponse> PostAsync (
		string path,
		IReadOnlyDictionary<string, string>? headers = null,
		byte[]? body = null,
		bool allowError = false,
		CancellationToken cancellationToken = default) =>
		SendAndReceiveAsync ("POST", path, headers: headers, body: body, allowError: allowError, cancellationToken: cancellationToken);

	/// <summary>Send a request of the given method and wait for the matching response.</summary>
	/// <param name="method">The request method.</param>
	/// <param name="uri">The request path/URI.</param>
	/// <param name="protocol">The protocol/version string, e.g. "HTTP/1.1" or "RTSP/1.0".</param>
	/// <param name="userAgent">The value of the User-Agent header.</param>
	/// <param name="contentType">An optional Content-Type header value.</param>
	/// <param name="headers">Additional request headers.</param>
	/// <param name="body">An optional request body.</param>
	/// <param name="allowError">If <see langword="true"/>, non-2xx responses are returned rather than throwing.</param>
	/// <param name="timeout">How long to wait for a response before failing.</param>
	/// <param name="cancellationToken">A token used to cancel the exchange.</param>
	/// <returns>The response to the request.</returns>
	// pyatv/support/http.py (HttpConnection.send_and_receive) — line 435-483 as of pyatv 0.18.0
	public async Task<HttpResponse> SendAndReceiveAsync (
		string method,
		string uri,
		string protocol = "HTTP/1.1",
		string userAgent = "AppleTvControlLibrary",
		string? contentType = null,
		IReadOnlyDictionary<string, string>? headers = null,
		byte[]? body = null,
		bool allowError = false,
		TimeSpan? timeout = null,
		CancellationToken cancellationToken = default)
		{
		byte[] output = HttpMessages.FormatMessage (method, uri, protocol, userAgent, contentType, headers, body);

		if (_disposed)
			{
			throw new InvalidOperationException ("not connected to remote");
			}

		byte[] toSend = SendProcessor is null ? output : SendProcessor (output);

		var pending = new PendingRequest ();
		LinkedListNode<PendingRequest> node;
		lock (_requestsLock)
			{
			node = _requests.AddFirst (pending);
			}

		try
			{
			// The ReadOnlyMemory<byte> overload of Stream.WriteAsync is unavailable on net472,
			// and this project has exactly one code path across both TFMs (no #if in protocol
			// code), so the byte[]/offset/count overload is used deliberately here.
#pragma warning disable CA1835
			await _stream.WriteAsync (toSend, 0, toSend.Length, cancellationToken).ConfigureAwait (false);
#pragma warning restore CA1835

			using var timeoutCts = new CancellationTokenSource (timeout ?? TimeSpan.FromSeconds (10));
			using CancellationTokenSource linkedCts = CancellationTokenSource.CreateLinkedTokenSource (cancellationToken, timeoutCts.Token);
			try
				{
				await pending.Signal.WaitAsync (linkedCts.Token).ConfigureAwait (false);
				}
			catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
				{
				throw new TimeoutException ($"no response to {method} {uri} ({protocol})");
				}

			if (pending.ConnectionClosed)
				{
				throw new InvalidOperationException ("connection was lost");
				}

			HttpResponse response = pending.Response ?? throw new InvalidOperationException ("did not get a response");

			if (response.Code == 403)
				{
				throw new UnauthorizedAccessException ("not authenticated");
				}

			if (response.Code == 401)
				{
				if (allowError)
					{
					return response;
					}

				throw new UnauthorizedAccessException ("not authenticated");
				}

			if ((response.Code >= 200 && response.Code < 300) || allowError)
				{
				return response;
				}

			throw new InvalidOperationException ($"{protocol} method {method} failed with code {response.Code}: {response.Message}");
			}
		finally
			{
			// DispatchResponse (or HandleConnectionLost) may already have removed this node from
			// the list when the response/closure was delivered; only remove it here if it is still
			// attached, otherwise LinkedList<T>.Remove throws "node does not belong to current list".
			lock (_requestsLock)
				{
				if (node.List is not null)
					{
					_requests.Remove (node);
					}
				}

			pending.Dispose ();
			}
		}

	// pyatv/support/http.py (HttpConnection.data_received) — line 373-388 as of pyatv 0.18.0
	private void ReadLoop ()
		{
		byte[] readBuffer = new byte[4096];
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
				byte[] processed = ReceiveProcessor is null ? received : ReceiveProcessor (received);

				_buffer.AddRange (processed);

				while (_buffer.Count > 0)
					{
					byte[] bufferArray = [.. _buffer];
					if (!HttpMessages.TryParseResponse (bufferArray, out HttpResponse? parsed, out byte[] rest))
						{
						break;
						}

					_buffer.Clear ();
					_buffer.AddRange (rest);

					DispatchResponse (parsed!);
					}
				}
			}
		catch
			{
			// pyatv/support/http.py — connection_lost handles the read failing; treated the
			// same way here regardless of whether it was caused by Dispose() or a real drop.
			}
		finally
			{
			HandleConnectionLost ();
			}
		}

	// pyatv/support/http.py (HttpConnection.data_received) — line 380-388 as of pyatv 0.18.0: dispatch
	// to the oldest outstanding request, mirroring pyatv's use of a deque and .pop().
	private void DispatchResponse (HttpResponse response)
		{
		PendingRequest? target = null;
		lock (_requestsLock)
			{
			if (_requests.Last is not null)
				{
				target = _requests.Last.Value;
				_requests.RemoveLast ();
				}
			}

		if (target is not null)
			{
			target.Response = response;
			target.Signal.Release ();
			}
		}

	// pyatv/support/http.py (HttpConnection.connection_lost) — line 411-417 as of pyatv 0.18.0
	private void HandleConnectionLost ()
		{
		lock (_requestsLock)
			{
			foreach (PendingRequest pending in _requests)
				{
				pending.ConnectionClosed = true;
				pending.Signal.Release ();
				}

			_requests.Clear ();
			}
		}

	/// <summary>Closes the underlying socket and stops the read loop.</summary>
	// pyatv/support/http.py (HttpConnection.close) — line 366-371 as of pyatv 0.18.0
	public void Dispose ()
		{
		if (_disposed)
			{
			return;
			}

		_disposed = true;
		_client.Close ();
		HandleConnectionLost ();

		if (_readThread.IsAlive && Environment.CurrentManagedThreadId != _readThread.ManagedThreadId)
			{
			_readThread.Join (TimeSpan.FromSeconds (2));
			}
		}
	}
