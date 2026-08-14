// Copyright (c) 2026 Neil Colvin. Licensed under the MIT License.
// See LICENSE file in the repository root for full license text.

using System;
using System.Collections.Generic;
using System.Linq;

using Org.BouncyCastle.Crypto.Agreement;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Signers;
using Org.BouncyCastle.Math.EC.Rfc8032;

using AppleTvControlLibrary.Auth;
using AppleTvControlLibrary.Crypto;
using AppleTvControlLibrary.Mrp.Auth;
using AppleTvControlLibrary.Mrp.Protobuf;
using AppleTvControlLibrary.Tlv8;

namespace AppleTvControlLibrary.Mrp.FakeDevice;

/// <summary>
/// In-memory fake MRP Apple TV used to validate the client-side pairing/verification implementation
/// without a real device or socket transport.
/// </summary>
/// <remarks>
/// Ported from <c>pyatv/protocols/mrp/server_auth.py</c> (<c>MrpServerAuth</c>). Unlike the Python
/// original, this type operates directly on decoded <see cref="ProtocolMessage"/> instances via
/// <see cref="HandleMessage"/> rather than raw sockets, so it can be driven in-process by a test using
/// the <see cref="MrpSendAndReceive"/> delegate consumed by <see cref="MrpPairSetupProcedure"/> and
/// <see cref="MrpPairVerifyProcedure"/>.
/// </remarks>
// pyatv/protocols/mrp/server_auth.py (MrpServerAuth) — line 69-229 as of pyatv 0.18.0
public sealed class FakeMrpDevice
	{
	// pyatv/auth/server_auth.py — line 3 as of pyatv 0.18.0
	public const int PIN_CODE = 1111;

	// pyatv/auth/server_auth.py — line 12 as of pyatv 0.18.0
	public const string SERVER_IDENTIFIER = "5D797FD3-3538-427E-A47B-A32FC6CF3A6A";

	// pyatv/tests/fake_device/mrp.py — line 66-72 as of pyatv 0.18.0
	public const string DEVICE_NAME = "Fake MRP ATV";
	public const string DEVICE_UID = "E510C430-B01D-45DF-B558-6EA6F8251069";
	public const string BUILD_NUMBER = "18M60";
	public const string PLAYER_IDENTIFIER = "com.github.postlund.pyatv";
	private const double VOLUME_STEP = 0.05;

	// pyatv/tests/fake_device/mrp.py (_COMMAND_LOOKUP) — line 43-50 as of pyatv 0.18.0
	private static readonly Dictionary<Command, string> CommandLookup = new ()
		{
		{ Command.Play, "play" },
		{ Command.TogglePlayPause, "playpause" },
		{ Command.Pause, "pause" },
		{ Command.Stop, "stop" },
		{ Command.NextTrack, "nextitem" },
		{ Command.PreviousTrack, "previtem" },
		};

	// pyatv/tests/fake_device/mrp.py (_KEY_LOOKUP) — line 24-39 as of pyatv 0.18.0
	private static readonly Dictionary<(int UsePage, int Usage), string> KeyLookup = new ()
		{
		{ (1, 0x8C), "up" },
		{ (1, 0x8D), "down" },
		{ (1, 0x8B), "left" },
		{ (1, 0x8A), "right" },
		{ (12, 0xB7), "stop" },
		{ (12, 0xB5), "next" },
		{ (12, 0xB6), "previous" },
		{ (1, 0x89), "select" },
		{ (1, 0x86), "menu" },
		{ (12, 0x60), "top_menu" },
		{ (12, 0x40), "home" },
		{ (1, 0x82), "suspend" },
		{ (1, 0x83), "wakeup" },
		{ (12, 0xE9), "volumeup" },
		{ (12, 0xEA), "volumedown" },
		};

	// pyatv/auth/server_auth.py (32 * b"\xaa") — line 13 as of pyatv 0.18.0
	private static readonly byte[] PrivateKeySeed = CreateSeed ();

	private readonly byte[] _uniqueId;
	private readonly FakeMrpServerKeys _keys;
	private readonly FakeMrpSrpServer _srpSession;

	private byte[]? _outputKey;
	private byte[]? _inputKey;

	/// <summary>Gets the shared, mutable device state (now-playing metadata, volume, output devices, etc.).</summary>
	public FakeMrpDeviceState State { get; } = new ();

	/// <summary>Gets or sets the callback used to push an unsolicited message to the client (e.g. a SET_STATE_MESSAGE).</summary>
	/// <remarks>
	/// pyatv's fake device pushes messages directly to a connected socket (<c>send_to_client</c>); since this port has
	/// no socket of its own, a caller wires this delegate to whatever transport/queue is driving the test.
	/// </remarks>
	// pyatv/tests/fake_device/mrp.py (FakeMrpService.send_to_client) — line 407-416 as of pyatv 0.18.0
	public Action<ProtocolMessage>? SendToClient { get; set; }

	/// <summary>Initializes a new instance of the <see cref="FakeMrpDevice"/> class.</summary>
	/// <param name="uniqueId">The device identifier reported during pairing. Defaults to <see cref="SERVER_IDENTIFIER"/>.</param>
	/// <param name="pin">The PIN code required to complete pair-setup. Defaults to <see cref="PIN_CODE"/>.</param>
	// pyatv/protocols/mrp/server_auth.py (MrpServerAuth.__init__) — line 72-79 as of pyatv 0.18.0
	public FakeMrpDevice (string? uniqueId = null, int pin = PIN_CODE)
		{
		_uniqueId = System.Text.Encoding.UTF8.GetBytes (uniqueId ?? SERVER_IDENTIFIER);
		_keys = FakeMrpServerKeys.Generate (PrivateKeySeed);
		_srpSession = FakeMrpSrpServer.Create (pin);
		}

	/// <summary>Gets a value indicating whether pairing has completed successfully.</summary>
	public bool HasPaired
		{
		get;
		private set;
		}

	/// <summary>Gets the key derived by the server to encrypt data sent to the client, set after pair-verify M1. pyatv/protocols/mrp/server_auth.py — line 158-160 as of pyatv 0.18.0</summary>
	public byte[]? ServerOutputKey => _outputKey;

	/// <summary>Gets the key derived by the server to decrypt data from the client, set after pair-verify M1. pyatv/protocols/mrp/server_auth.py — line 158-160 as of pyatv 0.18.0</summary>
	public byte[]? ServerInputKey => _inputKey;

	/// <summary>Gets the client identifier learned during the most recent pair-setup, if any.</summary>
	public byte[]? PairedClientId
		{
		get;
		private set;
		}

	/// <summary>Handle an incoming CRYPTO_PAIRING_MESSAGE, mirroring the client-driven pair-setup/pair-verify state machine.</summary>
	/// <param name="message">The received message.</param>
	/// <returns>The CRYPTO_PAIRING_MESSAGE response to send back to the client.</returns>
	// pyatv/protocols/mrp/server_auth.py (handle_crypto_pairing) — line 104-116 as of pyatv 0.18.0
	public ProtocolMessage HandleMessage (ProtocolMessage message)
		{
		Dictionary<int, byte[]> pairingData = MrpMessages.GetPairingData (message);
		int seqNo = pairingData[(int)TlvValue.SeqNo][0];

		// pyatv/protocols/mrp/server_auth.py — line 108-113 as of pyatv 0.18.0: work-around to
		// support "tries" to auth before pairing
		if (seqNo == 1)
			{
			if (pairingData.ContainsKey ((int)TlvValue.PublicKey))
				{
				HasPaired = true;
				}
			else if (pairingData.ContainsKey ((int)TlvValue.Method))
				{
				HasPaired = false;
				}
			}

		return (HasPaired, seqNo) switch
			{
			(true, 1) => M1Verify (pairingData),
			(true, 3) => M3Verify (pairingData),
			(false, 1) => M1Setup (pairingData),
			(false, 3) => M3Setup (pairingData),
			(false, 5) => M5Setup (pairingData),
			_ => throw new NotSupportedException ($"seqno {seqNo} (has_paired={HasPaired})"),
			};
		}

	// pyatv/protocols/mrp/server_auth.py (_m1_verify) — line 118-146 as of pyatv 0.18.0
	private ProtocolMessage M1Verify (Dictionary<int, byte[]> pairingData)
		{
		byte[] serverPubKey = _keys.VerifyPub.GetEncoded ();
		byte[] clientPubKey = pairingData[(int)TlvValue.PublicKey];

		var agreement = new X25519Agreement ();
		agreement.Init (_keys.Verify);
		var shared = new byte[agreement.AgreementSize];
		agreement.CalculateAgreement (new X25519PublicKeyParameters (clientPubKey, 0), shared, 0);

		byte[] sessionKey = SrpAuthHandler.HkdfExpand ("Pair-Verify-Encrypt-Salt", "Pair-Verify-Encrypt-Info", shared);

		byte[] info = Concat (serverPubKey, _uniqueId, clientPubKey);
		byte[] signature = new byte[Ed25519PrivateKeyParameters.SignatureSize];
		_keys.Sign.Sign (Ed25519.Algorithm.Ed25519, null, info, 0, info.Length, signature, 0);

		byte[] innerTlv = Tlv8.Tlv8.WriteTlv (new Dictionary<int, byte[]>
			{
			{ (int)TlvValue.Identifier, _uniqueId },
			{ (int)TlvValue.Signature, signature },
			});

		var chacha = new Chacha20Cipher8ByteNonce (sessionKey, sessionKey);
		byte[] encrypted = chacha.Encrypt (innerTlv, nonce: System.Text.Encoding.UTF8.GetBytes ("PV-Msg02"));

		// pyatv/protocols/mrp/server_auth.py — line 149-155 as of pyatv 0.18.0
		_outputKey = SrpAuthHandler.HkdfExpand (MrpProtocolConstants.SrpSalt, MrpProtocolConstants.SrpOutputInfo, shared);
		_inputKey = SrpAuthHandler.HkdfExpand (MrpProtocolConstants.SrpSalt, MrpProtocolConstants.SrpInputInfo, shared);

		return MrpMessages.CryptoPairing (new Dictionary<int, byte[]>
			{
			{ (int)TlvValue.SeqNo, new byte[] { 2 } },
			{ (int)TlvValue.PublicKey, serverPubKey },
			{ (int)TlvValue.EncryptedData, encrypted },
			});
		}

	// pyatv/protocols/mrp/server_auth.py (_m3_verify) — line 187-189 as of pyatv 0.18.0
	private ProtocolMessage M3Verify (Dictionary<int, byte[]> pairingData)
		{
		_ = pairingData;

		return MrpMessages.CryptoPairing (new Dictionary<int, byte[]>
			{
			{ (int)TlvValue.SeqNo, new byte[] { 4 } },
			});
		}

	// pyatv/protocols/mrp/server_auth.py (_m1_setup) — line 176-185 as of pyatv 0.18.0
	private ProtocolMessage M1Setup (Dictionary<int, byte[]> pairingData)
		{
		_ = pairingData;

		return MrpMessages.CryptoPairing (new Dictionary<int, byte[]>
			{
			{ (int)TlvValue.Salt, _srpSession.Salt },
			{ (int)TlvValue.PublicKey, PadServerPublic () },
			{ (int)TlvValue.SeqNo, new byte[] { 2 } },
			});
		}

	// pyatv/protocols/mrp/server_auth.py (_m3_setup) — line 191-206 as of pyatv 0.18.0
	private ProtocolMessage M3Setup (Dictionary<int, byte[]> pairingData)
		{
		byte[] clientPublicKey = pairingData[(int)TlvValue.PublicKey];
		_srpSession.ProcessClientPublicKey (clientPublicKey);

		byte[]? serverProof = _srpSession.VerifyClientProofAndGetServerProof (pairingData[(int)TlvValue.Proof]);

		if (serverProof is not null)
			{
			return MrpMessages.CryptoPairing (new Dictionary<int, byte[]>
				{
				{ (int)TlvValue.Proof, serverProof },
				{ (int)TlvValue.SeqNo, new byte[] { 4 } },
				});
			}

		return MrpMessages.CryptoPairing (new Dictionary<int, byte[]>
			{
			{ (int)TlvValue.Error, new byte[] { (byte)AppleTvControlLibrary.Tlv8.ErrorCode.Authentication } },
			{ (int)TlvValue.SeqNo, new byte[] { 4 } },
			});
		}

	// pyatv/protocols/mrp/server_auth.py (_m5_setup) — line 208-227 as of pyatv 0.18.0
	private ProtocolMessage M5Setup (Dictionary<int, byte[]> pairingData)
		{
		byte[] sessionKey = SrpAuthHandler.HkdfExpand ("Pair-Setup-Encrypt-Salt", "Pair-Setup-Encrypt-Info", _srpSession.SessionKey);
		byte[] accessoryDeviceX = SrpAuthHandler.HkdfExpand ("Pair-Setup-Accessory-Sign-Salt", "Pair-Setup-Accessory-Sign-Info", _srpSession.SessionKey);

		var chacha = new Chacha20Cipher8ByteNonce (sessionKey, sessionKey);
		byte[] decryptedTlvBytes = chacha.Decrypt (pairingData[(int)TlvValue.EncryptedData], nonce: System.Text.Encoding.UTF8.GetBytes ("PS-Msg05"));

		var decryptedTlv = Tlv8.Tlv8.ReadTlv (decryptedTlvBytes);
		byte[] clientId = decryptedTlv[(int)TlvValue.Identifier];

		byte[] deviceInfo = Concat (accessoryDeviceX, _uniqueId, _keys.AuthPub);
		byte[] signature = new byte[Ed25519PrivateKeyParameters.SignatureSize];
		_keys.Sign.Sign (Ed25519.Algorithm.Ed25519, null, deviceInfo, 0, deviceInfo.Length, signature, 0);

		byte[] innerTlv = Tlv8.Tlv8.WriteTlv (new Dictionary<int, byte[]>
			{
			{ (int)TlvValue.Identifier, _uniqueId },
			{ (int)TlvValue.PublicKey, _keys.AuthPub },
			{ (int)TlvValue.Signature, signature },
			});

		var encryptChacha = new Chacha20Cipher8ByteNonce (sessionKey, sessionKey);
		byte[] encrypted = encryptChacha.Encrypt (innerTlv, nonce: System.Text.Encoding.UTF8.GetBytes ("PS-Msg06"));

		HasPaired = true;
		PairedClientId = clientId;

		return MrpMessages.CryptoPairing (new Dictionary<int, byte[]>
			{
			{ (int)TlvValue.SeqNo, new byte[] { 6 } },
			{ (int)TlvValue.EncryptedData, encrypted },
			});
		}

	private byte[] PadServerPublic ()
		{
		return _srpSession.ServerPublic.ToByteArrayUnsigned ();
		}

	/// <summary>Dispatch a non-pairing protocol message to the appropriate handler, mirroring pyatv's per-type dispatch.</summary>
	/// <param name="message">The received message.</param>
	/// <returns>Zero or more response messages to send back to the client (a handler may reply with none, one, or several messages).</returns>
	// pyatv/tests/fake_device/mrp.py (FakeMrpService.data_received) — line 441-464 as of pyatv 0.18.0
	public IReadOnlyList<ProtocolMessage> HandleProtocolMessage (ProtocolMessage message)
		{
		return message.Type switch
			{
			ProtocolMessage.Types.Type.DeviceInfoMessage => [HandleDeviceInfo (message)],
			ProtocolMessage.Types.Type.SetConnectionStateMessage => HandleSetConnectionState (message),
			ProtocolMessage.Types.Type.ClientUpdatesConfigMessage => HandleClientUpdatesConfig (message),
			ProtocolMessage.Types.Type.GetKeyboardSessionMessage => [HandleGetKeyboardSession (message)],
			ProtocolMessage.Types.Type.SendHidEventMessage => HandleSendHidEvent (message),
			ProtocolMessage.Types.Type.SendCommandMessage => [HandleSendCommand (message)],
			ProtocolMessage.Types.Type.PlaybackQueueRequestMessage => [HandlePlaybackQueueRequest (message)],
			ProtocolMessage.Types.Type.WakeDeviceMessage => HandleWakeDevice (message),
			ProtocolMessage.Types.Type.GenericMessage => [HandleGeneric (message)],
			ProtocolMessage.Types.Type.SetVolumeMessage => [HandleSetVolume (message)],
			ProtocolMessage.Types.Type.ModifyOutputContextRequestMessage => HandleModifyOutputContextRequest (message),
			_ => throw new NotSupportedException ($"No message handler for {message.Type}"),
			};
		}

	// pyatv/tests/fake_device/mrp.py (handle_device_info) — line 464-465 as of pyatv 0.18.0
	private ProtocolMessage HandleDeviceInfo (ProtocolMessage message)
		{
		return BuildDeviceInfo (message.Identifier, update: false);
		}

	// pyatv/tests/fake_device/mrp.py (_send_device_info) — line 419-439 as of pyatv 0.18.0
	private ProtocolMessage BuildDeviceInfo (string? identifier, bool update)
		{
		ProtocolMessage resp = MrpMessages.DeviceInformation (DEVICE_NAME, BUILD_NUMBER, System.Text.Encoding.UTF8.GetString (_uniqueId), update);
		if (identifier is not null)
			{
			resp.Identifier = identifier;
			}

		DeviceInfoMessage inner = resp.GetExtension (DeviceInfoMessageExtensions.DeviceInfoMessage);
		inner.LogicalDeviceCount = (uint)(State.PoweredOn ? 1 : 0);
		inner.DeviceUID = DEVICE_UID;
		inner.ModelID = "AppleTV6,2";
		inner.IsGroupLeader = State.OutputDevices.Count > 0;
		inner.IsProxyGroupPlayer = State.OutputDevices.Count > 0 && !State.OutputDevices.Contains (DEVICE_UID);

		foreach (string device in State.OutputDevices)
			{
			if (device == DEVICE_UID)
				{
				continue;
				}

			inner.GroupedDevices.Add (new DeviceInfoMessage
				{
				Name = "Device " + device.Substring (0, Math.Min (2, device.Length)),
				DeviceUID = device,
				});
			}

		resp.SetExtension (DeviceInfoMessageExtensions.DeviceInfoMessage, inner);
		return resp;
		}

	// pyatv/tests/fake_device/mrp.py (handle_set_connection_state) — line 467-469 as of pyatv 0.18.0
	private IReadOnlyList<ProtocolMessage> HandleSetConnectionState (ProtocolMessage message)
		{
		SetConnectionStateMessage inner = message.GetExtension (SetConnectionStateMessageExtensions.SetConnectionStateMessage);
		State.ConnectionState = (int)inner.State;
		return [];
		}

	// pyatv/tests/fake_device/mrp.py (handle_client_updates_config) — line 478-487 as of pyatv 0.18.0
	private IReadOnlyList<ProtocolMessage> HandleClientUpdatesConfig (ProtocolMessage message)
		{
		var responses = new List<ProtocolMessage> ();
		foreach (KeyValuePair<string, PlayingState> entry in State.States)
			{
			responses.Add (BuildSetStateMessage (entry.Value, entry.Key));
			}

		// pyatv/tests/fake_device/mrp.py — line 486-487 as of pyatv 0.18.0: only reply directly to
		// the request (with an empty UNKNOWN_MESSAGE correlated by identifier) if the client actually
		// set one; MrpProtocol.SendAndReceiveAsync always sets one, so this is what unblocks it.
		if (!string.IsNullOrEmpty (message.Identifier))
			{
			responses.Add (MrpMessages.Create (ProtocolMessage.Types.Type.UnknownMessage, identifier: message.Identifier));
			}

		return responses;
		}

	// pyatv/tests/fake_device/mrp.py (handle_get_keyboard_session) — line 488-492 as of pyatv 0.18.0
	private ProtocolMessage HandleGetKeyboardSession (ProtocolMessage message)
		{
		return MrpMessages.Create (ProtocolMessage.Types.Type.KeyboardMessage, identifier: message.Identifier);
		}

	// pyatv/tests/fake_device/mrp.py (handle_send_hid_event) — line 494-540 as of pyatv 0.18.0
	private IReadOnlyList<ProtocolMessage> HandleSendHidEvent (ProtocolMessage message)
		{
		SendHIDEventMessage inner = message.GetExtension (SendHIDEventMessageExtensions.SendHIDEventMessage);
		byte[] data = inner.HidEventData.ToByteArray ();

		// pyatv/tests/fake_device/mrp.py — line 508-509 as of pyatv 0.18.0: bytes [43:49] hold
		// (usePage: uint16 BE, usage: uint16 BE, down: uint16 BE)
		int usePage = (data[43] << 8) | data[44];
		int usage = (data[45] << 8) | data[46];
		int downPress = (data[47] << 8) | data[48];

		var responses = new List<ProtocolMessage> ();

		if (downPress == 1)
			{
			State.OutstandingKeypresses[(usePage, usage)] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds ();
			responses.Add (MrpMessages.Create (ProtocolMessage.Types.Type.UnknownMessage, identifier: message.Identifier));
			}
		else if (downPress == 0)
			{
			if (State.OutstandingKeypresses.TryGetValue ((usePage, usage), out long downTimestamp))
				{
				if (!KeyLookup.TryGetValue ((usePage, usage), out string? pressedKey))
					{
					throw new InvalidOperationException ($"unsupported key: use_page={usePage}, usage={usage}");
					}

				if (pressedKey == "select" && State.LastButtonPressed == "home")
					{
					State.PoweredOn = false;
					responses.Add (BuildDeviceInfo (identifier: null, update: true));
					}

				long timeDiffMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds () - downTimestamp;
				if (timeDiffMs > 500)
					{
					State.LastButtonAction = FakeMrpInputAction.Hold;
					}
				else if (State.LastButtonPressed == pressedKey)
					{
					State.LastButtonAction = FakeMrpInputAction.DoubleTap;
					}
				else
					{
					State.LastButtonAction = FakeMrpInputAction.SingleTap;
					}

				State.LastButtonPressed = pressedKey;
				State.OutstandingKeypresses.Remove ((usePage, usage));
				responses.Add (MrpMessages.Create (ProtocolMessage.Types.Type.UnknownMessage, identifier: message.Identifier));

				if (pressedKey == "volumeup" && !IsClose (State.Volume, 1.0))
					{
					responses.AddRange (SetVolume (Math.Min (State.Volume + VOLUME_STEP, 1.0), DEVICE_UID));
					}
				else if (pressedKey == "volumedown" && !IsClose (State.Volume, 0.0))
					{
					responses.AddRange (SetVolume (Math.Max (State.Volume - VOLUME_STEP, 0.0), DEVICE_UID));
					}
				}
			}

		return responses;
		}

	// pyatv/tests/fake_device/mrp.py (handle_send_command) — line 542-585 as of pyatv 0.18.0
	private ProtocolMessage HandleSendCommand (ProtocolMessage message)
		{
		SendCommandMessage inner = message.GetExtension (SendCommandMessageExtensions.SendCommandMessage);
		PlayingState state = State.GetPlayerState (State.ActivePlayer ?? PLAYER_IDENTIFIER);

		if (CommandLookup.TryGetValue (inner.Command, out string? button))
			{
			State.LastButtonPressed = button;
			}
		else if (inner.Command == Command.ChangeRepeatMode)
			{
			state.Repeat = inner.Options.RepeatMode switch
				{
				RepeatMode.Types.Enum.Off => RepeatMode.Types.Enum.Off,
				RepeatMode.Types.Enum.One => RepeatMode.Types.Enum.One,
				RepeatMode.Types.Enum.All => RepeatMode.Types.Enum.All,
				_ => RepeatMode.Types.Enum.Off,
				};
			}
		else if (inner.Command == Command.ChangeShuffleMode)
			{
			state.Shuffle = inner.Options.ShuffleMode switch
				{
				ShuffleMode.Types.Enum.Off => ShuffleMode.Types.Enum.Off,
				ShuffleMode.Types.Enum.Albums => ShuffleMode.Types.Enum.Albums,
				ShuffleMode.Types.Enum.Songs => ShuffleMode.Types.Enum.Songs,
				_ => ShuffleMode.Types.Enum.Off,
				};
			}
		else if (inner.Command == Command.SeekToPlaybackPosition)
			{
			state.Position = inner.Options.PlaybackPosition;
			}
		else if (inner.Command == Command.SkipForward)
			{
			state.Position = (state.Position ?? 0) + (int)inner.Options.SkipInterval;
			}
		else if (inner.Command == Command.SkipBackward)
			{
			state.Position = (state.Position ?? 0) - (int)inner.Options.SkipInterval;
			}
		else
			{
			return MrpMessages.CommandResult (message.Identifier, SendError.Types.Enum.NoCommandHandlers);
			}

		State.LastButtonAction = null;
		return MrpMessages.CommandResult (message.Identifier);
		}

	// pyatv/tests/fake_device/mrp.py (handle_playback_queue_request) — line 587-601 as of pyatv 0.18.0
	private ProtocolMessage HandlePlaybackQueueRequest (ProtocolMessage message)
		{
		string activePlayer = State.ActivePlayer ?? PLAYER_IDENTIFIER;
		PlayingState state = State.GetPlayerState (activePlayer);

		ProtocolMessage setState = MrpMessages.Create (ProtocolMessage.Types.Type.SetStateMessage, identifier: message.Identifier);
		var innerState = new SetStateMessage ();

		if (state.Artwork is { Length: > 0 } artworkData)
			{
			var queue = new PlaybackQueue { Location = 0 };
			var item = new ContentItem
				{
				ArtworkData = Google.Protobuf.ByteString.CopyFrom (artworkData),
				ArtworkDataWidth = state.ArtworkWidth ?? 456,
				ArtworkDataHeight = state.ArtworkHeight ?? 789,
				};

			queue.ContentItems.Add (item);
			innerState.PlaybackQueue = queue;
			}

		setState.SetExtension (SetStateMessageExtensions.SetStateMessage, innerState);
		return setState;
		}

	// pyatv/tests/fake_device/mrp.py (handle_wake_device) — line 603-606 as of pyatv 0.18.0
	private IReadOnlyList<ProtocolMessage> HandleWakeDevice (ProtocolMessage message)
		{
		State.PoweredOn = true;
		return [MrpMessages.CommandResult (message.Identifier), BuildDeviceInfo (identifier: null, update: true)];
		}

	// pyatv/tests/fake_device/mrp.py (handle_generic) — line 608-616 as of pyatv 0.18.0
	private ProtocolMessage HandleGeneric (ProtocolMessage message)
		{
		State.HeartbeatCount++;
		return MrpMessages.Create (ProtocolMessage.Types.Type.UnknownMessage, identifier: message.Identifier);
		}

	// pyatv/tests/fake_device/mrp.py (handle_set_volume) — line 618-624 as of pyatv 0.18.0
	private ProtocolMessage HandleSetVolume (ProtocolMessage message)
		{
		SetVolumeMessage inner = message.GetExtension (SetVolumeMessageExtensions.SetVolumeMessage);
		IReadOnlyList<ProtocolMessage> volumeMessages = SetVolume (inner.Volume, inner.OutputDeviceUID);
		_ = volumeMessages;
		return MrpMessages.Create (ProtocolMessage.Types.Type.UnknownMessage, identifier: message.Identifier);
		}

	// pyatv/tests/fake_device/mrp.py (FakeMrpState.set_volume) — line 289-297 as of pyatv 0.18.0
	private IReadOnlyList<ProtocolMessage> SetVolume (double volume, string outputDeviceUid)
		{
		if (volume is < 0 or > 1)
			{
			return [];
			}

		State.Volume = volume;

		ProtocolMessage msg = MrpMessages.Create (ProtocolMessage.Types.Type.VolumeDidChangeMessage);
		var inner = new VolumeDidChangeMessage
			{
			OutputDeviceUID = outputDeviceUid,
			Volume = (float)volume,
			};

		msg.SetExtension (VolumeDidChangeMessageExtensions.VolumeDidChangeMessage, inner);
		return [msg];
		}

	// pyatv/tests/fake_device/mrp.py (handle_modify_output_context_request) — line 626-636 as of pyatv 0.18.0
	private IReadOnlyList<ProtocolMessage> HandleModifyOutputContextRequest (ProtocolMessage message)
		{
		ModifyOutputContextRequestMessage inner = message.GetExtension (ModifyOutputContextRequestMessageExtensions.ModifyOutputContextRequestMessage);
		var responses = new List<ProtocolMessage> ();

		if (inner.AddingDevices.Count > 0)
			{
			foreach (string device in inner.AddingDevices)
				{
				if (!State.OutputDevices.Contains (device))
					{
					State.OutputDevices.Add (device);
					}
				}

			responses.Add (BuildDeviceInfo (identifier: null, update: true));
			}

		if (inner.RemovingDevices.Count > 0)
			{
			foreach (string device in inner.RemovingDevices)
				{
				State.OutputDevices.Remove (device);
				}

			responses.Add (BuildDeviceInfo (identifier: null, update: true));
			}

		if (inner.SettingDevices.Count > 0)
			{
			State.OutputDevices.Clear ();
			State.OutputDevices.AddRange (inner.SettingDevices);
			responses.Add (BuildDeviceInfo (identifier: null, update: true));
			}

		return responses;
		}

	// pyatv/tests/fake_device/mrp.py (_set_state_message) — line 116-159 as of pyatv 0.18.0
	private static ProtocolMessage BuildSetStateMessage (PlayingState metadata, string identifier)
		{
		ProtocolMessage message = MrpMessages.Create (ProtocolMessage.Types.Type.SetStateMessage);
		var inner = new SetStateMessage
			{
			PlaybackState = metadata.PlaybackState,
			DisplayName = "Fake Player",
			};

		if (metadata.SupportedCommands is { Length: > 0 } supportedCommands)
			{
			var commands = new SupportedCommands ();
			foreach (Command command in supportedCommands)
				{
				var item = new CommandInfo { Command = command, Enabled = true };
				if ((command == Command.SkipForward || command == Command.SkipBackward) && metadata.SkipTime is float skipTime)
					{
					item.PreferredIntervals.Add (skipTime);
					}

				commands.SupportedCommands_.Add (item);
				}

			inner.SupportedCommands = commands;
			}

		var queue = new PlaybackQueue { Location = 0 };
		var item2 = new ContentItem ();
		FillItem (item2, metadata, identifier);
		queue.ContentItems.Add (item2);
		inner.PlaybackQueue = queue;

		var playerPath = new PlayerPath
			{
			Client = new NowPlayingClient { ProcessIdentifier = 123, BundleIdentifier = identifier },
			Player = new NowPlayingPlayer { Identifier = "MediaRemote-DefaultPlayer", DisplayName = "Default Player" },
			};

		if (metadata.AppName is not null)
			{
			playerPath.Client.DisplayName = metadata.AppName;
			}

		inner.PlayerPath = playerPath;

		message.SetExtension (SetStateMessageExtensions.SetStateMessage, inner);
		return message;
		}

	// pyatv/tests/fake_device/mrp.py (_fill_item) — line 75-104 as of pyatv 0.18.0
	private static void FillItem (ContentItem item, PlayingState metadata, string identifier)
		{
		if (metadata.Identifier is not null)
			{
			item.Identifier = metadata.Identifier;
			}

		var md = new ContentItemMetadata
			{
			// pyatv/tests/fake_device/mrp.py — line 79 as of pyatv 0.18.0: _COCOA_BASE constant (seconds between 1970 and 2001 epochs)
			ElapsedTimeTimestamp = (new DateTime (1970, 1, 1) - new DateTime (2001, 1, 1)).TotalSeconds,
			};

		if (metadata.Artist is not null)
			{
			md.TrackArtistName = metadata.Artist;
			}

		if (metadata.Album is not null)
			{
			md.AlbumName = metadata.Album;
			}

		if (metadata.Title is not null)
			{
			md.Title = metadata.Title;
			}

		if (metadata.Genre is not null)
			{
			md.Genre = metadata.Genre;
			}

		if (metadata.TotalTime is not null)
			{
			md.Duration = metadata.TotalTime.Value;
			}

		if (metadata.Position is not null)
			{
			md.ElapsedTime = metadata.Position.Value;
			}

		if (metadata.PlaybackRate is not null)
			{
			md.PlaybackRate = metadata.PlaybackRate.Value;
			}

		if (metadata.MediaType is not null)
			{
			md.MediaType = metadata.MediaType.Value;
			}

		if (metadata.ArtworkMimetype is not null)
			{
			md.ArtworkAvailable = true;
			md.ArtworkMIMEType = metadata.ArtworkMimetype;
			}

		if (metadata.ArtworkIdentifier is not null)
			{
			md.ArtworkIdentifier = metadata.ArtworkIdentifier;
			}

		if (metadata.SeriesName is not null)
			{
			md.SeriesName = metadata.SeriesName;
			}

		if (metadata.SeasonNumber is not null)
			{
			md.SeasonNumber = metadata.SeasonNumber.Value;
			}

		if (metadata.EpisodeNumber is not null)
			{
			md.EpisodeNumber = metadata.EpisodeNumber.Value;
			}

		if (metadata.ContentIdentifier is not null)
			{
			md.ContentIdentifier = metadata.ContentIdentifier;
			}

		if (metadata.ITunesStoreIdentifier is not null)
			{
			md.ITunesStoreIdentifier = metadata.ITunesStoreIdentifier.Value;
			}

		item.Metadata = md;
		_ = identifier;
		}

	private static bool IsClose (double a, double b)
		{
		return Math.Abs (a - b) < 0.0001;
		}

	private static byte[] Concat (params byte[][] arrays)
		{
		int length = 0;
		foreach (var array in arrays)
			{
			length += array.Length;
			}

		var result = new byte[length];
		int offset = 0;
		foreach (var array in arrays)
			{
			Array.Copy (array, 0, result, offset, array.Length);
			offset += array.Length;
			}

		return result;
		}

	private static byte[] CreateSeed ()
		{
		var seed = new byte[32];
		for (int i = 0; i < seed.Length; i++)
			{
			seed[i] = 0xaa;
			}

		return seed;
		}
	}
