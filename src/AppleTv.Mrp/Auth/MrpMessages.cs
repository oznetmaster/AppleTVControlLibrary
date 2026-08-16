// Copyright (c) 2026 Neil Colvin. Licensed under the MIT License.
// See LICENSE file in the repository root for full license text.

using System;
using System.Buffers.Text;
using System.Collections.Generic;

using AppleTvControlLibrary.Mrp.Protobuf;
using AppleTvControlLibrary.Tlv8;

namespace AppleTvControlLibrary.Mrp.Auth;

/// <summary>
/// Helper code for dealing with the subset of MRP protobuf messages needed for pairing.
/// </summary>
// pyatv/protocols/mrp/messages.py — line 1-71 as of pyatv 0.18.0
public static class MrpMessages
	{
	/// <summary>Create a ProtocolMessage of the given type.</summary>
	/// <param name="messageType">The message type to set.</param>
	/// <param name="errorCode">The error code to set.</param>
	/// <param name="identifier">An optional message identifier to correlate a response to a request.</param>
	/// <returns>The created message.</returns>
	// pyatv/protocols/mrp/messages.py (create) — line 13-20 as of pyatv 0.18.0
	public static ProtocolMessage Create (ProtocolMessage.Types.Type messageType, AppleTvControlLibrary.Mrp.Protobuf.ErrorCode.Types.Enum errorCode = AppleTvControlLibrary.Mrp.Protobuf.ErrorCode.Types.Enum.NoError, string? identifier = null)
		{
		var message = new ProtocolMessage
			{
			Type = messageType,
			ErrorCode = errorCode,
			UniqueIdentifier = Guid.NewGuid ().ToString ().ToUpperInvariant (),
			};

		if (identifier is not null)
			{
			message.Identifier = identifier;
			}

		return message;
		}

	/// <summary>Create a new CRYPTO_PAIRING_MESSAGE.</summary>
	/// <param name="pairingData">The TLV8 fields to encode into the message's pairingData field.</param>
	/// <param name="isPairing">Whether this message is part of pair-setup (as opposed to pair-verify).</param>
	/// <returns>The created message.</returns>
	// pyatv/protocols/mrp/messages.py (crypto_pairing) — line 65-75 as of pyatv 0.18.0
	public static ProtocolMessage CryptoPairing (Dictionary<int, byte[]> pairingData, bool isPairing = false)
		{
		ProtocolMessage message = Create (ProtocolMessage.Types.Type.CryptoPairingMessage);
		var crypto = new CryptoPairingMessage
			{
			Status = 0,
			PairingData = Google.Protobuf.ByteString.CopyFrom (Tlv8.Tlv8.WriteTlv (pairingData)),
			// Hardcoded values for now, might have to be changed — pyatv/protocols/mrp/messages.py line 72-74 as of pyatv 0.18.0
			IsRetrying = false,
			IsUsingSystemPairing = false,
			State = isPairing ? 2 : 0,
			};

		message.SetExtension (CryptoPairingMessageExtensions.CryptoPairingMessage, crypto);
		return message;
		}

	/// <summary>Create a new DEVICE_INFO_MESSAGE (or DEVICE_INFO_UPDATE_MESSAGE).</summary>
	/// <param name="name">The device name to report.</param>
	/// <param name="systemBuildVersion">The system build version to report.</param>
	/// <param name="identifier">The unique device identifier.</param>
	/// <param name="update">Whether to build a DEVICE_INFO_UPDATE_MESSAGE instead of DEVICE_INFO_MESSAGE.</param>
	/// <returns>The created message, with the DeviceInfoMessage extension populated.</returns>
	// pyatv/protocols/mrp/messages.py (device_information) — line 24-42 as of pyatv 0.18.0
	public static ProtocolMessage DeviceInformation (string name, string systemBuildVersion, string identifier, bool update = false)
		{
		ProtocolMessage message = Create (update ? ProtocolMessage.Types.Type.DeviceInfoUpdateMessage : ProtocolMessage.Types.Type.DeviceInfoMessage);
		var info = new DeviceInfoMessage
			{
			AllowsPairing = true,
			ApplicationBundleIdentifier = "com.apple.TVRemote",
			ApplicationBundleVersion = "344.28",
			LastSupportedMessageType = 108,
			LocalizedModelName = "iPhone",
			Name = name,
			ProtocolVersion = 1,
			SharedQueueVersion = 2,
			SupportsACL = true,
			SupportsExtendedMotion = true,
			SupportsSharedQueue = true,
			SupportsSystemPairing = true,
			SystemBuildVersion = systemBuildVersion,
			SystemMediaApplication = "com.apple.TVMusic",
			UniqueIdentifier = identifier,
			DeviceClass = DeviceClass.Types.Enum.IPhone,
			LogicalDeviceCount = 1,
			};

		message.SetExtension (DeviceInfoMessageExtensions.DeviceInfoMessage, info);
		return message;
		}

	/// <summary>Create a new SEND_COMMAND_RESULT_MESSAGE.</summary>
	/// <param name="identifier">The identifier of the SEND_COMMAND_MESSAGE being answered.</param>
	/// <param name="sendError">The error code to report, or <see cref="SendError.Types.Enum.NoError"/> on success.</param>
	/// <returns>The created message.</returns>
	// pyatv/protocols/mrp/messages.py (command_result) — line 172-177 as of pyatv 0.18.0
	public static ProtocolMessage CommandResult (string identifier, SendError.Types.Enum sendError = SendError.Types.Enum.NoError)
		{
		ProtocolMessage message = Create (ProtocolMessage.Types.Type.SendCommandResultMessage, identifier: identifier);
		var inner = new SendCommandResultMessage
			{
			SendError = sendError,
			HandlerReturnStatus = HandlerReturnStatus.Types.Enum.Success,
			};

		message.SetExtension (SendCommandResultMessageExtensions.SendCommandResultMessage, inner);
		return message;
		}

	/// <summary>Create a new WAKE_DEVICE_MESSAGE.</summary>
	/// <returns>The created message.</returns>
	// pyatv/protocols/mrp/messages.py (wake_device) — line 51-53 as of pyatv 0.18.0
	public static ProtocolMessage WakeDevice () => Create (ProtocolMessage.Types.Type.WakeDeviceMessage);

	/// <summary>Create a new SET_CONNECTION_STATE_MESSAGE with state set to Connected.</summary>
	/// <returns>The created message.</returns>
	// pyatv/protocols/mrp/messages.py (set_connection_state) — line 56-60 as of pyatv 0.18.0
	public static ProtocolMessage SetConnectionState ()
		{
		ProtocolMessage message = Create (ProtocolMessage.Types.Type.SetConnectionStateMessage);
		var inner = new SetConnectionStateMessage
			{
			State = SetConnectionStateMessage.Types.ConnectionState.Connected,
			};

		message.SetExtension (SetConnectionStateMessageExtensions.SetConnectionStateMessage, inner);
		return message;
		}

	/// <summary>Create a new GET_KEYBOARD_SESSION_MESSAGE.</summary>
	/// <returns>The created message.</returns>
	// pyatv/protocols/mrp/messages.py (get_keyboard_session) — line 63-65 as of pyatv 0.18.0
	public static ProtocolMessage GetKeyboardSession () => Create (ProtocolMessage.Types.Type.GetKeyboardSessionMessage);

	/// <summary>Create a new CLIENT_UPDATES_CONFIG_MESSAGE.</summary>
	/// <param name="artwork">Whether to subscribe to artwork updates.</param>
	/// <param name="nowPlaying">Whether to subscribe to now-playing updates.</param>
	/// <param name="volume">Whether to subscribe to volume updates.</param>
	/// <param name="keyboard">Whether to subscribe to keyboard updates.</param>
	/// <param name="outputDeviceUpdates">Whether to subscribe to output device updates.</param>
	/// <returns>The created message.</returns>
	// pyatv/protocols/mrp/messages.py (client_updates_config) — line 82-97 as of pyatv 0.18.0
	public static ProtocolMessage ClientUpdatesConfig (bool artwork = true, bool nowPlaying = false, bool volume = true, bool keyboard = true, bool outputDeviceUpdates = true)
		{
		ProtocolMessage message = Create (ProtocolMessage.Types.Type.ClientUpdatesConfigMessage);
		var inner = new ClientUpdatesConfigMessage
			{
			ArtworkUpdates = artwork,
			NowPlayingUpdates = nowPlaying,
			VolumeUpdates = volume,
			KeyboardUpdates = keyboard,
			OutputDeviceUpdates = outputDeviceUpdates,
			};

		message.SetExtension (ClientUpdatesConfigMessageExtensions.ClientUpdatesConfigMessage, inner);
		return message;
		}

	/// <summary>Create a new PLAYBACK_QUEUE_REQUEST_MESSAGE, used to explicitly fetch artwork bytes for an item.</summary>
	/// <param name="location">The queue location (index) of the item to fetch.</param>
	/// <param name="width">The requested artwork width, or -1 for no preference.</param>
	/// <param name="height">The requested artwork height, or -1 for no preference.</param>
	/// <returns>The created message.</returns>
	// pyatv/protocols/mrp/messages.py (playback_queue_request) — line 100-109 as of pyatv 0.18.0
	public static ProtocolMessage PlaybackQueueRequest (int location, int width = -1, int height = 400)
		{
		ProtocolMessage message = Create (ProtocolMessage.Types.Type.PlaybackQueueRequestMessage);
		var inner = new PlaybackQueueRequestMessage
			{
			Location = location,
			Length = 1,
			ArtworkWidth = width,
			ArtworkHeight = height,
			ReturnContentItemAssetsInUserCompletion = true,
			};

		message.SetExtension (PlaybackQueueRequestMessageExtensions.PlaybackQueueRequestMessage, inner);
		return message;
		}

	/// <summary>Extract the CryptoPairingMessage extension from a ProtocolMessage and decode its TLV8 pairing data.</summary>
	/// <param name="message">The received message.</param>
	/// <returns>The decoded TLV8 fields.</returns>
	/// <exception cref="AppleTvControlLibrary.Auth.AuthenticationException">Thrown if the pairing data contains a TlvValue.Error entry.</exception>
	// pyatv/protocols/mrp/auth.py (_get_pairing_data) — line 19-23 as of pyatv 0.18.0
	public static Dictionary<int, byte[]> GetPairingData (ProtocolMessage message)
		{
		CryptoPairingMessage inner = message.GetExtension (CryptoPairingMessageExtensions.CryptoPairingMessage);
		Dictionary<int, byte[]> tlv = Tlv8.Tlv8.ReadTlv (inner.PairingData.ToByteArray ());

		return tlv.ContainsKey ((int)TlvValue.Error)
			? throw new AppleTvControlLibrary.Auth.AuthenticationException (Tlv8.Tlv8.Stringify (tlv))
			: tlv;
		}

	/// <summary>Create a new SEND_HID_EVENT_MESSAGE for the given HID usage page/usage.</summary>
	/// <param name="usePage">The HID usage page.</param>
	/// <param name="usage">The HID usage within the page.</param>
	/// <param name="down">Whether this is a key-down (as opposed to key-up) event.</param>
	/// <returns>The created message.</returns>
	// pyatv/protocols/mrp/messages.py (send_hid_event) — line 112-138 as of pyatv 0.18.0
	public static ProtocolMessage SendHidEvent (int usePage, int usage, bool down)
		{
		ProtocolMessage message = Create (ProtocolMessage.Types.Type.SendHidEventMessage);
		var inner = new SendHIDEventMessage ();

		// pyatv/protocols/mrp/messages.py — line 120 as of pyatv 0.18.0: hardcoded mach AbsoluteTime;
		// the device does not seem to care about the actual value.
		byte[] abstime = ParseHex ("438922cf08020000");

		var data = new byte[6];
		data[0] = (byte)(usePage >> 8);
		data[1] = (byte)usePage;
		data[2] = (byte)(usage >> 8);
		data[3] = (byte)usage;
		data[4] = 0;
		data[5] = (byte)(down ? 1 : 0);

		// pyatv/protocols/mrp/messages.py — line 130-133 as of pyatv 0.18.0: undecoded but fixed
		// framing bytes surrounding the usePage/usage/down data. This is exactly the concatenation
		// of the two hex literals `binascii.unhexlify(b"00000000000000000100000000000000020" +
		// b"00000200000000300000001000000000000")` from the source.
		byte[] prefix = ParseHex ("0000000000000000010000000000000002000000200000000300000001000000000000");
		byte[] suffix = ParseHex ("0000000000000001000000");

		var buffer = new byte[abstime.Length + prefix.Length + data.Length + suffix.Length];
		int offset = 0;
		Array.Copy (abstime, 0, buffer, offset, abstime.Length);
		offset += abstime.Length;
		Array.Copy (prefix, 0, buffer, offset, prefix.Length);
		offset += prefix.Length;
		Array.Copy (data, 0, buffer, offset, data.Length);
		offset += data.Length;
		Array.Copy (suffix, 0, buffer, offset, suffix.Length);

		inner.HidEventData = Google.Protobuf.ByteString.CopyFrom (buffer);
		message.SetExtension (SendHIDEventMessageExtensions.SendHIDEventMessage, inner);
		return message;
		}

	/// <summary>Create a new SEND_BUTTON_EVENT_MESSAGE.</summary>
	/// <param name="usagePage">The HID usage page.</param>
	/// <param name="usage">The HID usage within the page.</param>
	/// <param name="buttonDown">Whether this is a button-down (as opposed to button-up) event.</param>
	/// <returns>The created message.</returns>
	// pyatv/protocols/mrp/messages.py (send_button) — line 141-148 as of pyatv 0.18.0
	public static ProtocolMessage SendButton (int usagePage, int usage, bool buttonDown)
		{
		ProtocolMessage message = Create (ProtocolMessage.Types.Type.SendButtonEventMessage);
		var inner = new SendButtonEventMessage
			{
			UsagePage = (uint)usagePage,
			Usage = (uint)usage,
			ButtonDown = buttonDown,
			};

		message.SetExtension (SendButtonEventMessageExtensions.SendButtonEventMessage, inner);
		return message;
		}

	/// <summary>Create a new SEND_COMMAND_MESSAGE for a playback command.</summary>
	/// <param name="command">The command to send.</param>
	/// <param name="configureOptions">An optional callback used to populate the command's options.</param>
	/// <returns>The created message.</returns>
	// pyatv/protocols/mrp/messages.py (command) — line 151-158 as of pyatv 0.18.0
	public static ProtocolMessage Command (Command command, Action<CommandOptions>? configureOptions = null)
		{
		ProtocolMessage message = Create (ProtocolMessage.Types.Type.SendCommandMessage);
		var inner = new SendCommandMessage
			{
			Command = command,
			Options = new CommandOptions (),
			};

		configureOptions?.Invoke (inner.Options);

		message.SetExtension (SendCommandMessageExtensions.SendCommandMessage, inner);
		return message;
		}

	/// <summary>Create a SEND_COMMAND_MESSAGE that changes the repeat mode of the current player.</summary>
	/// <param name="mode">The repeat mode to set.</param>
	/// <returns>The created message.</returns>
	// pyatv/protocols/mrp/messages.py (repeat) — line 170-181 as of pyatv 0.18.0
	public static ProtocolMessage Repeat (RepeatMode.Types.Enum mode) => Command (AppleTvControlLibrary.Mrp.Protobuf.Command.ChangeRepeatMode, options =>
																										{
																											options.SendOptions = 0;
																											options.RepeatMode = mode;
																										});

	/// <summary>Create a SEND_COMMAND_MESSAGE that changes the shuffle mode of the current player.</summary>
	/// <param name="state">The shuffle mode to set.</param>
	/// <returns>The created message.</returns>
	// pyatv/protocols/mrp/messages.py (shuffle) — line 184-195 as of pyatv 0.18.0
	public static ProtocolMessage Shuffle (ShuffleMode.Types.Enum state) => Command (AppleTvControlLibrary.Mrp.Protobuf.Command.ChangeShuffleMode, options =>
																											{
																												options.SendOptions = 0;
																												options.ShuffleMode = state;
																											});

	/// <summary>Create a SEND_COMMAND_MESSAGE that seeks to an absolute position in the current stream.</summary>
	/// <param name="position">The playback position, in seconds.</param>
	/// <returns>The created message.</returns>
	// pyatv/protocols/mrp/messages.py (seek_to_position) — line 198-203 as of pyatv 0.18.0
	public static ProtocolMessage SeekToPosition (double position) => Command (AppleTvControlLibrary.Mrp.Protobuf.Command.SeekToPlaybackPosition, options => options.PlaybackPosition = position);

	/// <summary>Create a new SET_VOLUME_MESSAGE to change the volume on a specific output device.</summary>
	/// <param name="deviceUid">The output device identifier.</param>
	/// <param name="volume">The volume level, in the range 0.0-1.0.</param>
	/// <returns>The created message.</returns>
	// pyatv/protocols/mrp/messages.py (set_volume) — line 206-212 as of pyatv 0.18.0
	public static ProtocolMessage SetVolume (string deviceUid, float volume)
		{
		ProtocolMessage message = Create (ProtocolMessage.Types.Type.SetVolumeMessage);
		var inner = new SetVolumeMessage
			{
			OutputDeviceUID = deviceUid,
			Volume = volume,
			};

		message.SetExtension (SetVolumeMessageExtensions.SetVolumeMessage, inner);
		return message;
		}

	/// <summary>Parses a hex string into a byte array. <c>Convert.FromHexString</c> is not
	/// available on net472, so this is a small polyfill kept local to this file, built on
	/// <see cref="Utf8Parser.TryParse(ReadOnlySpan{byte}, out byte, out int, char)"/> from
	/// <c>System.Memory</c> (already referenced on net472) to avoid per-byte string allocation.</summary>
	/// <param name="hex">The hex string to parse. Must have an even number of characters.</param>
	/// <returns>The parsed bytes.</returns>
	private static byte[] ParseHex (string hex)
		{
		byte[] utf8 = System.Text.Encoding.ASCII.GetBytes (hex);
		var bytes = new byte[hex.Length / 2];
		for (int i = 0; i < bytes.Length; i++)
			{
			if (!Utf8Parser.TryParse (utf8.AsSpan (i * 2, 2), out byte value, out _, 'X'))
				{
				throw new FormatException ($"Invalid hex pair at index {i * 2}: {hex.Substring (i * 2, 2)}");
				}

			bytes[i] = value;
			}

		return bytes;
		}
	}
