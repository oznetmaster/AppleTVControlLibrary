// Copyright (c) 2026 Neil Colvin. Licensed under the MIT License.
// See LICENSE file in the repository root for full license text.

using System;
using System.Threading.Tasks;

using Google.Protobuf;

using AppleTvControlLibrary.Mrp.AirPlay.Channels;
using AppleTvControlLibrary.Mrp.Connection;
using AppleTvControlLibrary.Mrp.Protobuf;
using AppleTvControlLibrary.Mrp.Protocol;

namespace AppleTvControlLibrary.Mrp.AirPlay;

/// <summary>
/// MRP connection implemented as a channel/stream tunneled over an AirPlay 2 data-stream
/// channel, rather than a raw TCP socket. This is the adapter that lets <see cref="Mrp.Protocol.MrpProtocol"/>
/// be driven by an already-established <see cref="Ap2Session"/> instead of <see cref="Transport.TcpMrpTransport"/>.
/// </summary>
// pyatv/protocols/airplay/mrp_connection.py (AirPlayMrpConnection) — line 16-75 as of pyatv 0.18.0
public sealed class AirPlayMrpConnection : IMrpFrameConnection, IDataStreamListener, IDisposable
	{
	private readonly Ap2Session _session;
	private DataStreamChannel? _dataChannel;

	/// <summary>Initializes a new instance of the <see cref="AirPlayMrpConnection"/> class.</summary>
	/// <param name="session">An <see cref="Ap2Session"/> for which <see cref="Ap2Session.SetupRemoteControlAsync"/> has already completed.</param>
	// pyatv/protocols/airplay/mrp_connection.py (__init__) — line 19-24 as of pyatv 0.18.0
	public AirPlayMrpConnection (Ap2Session session)
		{
		_session = session;
		}

	/// <inheritdoc/>
	public IMrpConnectionListener? Listener { get; set; }

	/// <summary>Raised when the underlying AirPlay data channel is dropped.</summary>
	// pyatv/protocols/airplay/mrp_connection.py (device_listener / connection_lost) — line 20-31 as of pyatv 0.18.0
	public event Action<Exception?>? ConnectionLost;

	/// <summary>Attach this adapter to the session's already-established data channel.</summary>
	// pyatv/protocols/airplay/mrp_connection.py (connect) — line 33-38 as of pyatv 0.18.0
	public void Connect ()
		{
		_dataChannel = _session.DataChannel ?? throw new InvalidOperationException ("remote control channel not connected");
		_dataChannel.Listener = this;
		}

	/// <summary>Gets or sets an asynchronous callback for transmitting a fully-built frame.</summary>
	/// <remarks>
	/// Set by <see cref="Mrp.Protocol.MrpProtocol.AsyncSender"/>; this adapter re-parses the frame
	/// (produced by <see cref="BuildMessage"/> as a byte-identity passthrough) back into a
	/// <see cref="ProtocolMessage"/> so it can be handed to <see cref="DataStreamChannel.SendProtobuf"/>,
	/// which does its own AirPlay-specific framing rather than the variant-length framing used by
	/// direct TCP MRP.
	/// </remarks>
	public Task SendAsync (byte[] frame)
		{
		if (_dataChannel is null)
			{
			throw new InvalidOperationException ("not connected");
			}

		var message = ProtocolMessage.Parser.WithExtensionRegistry (MrpExtensions.Registry).ParseFrom (frame);
		Send (message);
		return Task.CompletedTask;
		}

	// pyatv/protocols/airplay/mrp_connection.py (send) — line 55-59 as of pyatv 0.18.0
	private void Send (ProtocolMessage message)
		{
		if (_dataChannel is null)
			{
			throw new InvalidOperationException ("not connected");
			}

		_dataChannel.SendProtobuf (message);
		}

	/// <inheritdoc/>
	// pyatv/protocols/airplay/mrp_connection.py (enable_encryption) — line 40-41 as of pyatv 0.18.0: no-op,
	// since the AirPlay data channel is already HAP-encrypted end-to-end.
	public void EnableEncryption (byte[] outputKey, byte[] inputKey)
		{
		}

	/// <inheritdoc/>
	/// <remarks>
	/// The AirPlay data channel does its own framing (see <see cref="DataStreamChannel.SendProtobuf"/>),
	/// so this is an identity passthrough rather than the variant-length prefixing used by direct TCP
	/// MRP; the actual send happens in <see cref="SendAsync"/>.
	/// </remarks>
	public byte[] BuildMessage (byte[] data) => data;

	/// <summary>Handle an incoming protobuf message tunneled over the data channel.</summary>
	/// <param name="message">The decoded message.</param>
	// pyatv/protocols/airplay/mrp_connection.py (handle_protobuf) — line 61-65 as of pyatv 0.18.0
	public void HandleProtobuf (ProtocolMessage message) => Listener?.MessageReceived (message.ToByteArray ());

	/// <summary>Device connection was dropped.</summary>
	/// <param name="exception">The exception that caused the drop, or <see langword="null"/> for a clean close.</param>
	// pyatv/protocols/airplay/mrp_connection.py (handle_connection_lost) — line 67-75 as of pyatv 0.18.0
	public void HandleConnectionLost (Exception? exception) => ConnectionLost?.Invoke (exception);

	/// <summary>Closes the underlying AirPlay session.</summary>
	// pyatv/protocols/airplay/mrp_connection.py (close) — line 47-53 as of pyatv 0.18.0
	public void Dispose () => _session.Dispose ();
	}
