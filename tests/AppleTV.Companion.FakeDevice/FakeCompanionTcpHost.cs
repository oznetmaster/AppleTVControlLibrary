// Copyright (c) 2026 Neil Colvin. Licensed under the MIT License.
// See LICENSE file in the repository root for full license text.

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;

using AppleTvControlLibrary.Connection;
using AppleTvControlLibrary.Opack;

namespace AppleTvControlLibrary.FakeDevice;

/// <summary>
/// A real, socket-backed fake Companion Apple TV: listens on a loopback TCP port, accepts a
/// single client connection, and bridges the raw bytes into the existing in-memory
/// <see cref="FakeCompanionDevice"/> (pairing/auth) and <see cref="FakeCompanionOpackDevice"/>
/// (OPACK/API) fakes exactly as a real device's Companion Link listener would.
/// </summary>
/// <remarks>
/// Unlike <see cref="FakeCompanionDevice"/>/<see cref="FakeCompanionOpackDevice"/> themselves
/// (which operate on already-decoded frames so they can be driven synchronously, in-process),
/// this type exists specifically to exercise the real socket/async path -- <c>TcpClient</c>,
/// background read threads, real framing over the wire -- the same code path the WPF remote
/// app's <c>TcpCompanionTransport</c> uses, so client-side bugs in that path (timing, partial
/// reads, cancellation/dispose races) can be caught by tests instead of only surfacing against
/// real hardware.
/// </remarks>
public sealed class FakeCompanionTcpHost : IDisposable
	{
	private readonly TcpListener _listener;
	private readonly FakeCompanionDevice _authDevice;
	private readonly FakeCompanionOpackDevice _opackDevice;
	private readonly CompanionConnection _serverConnection;
	private Thread? _acceptThread;
	private TcpClient? _client;
	private volatile bool _disposed;

	/// <summary>Initializes a new instance of the <see cref="FakeCompanionTcpHost"/> class and starts listening.</summary>
	/// <param name="pin">The PIN code required to complete pair-setup. Defaults to <see cref="FakeCompanionDevice.PIN_CODE"/>.</param>
	public FakeCompanionTcpHost (int pin = FakeCompanionDevice.PIN_CODE)
		{
		_authDevice = new FakeCompanionDevice (pin: pin);
		_opackDevice = new FakeCompanionOpackDevice ();
		_serverConnection = new CompanionConnection ();

		_listener = new TcpListener (IPAddress.Loopback, 0);
		_listener.Start ();

		_serverConnection.FrameReceived += OnFrameReceived;
		}

	/// <summary>Gets the loopback port the host is listening on.</summary>
	public int Port => ((IPEndPoint)_listener.LocalEndpoint).Port;

	/// <summary>Gets the underlying pairing/auth fake device, for assertions.</summary>
	public FakeCompanionDevice AuthDevice => _authDevice;

	/// <summary>Gets the underlying OPACK/API fake device, for assertions.</summary>
	public FakeCompanionOpackDevice OpackDevice => _opackDevice;

	/// <summary>
	/// Accepts a single incoming connection and starts a background read loop bridging bytes
	/// into the fake devices. Call once, after a client has begun connecting (or is about to).
	/// </summary>
	public void AcceptOne ()
		{
		_acceptThread = new Thread (AcceptLoop)
			{
			IsBackground = true,
			Name = "FakeCompanionTcpHost-Accept",
			};
		_acceptThread.Start ();
		}

	private void AcceptLoop ()
		{
		TcpClient client;
		try
			{
			client = _listener.AcceptTcpClient ();
			}
		catch (Exception) when (_disposed)
			{
			return;
			}
		catch (Exception)
			{
			return;
			}

		_client = client;
		NetworkStream stream = client.GetStream ();
		byte[] buffer = new byte[4096];

		try
			{
			while (true)
				{
				int read = stream.Read (buffer, 0, buffer.Length);
				if (read == 0)
					{
					return;
					}

				byte[] received = new byte[read];
				Array.Copy (buffer, received, read);
				_serverConnection.ReceiveData (received);
				}
			}
		catch (Exception)
			{
			// Connection closed by the client, or the listener/socket was disposed to stop the
			// host -- either way, there's nothing further to bridge.
			}
		}

	private void OnFrameReceived (object sender, FrameType frameType, byte[] data)
		{
		bool isAuth = frameType is FrameType.PS_Start or FrameType.PS_Next or FrameType.PV_Start or FrameType.PV_Next;

		if (isAuth)
			{
			HandleAuthFrame (frameType, data);
			}
		else
			{
			HandleOpackFrame (frameType, data);
			}
		}

	private void HandleAuthFrame (FrameType frameType, byte[] data)
		{
		object? unpacked = AppleTvControlLibrary.Opack.Opack.Unpack (data, out _);
		if (unpacked is not Dictionary<object, object?> request || request["_pd"] is not byte[] pairingData)
			{
			return;
			}

		(FrameType responseFrameType, byte[] responseTlv) = _authDevice.HandleAuthFrame (frameType, pairingData);

		Dictionary<string, object?> response = new () { { "_pd", responseTlv } };
		if (request.TryGetValue ("_x", out object? xid))
			{
			response["_x"] = xid;
			}

		byte[] responseFrame = _serverConnection.BuildFrame (responseFrameType, AppleTvControlLibrary.Opack.Opack.Pack (response));
		WriteToClient (responseFrame);

		// pyatv/protocols/companion/server_auth.py (handle_auth_frame) completes pair-verify once
		// the client's M3 proof has been validated; mirror the client side's EnableEncryption call
		// (AppleTvCompanionSession.PairVerifyAsync) so the server can decrypt/encrypt subsequent
		// E_OPACK frames instead of only ever handling the plaintext auth handshake.
		if (_authDevice.IsEncrypted && !_serverConnection.IsEncrypted)
			{
			_serverConnection.EnableEncryption (_authDevice.ServerOutputKey!, _authDevice.ServerInputKey!);
			}
		}

	private void HandleOpackFrame (FrameType frameType, byte[] data)
		{
		object? unpacked = AppleTvControlLibrary.Opack.Opack.Unpack (data, out _);
		if (unpacked is not Dictionary<object, object?> request)
			{
			return;
			}

		Dictionary<object, object?>? response = _opackDevice.HandleOpackFrame (request);
		if (response is null)
			{
			return;
			}

		byte[] responseFrame = _serverConnection.BuildFrame (frameType, AppleTvControlLibrary.Opack.Opack.Pack (response));
		WriteToClient (responseFrame);
		}

	private void WriteToClient (byte[] frame)
		{
		TcpClient? client = _client;
		if (client is null || !client.Connected)
			{
			return;
			}

		NetworkStream stream = client.GetStream ();
		lock (stream)
			{
			stream.Write (frame, 0, frame.Length);
			}
		}

	/// <inheritdoc/>
	public void Dispose ()
		{
		_disposed = true;
		try
			{
			_listener.Stop ();
			}
		catch (Exception)
			{
			}

		try
			{
			_client?.Dispose ();
			}
		catch (Exception)
			{
			}
		}
	}
