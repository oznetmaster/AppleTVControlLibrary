using System;
using System.Collections.Generic;
using System.Globalization;

using Org.BouncyCastle.Crypto.Agreement;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Signers;
using Org.BouncyCastle.Math.EC.Rfc8032;

using AppleTvControlLibrary.Auth;
using AppleTvControlLibrary.Connection;
using AppleTvControlLibrary.Crypto;
using AppleTvControlLibrary.Tlv8;

namespace AppleTvControlLibrary.FakeDevice;

/// <summary>
/// In-memory fake Companion Apple TV used to validate the client-side pairing/verification
/// implementation without a real device or socket transport.
/// </summary>
/// <remarks>
/// Ported from <c>tests/fake_device/companion.py</c> (<c>FakeCompanionService</c>) and
/// <c>pyatv/protocols/companion/server_auth.py</c> (<c>CompanionServerAuth</c>). Unlike the
/// Python original, this type operates directly on decoded TLV/OPACK dictionaries via
/// <see cref="HandleAuthFrame"/> rather than raw sockets, so it can be driven in-process by a
/// test that also drives the client-side <see cref="SrpAuthHandler"/>.
/// </remarks>
// pyatv/protocols/companion/server_auth.py:66-90 (CompanionServerAuth); tests/fake_device/companion.py:225-283
public sealed class FakeCompanionDevice
	{
	// pyatv/auth/server_auth.py:3
	public const int PIN_CODE = 1111;

	// pyatv/auth/server_auth.py:12
	public const string SERVER_IDENTIFIER = "5D797FD3-3538-427E-A47B-A32FC6CF3A6A";

	// pyatv/auth/server_auth.py:13 (32 * b"\xaa")
	private static readonly byte[] PrivateKeySeed = CreateSeed ();

	private readonly byte[] _uniqueId;
	private readonly FakeCompanionServerKeys _keys;
	private readonly FakeCompanionSrpServer _srpSession;

	private byte[]? _outputKey;
	private byte[]? _inputKey;
	private Chacha20Cipher? _sessionChacha;

	/// <summary>Initializes a new instance of the <see cref="FakeCompanionDevice"/> class.</summary>
	/// <param name="uniqueId">The device identifier reported during pairing. Defaults to <see cref="SERVER_IDENTIFIER"/>.</param>
	/// <param name="pin">The PIN code required to complete pair-setup. Defaults to <see cref="PIN_CODE"/>.</param>
	// pyatv/protocols/companion/server_auth.py:69-75 (CompanionServerAuth.__init__)
	public FakeCompanionDevice (string? uniqueId = null, int pin = PIN_CODE)
		{
		_uniqueId = System.Text.Encoding.UTF8.GetBytes (uniqueId ?? SERVER_IDENTIFIER);
		_keys = FakeCompanionServerKeys.Generate (PrivateKeySeed);
		_srpSession = FakeCompanionSrpServer.Create (pin);
		}

	/// <summary>Gets a value indicating whether pairing has completed successfully.</summary>
	public bool HasPaired
		{
		get;
		private set;
		}

	/// <summary>Gets a value indicating whether the encrypted session channel is active.</summary>
	public bool IsEncrypted => _sessionChacha is not null;

	/// <summary>Gets the key derived by the server to encrypt data sent to the client, set after pair-verify M1. pyatv/protocols/companion/server_auth.py:131</summary>
	public byte[]? ServerOutputKey => _outputKey;

	/// <summary>Gets the key derived by the server to decrypt data from the client, set after pair-verify M1. pyatv/protocols/companion/server_auth.py:132</summary>
	public byte[]? ServerInputKey => _inputKey;

	/// <summary>Handle an incoming auth frame, mirroring the client-driven pair-setup/pair-verify state machine.</summary>
	/// <param name="frameType">The frame type of the incoming message.</param>
	/// <param name="pairingData">The raw bytes of the <c>_pd</c> TLV8 blob.</param>
	/// <returns>The frame type and TLV8 payload (already wrapped in a <c>_pd</c> dict entry) to send back to the client.</returns>
	// pyatv/protocols/companion/server_auth.py:82-90 (handle_auth_frame)
	public (FrameType FrameType, byte[] PairingData) HandleAuthFrame (FrameType frameType, byte[] pairingData)
		{
		var tlv = Tlv8.Tlv8.ReadTlv (pairingData);
		int seqNo = tlv[(int)TlvValue.SeqNo][0];

		bool isVerify = frameType is FrameType.PV_Start or FrameType.PV_Next;

		return (isVerify, seqNo) switch
			{
			(true, 1) => M1Verify (tlv),
			(true, 3) => M3Verify (tlv),
			(false, 1) => M1Setup (tlv),
			(false, 3) => M3Setup (tlv),
			(false, 5) => M5Setup (tlv),
			_ => throw new NotSupportedException ($"seqno {seqNo} (verify={isVerify})"),
			};
		}

	// pyatv/protocols/companion/server_auth.py:92-124 (_m1_verify)
	private (FrameType, byte[]) M1Verify (Dictionary<int, byte[]> pairingData)
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

		byte[] responseTlv = Tlv8.Tlv8.WriteTlv (new Dictionary<int, byte[]>
			{
				{ (int)TlvValue.SeqNo, new byte[] { 2 } },
				{ (int)TlvValue.PublicKey, serverPubKey },
				{ (int)TlvValue.EncryptedData, encrypted },
			});

		// pyatv/protocols/companion/server_auth.py:117-118
		_outputKey = SrpAuthHandler.HkdfExpand ("", "ServerEncrypt-main", shared);
		_inputKey = SrpAuthHandler.HkdfExpand ("", "ClientEncrypt-main", shared);

		return (FrameType.PV_Next, responseTlv);
		}

	// pyatv/protocols/companion/server_auth.py:126-130 (_m3_verify)
	private (FrameType, byte[]) M3Verify (Dictionary<int, byte[]> pairingData)
		{
		_ = pairingData;

		byte[] responseTlv = Tlv8.Tlv8.WriteTlv (new Dictionary<int, byte[]>
			{
				{ (int)TlvValue.SeqNo, new byte[] { 4 } },
			});

		if (_outputKey is not null && _inputKey is not null)
			{
			// pyatv/protocols/companion/connection.py:92 (nonce_length=12)
			_sessionChacha = new Chacha20Cipher (_outputKey, _inputKey, nonceLength: 12);
			}

		return (FrameType.PV_Next, responseTlv);
		}

	// pyatv/protocols/companion/server_auth.py:132-140 (_m1_setup)
	private (FrameType, byte[]) M1Setup (Dictionary<int, byte[]> pairingData)
		{
		_ = pairingData;

		byte[] responseTlv = Tlv8.Tlv8.WriteTlv (new Dictionary<int, byte[]>
			{
				{ (int)TlvValue.SeqNo, new byte[] { 2 } },
				{ (int)TlvValue.Salt, _srpSession.Salt },
				{ (int)TlvValue.PublicKey, PadServerPublic () },
				{ 27, new byte[] { 1 } },
			});

		return (FrameType.PS_Next, responseTlv);
		}

	// pyatv/protocols/companion/server_auth.py:142-155 (_m3_setup)
	private (FrameType, byte[]) M3Setup (Dictionary<int, byte[]> pairingData)
		{
		byte[] clientPublicKey = pairingData[(int)TlvValue.PublicKey];
		_srpSession.ProcessClientPublicKey (clientPublicKey);

		byte[]? serverProof = _srpSession.VerifyClientProofAndGetServerProof (pairingData[(int)TlvValue.Proof]);

		Dictionary<int, byte[]> tlv;
		if (serverProof is not null)
			{
			tlv = new Dictionary<int, byte[]>
				{
					{ (int)TlvValue.Proof, serverProof },
					{ (int)TlvValue.SeqNo, new byte[] { 4 } },
				};
			}
		else
			{
			tlv = new Dictionary<int, byte[]>
				{
					{ (int)TlvValue.Error, new byte[] { (byte)ErrorCode.Authentication } },
					{ (int)TlvValue.SeqNo, new byte[] { 4 } },
				};
			}

		return (FrameType.PS_Next, Tlv8.Tlv8.WriteTlv (tlv));
		}

	// pyatv/protocols/companion/server_auth.py:157-217 (_m5_setup)
	private (FrameType, byte[]) M5Setup (Dictionary<int, byte[]> pairingData)
		{
		byte[] sessionKey = SrpAuthHandler.HkdfExpand ("Pair-Setup-Encrypt-Salt", "Pair-Setup-Encrypt-Info", _srpSession.SessionKey);
		byte[] accessoryDeviceX = SrpAuthHandler.HkdfExpand ("Pair-Setup-Accessory-Sign-Salt", "Pair-Setup-Accessory-Sign-Info", _srpSession.SessionKey);

		var chacha = new Chacha20Cipher8ByteNonce (sessionKey, sessionKey);
		byte[] decryptedTlvBytes = chacha.Decrypt (pairingData[(int)TlvValue.EncryptedData], nonce: System.Text.Encoding.UTF8.GetBytes ("PS-Msg05"));

		var decryptedTlv = Tlv8.Tlv8.ReadTlv (decryptedTlvBytes);
		byte[] clientId = decryptedTlv[(int)TlvValue.Identifier];

		// pyatv/protocols/companion/server_auth.py:199-207: signature over
		// acc_device_x + unique_id + auth_pub
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

		byte[] responseTlv = Tlv8.Tlv8.WriteTlv (new Dictionary<int, byte[]>
			{
				{ (int)TlvValue.SeqNo, new byte[] { 6 } },
				{ (int)TlvValue.EncryptedData, encrypted },
			});

		HasPaired = true;
		PairedClientId = clientId;

		return (FrameType.PS_Next, responseTlv);
		}

	/// <summary>Gets the client identifier learned during the most recent pair-setup, if any.</summary>
	public byte[]? PairedClientId
		{
		get;
		private set;
		}

	private byte[] PadServerPublic ()
		{
		byte[] unpadded = _srpSession.ServerPublic.ToByteArrayUnsigned ();
		return unpadded;
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
