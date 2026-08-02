using System;
using System.Collections.Generic;

using AppleTvControlLibrary.Crypto;

namespace AppleTvControlLibrary.Connection;

/// <summary>
/// Frame type values.
/// </summary>
// pyatv/protocols/companion/connection.py:21-40 (FrameType)
public enum FrameType
	{
	/// <summary>Unknown frame type. pyatv/protocols/companion/connection.py:24</summary>
	Unknown = 0,
	/// <summary>No-op frame, never encrypted. pyatv/protocols/companion/connection.py:25</summary>
	NoOp = 1,
	/// <summary>Pair-setup, start (M1). pyatv/protocols/companion/connection.py:26</summary>
	PS_Start = 3,
	/// <summary>Pair-setup, next step. pyatv/protocols/companion/connection.py:27</summary>
	PS_Next = 4,
	/// <summary>Pair-verify, start (M1). pyatv/protocols/companion/connection.py:28</summary>
	PV_Start = 5,
	/// <summary>Pair-verify, next step. pyatv/protocols/companion/connection.py:29</summary>
	PV_Next = 6,
	/// <summary>Unencrypted OPACK payload. pyatv/protocols/companion/connection.py:30</summary>
	U_OPACK = 7,
	/// <summary>Encrypted OPACK payload. pyatv/protocols/companion/connection.py:31</summary>
	E_OPACK = 8,
	/// <summary>Plist OPACK payload. pyatv/protocols/companion/connection.py:32</summary>
	P_OPACK = 9,
	/// <summary>Pairing authentication request. pyatv/protocols/companion/connection.py:33</summary>
	PA_Req = 10,
	/// <summary>Pairing authentication response. pyatv/protocols/companion/connection.py:34</summary>
	PA_Rsp = 11,
	/// <summary>Session start request. pyatv/protocols/companion/connection.py:35</summary>
	SessionStartRequest = 16,
	/// <summary>Session start response. pyatv/protocols/companion/connection.py:36</summary>
	SessionStartResponse = 17,
	/// <summary>Session data. pyatv/protocols/companion/connection.py:37</summary>
	SessionData = 18,
	/// <summary>Family identity request. pyatv/protocols/companion/connection.py:38</summary>
	FamilyIdentityRequest = 32,
	/// <summary>Family identity response. pyatv/protocols/companion/connection.py:39</summary>
	FamilyIdentityResponse = 33,
	/// <summary>Family identity update. pyatv/protocols/companion/connection.py:40</summary>
	FamilyIdentityUpdate = 34,
	}

/// <summary>
/// Listener interface for a Companion connection.
/// </summary>
// pyatv/protocols/companion/connection.py:46-50 (CompanionConnectionListener)
public interface ICompanionConnectionListener
	{
	/// <summary>Frame was received from remote device.</summary>
	/// <param name="frameType">The type of the received frame.</param>
	/// <param name="data">The (already decrypted, if applicable) frame payload.</param>
	// pyatv/protocols/companion/connection.py:49
	void FrameReceived (FrameType frameType, byte[] data);
	}

/// <summary>
/// Frame-level protocol handling for a Companion Link connection: header framing,
/// buffering of partial frames and transparent ChaCha20-Poly1305 encryption.
/// </summary>
/// <remarks>
/// This type intentionally has no socket I/O of its own; a caller feeds inbound bytes
/// via <see cref="ReceiveData"/> and consumes outbound bytes via <see cref="BuildFrame"/>,
/// so it can be driven by any transport (Socket, SslStream over sockets not applicable
/// here, etc.) on either target framework.
/// </remarks>
// pyatv/protocols/companion/connection.py:16-17, 53-168 (CompanionConnection)
public class CompanionConnection
	{
	// pyatv/protocols/companion/connection.py:16
	private const int AUTH_TAG_LENGTH = 16;

	// pyatv/protocols/companion/connection.py:17
	private const int HEADER_LENGTH = 4;

	private readonly List<byte> _buffer = new ();

	private Chacha20Cipher? _chacha;

	/// <summary>Gets a value indicating whether encryption has been enabled.</summary>
	public bool IsEncrypted => _chacha is not null;

	/// <summary>Enable encryption with the specified keys.</summary>
	/// <param name="outputKey">The key used to encrypt outgoing data.</param>
	/// <param name="inputKey">The key used to decrypt incoming data.</param>
	// pyatv/protocols/companion/connection.py:90-92 (enable_encryption)
	public void EnableEncryption (byte[] outputKey, byte[] inputKey)
		{
		// pyatv/protocols/companion/connection.py:92 (nonce_length=12)
		_chacha = new Chacha20Cipher (outputKey, inputKey, nonceLength: 12);
		}

	/// <summary>Build a framed (and, if encryption is enabled, encrypted) message ready to send.</summary>
	/// <param name="frameType">The type of frame being sent.</param>
	/// <param name="data">The frame payload.</param>
	/// <returns>The bytes to write to the transport, including the 4-byte header.</returns>
	// pyatv/protocols/companion/connection.py:98-119 (send)
	public byte[] BuildFrame (FrameType frameType, byte[] data)
		{
		int payloadLength = data.Length;

		// pyatv/protocols/companion/connection.py:104-105
		if (_chacha is not null && payloadLength > 0)
			{
			payloadLength += AUTH_TAG_LENGTH;
			}

		// pyatv/protocols/companion/connection.py:106 (1 byte type + 3 byte big-endian length)
		var header = new byte[HEADER_LENGTH];
		header[0] = (byte)frameType;
		header[1] = (byte)(payloadLength >> 16);
		header[2] = (byte)(payloadLength >> 8);
		header[3] = (byte)payloadLength;

		// pyatv/protocols/companion/connection.py:115-117 (AAD is the header, built
		// before encryption, so it already carries the tag-inclusive length)
		if (_chacha is not null && data.Length > 0)
			{
			data = _chacha.Encrypt (data, aad: header);
			}

		var frame = new byte[header.Length + data.Length];
		Buffer.BlockCopy (header, 0, frame, 0, header.Length);
		Buffer.BlockCopy (data, 0, frame, header.Length, data.Length);
		return frame;
		}

	/// <summary>Feed newly received bytes into the reassembly buffer, raising <see cref="FrameReceived"/>
	/// for each complete frame that becomes available.</summary>
	/// <param name="data">The bytes received from the transport.</param>
	// pyatv/protocols/companion/connection.py:126-153 (data_received)
	public void ReceiveData (byte[] data)
		{
		_buffer.AddRange (data);
		System.Diagnostics.Debug.WriteLine ($"[CompanionConnection] ReceiveData: +{data.Length} bytes, buffer now {_buffer.Count} bytes");

		// pyatv/protocols/companion/connection.py:131
		while (_buffer.Count >= HEADER_LENGTH)
			{
			// pyatv/protocols/companion/connection.py:132-134 (3 byte big-endian length)
			int payloadLength = HEADER_LENGTH
				+ (_buffer[1] << 16)
				+ (_buffer[2] << 8)
				+ _buffer[3];

			System.Diagnostics.Debug.WriteLine ($"[CompanionConnection] Frame header: type={_buffer[0]}, payloadLength(incl. header)={payloadLength}, buffered={_buffer.Count}");

			// pyatv/protocols/companion/connection.py:135-141
			if (_buffer.Count < payloadLength)
				{
				System.Diagnostics.Debug.WriteLine ($"[CompanionConnection] Waiting for {payloadLength - _buffer.Count} more bytes to complete frame");
				break;
				}

			var header = new byte[HEADER_LENGTH];
			_buffer.CopyTo (0, header, 0, HEADER_LENGTH);

			int payloadSize = payloadLength - HEADER_LENGTH;
			var payload = new byte[payloadSize];
			_buffer.CopyTo (HEADER_LENGTH, payload, 0, payloadSize);

			// pyatv/protocols/companion/connection.py:145
			_buffer.RemoveRange (0, payloadLength);
			System.Diagnostics.Debug.WriteLine ($"[CompanionConnection] Consumed {payloadLength} bytes, {_buffer.Count} bytes remain buffered (encrypted={_chacha is not null})");

			// pyatv/protocols/companion/connection.py:147-153
			try
				{
				if (_chacha is not null && payload.Length > 0)
					{
					payload = _chacha.Decrypt (payload, aad: header);
					}

				FrameReceived?.Invoke (this, (FrameType)header[0], payload);
				}
			catch (Exception ex)
				{
				// pyatv/protocols/companion/connection.py:152-153 (logged and swallowed;
				// a malformed/undecryptable frame must not tear down the reassembly loop)
				System.Diagnostics.Debug.WriteLine ($"[CompanionConnection] Failed to process frame (type={header[0]}, payloadSize={payloadSize}): {ex}");
				}
			}
		}

	/// <summary>Raised when a complete frame has been received (and decrypted, if applicable).</summary>
	public event FrameReceivedCallback? FrameReceived;
	}

/// <summary>Callback signature for the <see cref="CompanionConnection.FrameReceived"/> event.</summary>
/// <param name="sender">The <see cref="CompanionConnection"/> that raised the event.</param>
/// <param name="frameType">The type of the received frame.</param>
/// <param name="data">The (already decrypted, if applicable) frame payload.</param>
public delegate void FrameReceivedCallback (object sender, FrameType frameType, byte[] data);