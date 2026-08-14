// Copyright (c) 2026 Neil Colvin. Licensed under the MIT License.
// See LICENSE file in the repository root for full license text.

using System;
using System.Collections.Generic;

using Google.Protobuf;

using AppleTvControlLibrary.Mrp.Protobuf;
using AppleTvControlLibrary.Mrp.Support;

namespace AppleTvControlLibrary.Mrp.AirPlay.Channels;

/// <summary>
/// Listener interface for <see cref="DataStreamChannel"/>.
/// </summary>
// pyatv/protocols/airplay/channels.py (DataStreamListener) — line 96-103 as of pyatv 0.18.0
public interface IDataStreamListener
	{
	/// <summary>Handle an incoming protobuf message tunneled over the data channel.</summary>
	/// <param name="message">The decoded message.</param>
	// pyatv/protocols/airplay/channels.py (DataStreamListener.handle_protobuf) — line 99-100 as of pyatv 0.18.0
	void HandleProtobuf (ProtocolMessage message);

	/// <summary>Device connection was dropped.</summary>
	/// <param name="exception">The exception that caused the drop, or <see langword="null"/> for a clean close.</param>
	// pyatv/protocols/airplay/channels.py (DataStreamListener.handle_connection_lost) — line 102-103 as of pyatv 0.18.0
	void HandleConnectionLost (Exception? exception);
	}

/// <summary>
/// The AirPlay 2 data-stream channel: a HAP-encrypted TCP connection that tunnels MRP protobuf
/// messages wrapped in a fixed binary header plus a binary-plist envelope.
/// </summary>
// pyatv/protocols/airplay/channels.py (DataStreamChannel, BaseDataStreamChannel) — line 128-280 as of pyatv 0.18.0
public sealed class DataStreamChannel : AbstractHapChannel
	{
	// pyatv/protocols/airplay/channels.py (DATA_HEADER_PADDING) — line 26 as of pyatv 0.18.0
	private const int DataHeaderPadding = 0x00000000;

	// pyatv/protocols/airplay/channels.py (DataHeader) — line 28-30 as of pyatv 0.18.0: big-endian
	// uint32 size, 12-byte message_type, 4-byte command, uint64 seqno, uint32 padding.
	private const int HeaderLength = 4 + 12 + 4 + 8 + 4;

	private readonly Random _random = new ();
	private ulong _sendSeqNo;

	/// <summary>Initializes a new instance of the <see cref="DataStreamChannel"/> class.</summary>
	/// <param name="outputKey">The key used to encrypt outgoing data.</param>
	/// <param name="inputKey">The key used to decrypt incoming data.</param>
	// pyatv/protocols/airplay/channels.py (DataStreamChannel.__init__) — line 229-232 as of pyatv 0.18.0
	public DataStreamChannel (byte[] outputKey, byte[] inputKey)
		: base (outputKey, inputKey)
		{
		// pyatv/protocols/airplay/channels.py — line 232 as of pyatv 0.18.0: randrange(0x100000000, 0x1FFFFFFFF)
		_sendSeqNo = 0x100000000UL + (ulong)(_random.NextDouble () * 0xFFFFFFFFUL);
		}

	/// <summary>Gets or sets the listener notified of incoming protobuf messages and connection loss.</summary>
	public IDataStreamListener? Listener { get; set; }

	/// <summary>Serialize a protobuf message and send it to the receiver.</summary>
	/// <param name="message">The message to send.</param>
	// pyatv/protocols/airplay/channels.py (DataStreamChannel.send_protobuf) — line 266-279 as of pyatv 0.18.0
	public void SendProtobuf (ProtocolMessage message)
		{
		byte[] payload = EncodePayload (EncodeProtobufs ([message]));
		byte[] frame = EncodeMessage (
			messageType: PadTo (System.Text.Encoding.ASCII.GetBytes ("sync"), 12),
			command: PadTo (System.Text.Encoding.ASCII.GetBytes ("comm"), 4),
			seqno: _sendSeqNo,
			padding: DataHeaderPadding,
			payload: payload);
		Send (frame);
		}

	// pyatv/protocols/airplay/channels.py (BaseDataStreamChannel.encode_message) — line 137-149 as of pyatv 0.18.0
	private static byte[] EncodeMessage (byte[] messageType, byte[] command, ulong seqno, int padding, byte[] payload)
		{
		var header = new byte[HeaderLength];
		int offset = 0;
		WriteUInt32BigEndian (header, ref offset, (uint)(HeaderLength + payload.Length));
		Array.Copy (messageType, 0, header, offset, 12);
		offset += 12;
		Array.Copy (command, 0, header, offset, 4);
		offset += 4;
		WriteUInt64BigEndian (header, ref offset, seqno);
		WriteUInt32BigEndian (header, ref offset, (uint)padding);

		var result = new byte[header.Length + payload.Length];
		System.Buffer.BlockCopy (header, 0, result, 0, header.Length);
		System.Buffer.BlockCopy (payload, 0, result, header.Length, payload.Length);
		return result;
		}

	/// <summary>Encode data stream channel reply.</summary>
	/// <param name="seqno">The sequence number of the message being replied to.</param>
	// pyatv/protocols/airplay/channels.py (BaseDataStreamChannel.encode_reply) — line 168-177 as of pyatv 0.18.0
	private static byte[] EncodeReply (ulong seqno)
		{
		byte[] messageType = new byte[12];
		System.Text.Encoding.ASCII.GetBytes ("rply").CopyTo (messageType, 0);
		return EncodeMessage (messageType, new byte[4], seqno, DataHeaderPadding, []);
		}

	// pyatv/protocols/airplay/channels.py (BaseDataStreamChannel.encode_payload) — line 151-153 as of pyatv 0.18.0
	private static byte[] EncodePayload (byte[] protobufData)
		{
		var dict = new Claunia.PropertyList.NSDictionary ();
		var paramsDict = new Claunia.PropertyList.NSDictionary ();
		paramsDict.Add ("data", protobufData);
		dict.Add ("params", paramsDict);
		return AppleTvControlLibrary.Mrp.AirPlay.Http.PlistBody.Encode (dict);
		}

	// pyatv/protocols/airplay/channels.py (BaseDataStreamChannel.encode_protobufs) — line 155-165 as of pyatv 0.18.0
	private static byte[] EncodeProtobufs (IReadOnlyList<ProtocolMessage> messages)
		{
		var result = new List<byte> ();
		foreach (ProtocolMessage message in messages)
			{
			byte[] serialized = message.ToByteArray ();
			result.AddRange (Variant.WriteVariant (serialized.Length));
			result.AddRange (serialized);
			}

		return [.. result];
		}

	// pyatv/protocols/airplay/channels.py (BaseDataStreamChannel.decode_protobufs) — line 198-224 as of pyatv 0.18.0
	private static List<ProtocolMessage> DecodeProtobufs (byte[] data)
		{
		var messages = new List<ProtocolMessage> ();
		int offset = 0;
		try
			{
			while (offset < data.Length)
				{
				byte[] remaining = Slice (data, offset, data.Length - offset);
				byte[] message;

				// pyatv/protocols/airplay/channels.py — line 205-211 as of pyatv 0.18.0: tag 0x08 (field
				// #1, type) means the message is not length-prefixed (known for ConfigureConnectionMessage).
				if (remaining[0] == 0x8)
					{
					message = remaining;
					offset = data.Length;
					}
				else
					{
					(long length, byte[] raw) = Variant.ReadVariant (remaining);
					if (raw.Length < length)
						{
						break;
						}

					message = Slice (raw, 0, (int)length);
					offset = data.Length - (raw.Length - (int)length);
					}

				var protocolMessage = new ProtocolMessage ();
				protocolMessage.MergeFrom ((byte[])message);
				messages.Add (protocolMessage);
				}
			}
		catch
			{
			// pyatv/protocols/airplay/channels.py — line 223-224 as of pyatv 0.18.0: a malformed data
			// frame is logged and discarded rather than tearing down the channel.
			}

		return messages;
		}

	// pyatv/protocols/airplay/channels.py (BaseDataStreamChannel.decode_message) — line 179-197 as of pyatv 0.18.0
	private static bool TryDecodeMessage (byte[] data, out byte[] messageType, out ulong seqno, out byte[]? payload, out byte[] consumed, out byte[] rest)
		{
		messageType = [];
		seqno = 0;
		payload = null;
		consumed = [];
		rest = data;

		if (data.Length < HeaderLength)
			{
			return false;
			}

		int offset = 0;
		uint size = ReadUInt32BigEndian (data, ref offset);
		messageType = Slice (data, offset, 12);
		offset += 12;
		offset += 4; // command
		seqno = ReadUInt64BigEndian (data, ref offset);
		offset += 4; // padding

		if (data.Length < size)
			{
			return false;
			}

		payload = Slice (data, HeaderLength, (int)size - HeaderLength);
		consumed = Slice (data, 0, (int)size);
		rest = Slice (data, (int)size, data.Length - (int)size);
		return true;
		}

	/// <summary>Handle received data that was put in the buffer.</summary>
	// pyatv/protocols/airplay/channels.py (DataStreamChannel.handle_received) — line 240-250 as of pyatv 0.18.0
	protected override void HandleReceived ()
		{
		while (Buffer.Count >= HeaderLength)
			{
			byte[] bufferArray = [.. Buffer];
			if (!TryDecodeMessage (bufferArray, out byte[] messageType, out ulong seqno, out byte[]? payload, out byte[] consumed, out byte[] rest))
				{
				break;
				}

			Buffer.Clear ();
			Buffer.AddRange (rest);

			if (payload is { Length: > 0 })
				{
				ProcessPayload (payload);
				}

			// pyatv/protocols/airplay/channels.py — line 254-255 as of pyatv 0.18.0: reply only if this
			// was a "sync" request (message_type.startswith(b"sync")).
			if (messageType.Length >= 4 &&
				messageType[0] == (byte)'s' && messageType[1] == (byte)'y' && messageType[2] == (byte)'n' && messageType[3] == (byte)'c')
				{
				Send (EncodeReply (seqno));
				}
			}
		}

	// pyatv/protocols/airplay/channels.py (DataStreamChannel._process_payload) — line 257-264 as of pyatv 0.18.0
	private void ProcessPayload (byte[] payload)
		{
		Claunia.PropertyList.NSDictionary decoded;
		try
			{
			decoded = AppleTvControlLibrary.Mrp.AirPlay.Http.PlistBody.Decode (payload);
			}
		catch
			{
			return;
			}

		if (!decoded.TryGetValue ("params", out Claunia.PropertyList.NSObject? paramsObj) ||
			paramsObj is not Claunia.PropertyList.NSDictionary paramsDict ||
			!paramsDict.TryGetValue ("data", out Claunia.PropertyList.NSObject? dataObj) ||
			dataObj is not Claunia.PropertyList.NSData dataNode)
			{
			return;
			}

		foreach (ProtocolMessage message in DecodeProtobufs (dataNode.Bytes))
			{
			Listener?.HandleProtobuf (message);
			}
		}

	/// <summary>Device connection was dropped.</summary>
	/// <param name="exception">The exception that caused the drop, or <see langword="null"/> for a clean close.</param>
	// pyatv/protocols/airplay/channels.py (DataStreamChannel.connection_lost) — line 234-236 as of pyatv 0.18.0
	protected override void OnConnectionLost (Exception? exception) => Listener?.HandleConnectionLost (exception);

	private static byte[] PadTo (byte[] value, int length)
		{
		var result = new byte[length];
		Array.Copy (value, result, Math.Min (value.Length, length));
		return result;
		}

	private static byte[] Slice (byte[] data, int start, int length)
		{
		if (length <= 0)
			{
			return [];
			}

		var result = new byte[length];
		System.Buffer.BlockCopy (data, start, result, 0, length);
		return result;
		}

	private static void WriteUInt32BigEndian (byte[] buffer, ref int offset, uint value)
		{
		buffer[offset] = (byte)(value >> 24);
		buffer[offset + 1] = (byte)(value >> 16);
		buffer[offset + 2] = (byte)(value >> 8);
		buffer[offset + 3] = (byte)value;
		offset += 4;
		}

	private static void WriteUInt64BigEndian (byte[] buffer, ref int offset, ulong value)
		{
		for (int i = 0; i < 8; i++)
			{
			buffer[offset + i] = (byte)(value >> (8 * (7 - i)));
			}

		offset += 8;
		}

	private static uint ReadUInt32BigEndian (byte[] buffer, ref int offset)
		{
		uint value = ((uint)buffer[offset] << 24) | ((uint)buffer[offset + 1] << 16) | ((uint)buffer[offset + 2] << 8) | buffer[offset + 3];
		offset += 4;
		return value;
		}

	private static ulong ReadUInt64BigEndian (byte[] buffer, ref int offset)
		{
		ulong value = 0;
		for (int i = 0; i < 8; i++)
			{
			value = (value << 8) | buffer[offset + i];
			}

		offset += 8;
		return value;
		}
	}
