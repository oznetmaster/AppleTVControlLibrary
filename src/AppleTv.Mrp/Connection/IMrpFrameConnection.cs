// Copyright (c) 2026 Neil Colvin. Licensed under the MIT License.
// See LICENSE file in the repository root for full license text.

namespace AppleTvControlLibrary.Mrp.Connection;

/// <summary>
/// Listener interface for an MRP connection.
/// </summary>
// pyatv/protocols/mrp/connection.py (AbstractMrpConnection) — line 16-37 as of pyatv 0.18.0
public interface IMrpConnectionListener
	{
	/// <summary>A complete, decrypted message was received from the remote device.</summary>
	/// <param name="data">The serialized protobuf message bytes.</param>
	// pyatv/protocols/mrp/connection.py (_handle_message) — line 163-172 as of pyatv 0.18.0
	void MessageReceived (byte[] data);
	}

/// <summary>
/// Abstraction over the piece of <see cref="MrpProtocol"/> that turns a serialized protobuf
/// payload into whatever bytes actually need to be transmitted, and vice versa. This is what
/// lets <see cref="MrpProtocol"/> be driven by an AirPlay-tunneled connection (HAP-channel
/// framing/encryption) or, historically, a raw-TCP connection.
/// </summary>
/// <remarks>
/// Mirrors pyatv's <c>AbstractMrpConnection</c> (pyatv/protocols/mrp/connection.py — line 16-37
/// as of pyatv 0.18.0), trimmed to the members <see cref="MrpProtocol"/> actually depends on;
/// pyatv's <c>connect</c>/<c>connected</c>/<c>close</c> are transport-lifecycle concerns owned
/// by the transport classes in this port, not by this interface.
/// </remarks>
public interface IMrpFrameConnection
	{
	/// <summary>Gets or sets the listener notified when a complete message has been received and decoded.</summary>
	IMrpConnectionListener? Listener { get; set; }

	/// <summary>Enable encryption with the specified keys.</summary>
	/// <param name="outputKey">The key used to encrypt outgoing data.</param>
	/// <param name="inputKey">The key used to decrypt incoming data.</param>
	// pyatv/protocols/mrp/connection.py (enable_encryption) — line 83-85 as of pyatv 0.18.0
	void EnableEncryption (byte[] outputKey, byte[] inputKey);

	/// <summary>Build a message ready to send from a serialized protobuf payload.</summary>
	/// <param name="data">The serialized protobuf message payload.</param>
	/// <returns>The bytes to write to the transport.</returns>
	// pyatv/protocols/mrp/connection.py (send) — line 116-124 as of pyatv 0.18.0
	byte[] BuildMessage (byte[] data);
	}
