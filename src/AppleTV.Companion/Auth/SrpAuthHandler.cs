using System;
using System.Text;

using Org.BouncyCastle.Crypto.Agreement;
using Org.BouncyCastle.Crypto.Digests;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Signers;
using Org.BouncyCastle.Math;
using Org.BouncyCastle.Math.EC.Rfc8032;
using Org.BouncyCastle.Security;

using AppleTvControlLibrary.Crypto;

namespace AppleTvControlLibrary.Auth;

/// <summary>
/// Handle SRP crypto routines for auth and key derivation.
/// </summary>
/// <remarks>
/// The SRP math here is ported from srptools (the library pyatv itself depends on for its
/// SRP implementation), not from a generic SRP-6a reference, since the two differ in a few
/// details (padding, hash argument ordering) that must match byte-for-byte for the HAP
/// handshake to succeed.
/// </remarks>
// pyatv/auth/hap_srp.py:40-233 (SRPAuthHandler); srptools/context.py, srptools/client.py, srptools/common.py
public sealed class SrpAuthHandler
	{
	// pyatv/auth/hap_srp.py:21 (constants.PRIME_3072, value from srptools/constants.py:34-41)
	private static readonly BigInteger Prime = new BigInteger (
		"FFFFFFFFFFFFFFFFC90FDAA22168C234C4C6628B80DC1CD129024E088A67CC74020BBEA6" +
		"3B139B22514A08798E3404DDEF9519B3CD3A431B302B0A6DF25F14374FE1356D6D51C245" +
		"E485B576625E7EC6F44C42E9A637ED6B0BFF5CB6F406B7EDEE386BFB5A899FA5AE9F2411" +
		"7C4B1FE649286651ECE45B3DC2007CB8A163BF0598DA48361C55D39A69163FA8FD24CF5F" +
		"83655D23DCA3AD961C62F356208552BB9ED529077096966D670C354E4ABC9804F1746C08" +
		"CA18217C32905E462E36CE3BE39E772C180E86039B2783A2EC07A28FB5C55DF06F4C52C9" +
		"DE2BCBF6955817183995497CEA956AE515D2261898FA051015728E5A8AAAC42DAD33170D" +
		"04507A33A85521ABDF1CBA64ECFB850458DBEF0A8AEA71575D060C7DB3970F85A6E1E4C7" +
		"ABF5AE8CDB0933D71E8C94E04A25619DCEE3D2261AD2EE6BF12FFA06D98A0864D8760273" +
		"3EC86A64521F2B18177B200CBBE117577A615D6C770988C0BAD946E208E24FA074E5AB31" +
		"43DB5BFCE0FD108E4B82D120A93AD2CAFFFFFFFFFFFFFFFF", 16);

	// pyatv/auth/hap_srp.py:21 (constants.PRIME_3072_GEN, srptools/constants.py:33)
	private static readonly BigInteger Generator = BigInteger.ValueOf (5);

	// srptools/context.py:39 (self._mult = H(N | PAD(g)))
	private static readonly BigInteger Multiplier = HashAsInt ("", Prime, PadBytes (Generator));

	private const string USER_NAME = "Pair-Setup";

	// pyatv/auth/hap_srp.py:44 (pairing_id = str(uuid.uuid4()).encode())
	private readonly byte[] _pairingId;

	private Ed25519PrivateKeyParameters? _signingKey;
	private byte[]? _authPrivate;
	private byte[]? _authPublic;
	private X25519PrivateKeyParameters? _verifyPrivate;
	private byte[]? _publicBytes;
	private byte[]? _shared;

	private BigInteger? _clientPrivate;
	private BigInteger? _clientPublic;
	private BigInteger? _serverPublic;
	private byte[]? _salt;
	private byte[]? _pin;
	private byte[]? _sessionKeyBytes;
	private byte[]? _keyProof;

	private byte[]? _setupSessionKey;

	/// <summary>Initializes a new instance of the <see cref="SrpAuthHandler"/> class.</summary>
	// pyatv/auth/hap_srp.py:44-53 (__init__)
	public SrpAuthHandler ()
		{
		_pairingId = Encoding.UTF8.GetBytes (Guid.NewGuid ().ToString ());
		}

	/// <summary>Gets the shared secret (SRP session key) established during pair-setup.</summary>
	// pyatv/auth/hap_srp.py:55-58 (shared_key)
	public byte[] SharedKey
		{
		get
			{
			if (_sessionKeyBytes is null)
				{
				throw new InvalidOperationException ("session key not established");
				}

			return _sessionKeyBytes;
			}
		}

	/// <summary>Derives encryption keys from a shared secret using HKDF-SHA512.</summary>
	/// <param name="salt">The HKDF salt string.</param>
	/// <param name="info">The HKDF info string.</param>
	/// <param name="sharedSecret">The shared secret to derive from.</param>
	/// <returns>32 bytes of derived key material.</returns>
	// pyatv/auth/hap_srp.py:29-37 (hkdf_expand)
	public static byte[] HkdfExpand (string salt, string info, byte[] sharedSecret)
		{
		var generator = new HkdfBytesGenerator (new Sha512Digest ());
		generator.Init (new HkdfParameters (
			sharedSecret,
			Encoding.UTF8.GetBytes (salt),
			Encoding.UTF8.GetBytes (info)));

		var output = new byte[32];
		generator.GenerateBytes (output, 0, output.Length);
		return output;
		}

	/// <summary>Initialize operation by generating new keys.</summary>
	/// <returns>A tuple of (auth public key, verify public key).</returns>
	// pyatv/auth/hap_srp.py:66-79 (initialize)
	public (byte[] AuthPublic, byte[] PublicBytes) Initialize ()
		{
		var random = new SecureRandom ();

		_signingKey = new Ed25519PrivateKeyParameters (random);
		_authPrivate = _signingKey.GetEncoded ();
		_authPublic = _signingKey.GeneratePublicKey ().GetEncoded ();

		_verifyPrivate = new X25519PrivateKeyParameters (random);
		_publicBytes = _verifyPrivate.GeneratePublicKey ().GetEncoded ();

		return (_authPublic, _publicBytes);
		}

	/// <summary>First verification step.</summary>
	/// <param name="credentials">The credentials of the device being verified.</param>
	/// <param name="sessionPubKey">The device's X25519 public key.</param>
	/// <param name="encrypted">Encrypted TLV8 payload containing the device identifier and signature.</param>
	/// <returns>Encrypted TLV8 payload to send back to the device.</returns>
	// pyatv/auth/hap_srp.py:81-118 (verify1)
	public byte[] Verify1 (HapCredentials credentials, byte[] sessionPubKey, byte[] encrypted)
		{
		if (_verifyPrivate is null || _publicBytes is null)
			{
			throw new InvalidOperationException ("Initialize must be called first");
			}

		var agreement = new X25519Agreement ();
		agreement.Init (_verifyPrivate);
		var shared = new byte[agreement.AgreementSize];
		agreement.CalculateAgreement (new X25519PublicKeyParameters (sessionPubKey, 0), shared, 0);
		_shared = shared;

		byte[] sessionKey = HkdfExpand ("Pair-Verify-Encrypt-Salt", "Pair-Verify-Encrypt-Info", _shared);

		var chacha = new Chacha20Cipher8ByteNonce (sessionKey, sessionKey);
		byte[] decrypted = chacha.Decrypt (encrypted, nonce: Encoding.UTF8.GetBytes ("PV-Msg02"));
		var decryptedTlv = Tlv8.Tlv8.ReadTlv (decrypted);

		byte[] identifier = decryptedTlv[(int)Tlv8.TlvValue.Identifier];
		byte[] signature = decryptedTlv[(int)Tlv8.TlvValue.Signature];

		if (!BytesEqual (identifier, credentials.AtvId))
			{
			throw new AuthenticationException ("incorrect device response");
			}

		byte[] info = Concat (sessionPubKey, identifier, _publicBytes);
		var ltpk = new Ed25519PublicKeyParameters (credentials.Ltpk, 0);

		var verifier = new Ed25519Signer ();
		verifier.Init (false, ltpk);
		verifier.BlockUpdate (info, 0, info.Length);
		if (!verifier.VerifySignature (signature))
			{
			throw new AuthenticationException ("signature error");
			}

		byte[] deviceInfo = Concat (_publicBytes, credentials.ClientId, sessionPubKey);
		var ltsk = new Ed25519PrivateKeyParameters (credentials.Ltsk, 0);
		byte[] deviceSignature = new byte[Ed25519PrivateKeyParameters.SignatureSize];
		ltsk.Sign (Ed25519.Algorithm.Ed25519, Array.Empty<byte> (), deviceInfo, 0, deviceInfo.Length, deviceSignature, 0);

		byte[] tlv = Tlv8.Tlv8.WriteTlv (new System.Collections.Generic.Dictionary<int, byte[]>
			{
				{ (int)Tlv8.TlvValue.Identifier, credentials.ClientId },
				{ (int)Tlv8.TlvValue.Signature, deviceSignature },
			});

		return chacha.Encrypt (tlv, nonce: Encoding.UTF8.GetBytes ("PV-Msg03"));
		}

	/// <summary>Last verification step. Derives the output and input encryption keys.</summary>
	/// <param name="salt">The HKDF salt string.</param>
	/// <param name="outputInfo">The HKDF info string for the output key.</param>
	/// <param name="inputInfo">The HKDF info string for the input key.</param>
	/// <returns>A tuple of (output key, input key).</returns>
	// pyatv/auth/hap_srp.py:120-129 (verify2)
	public (byte[] OutputKey, byte[] InputKey) Verify2 (string salt, string outputInfo, string inputInfo)
		{
		if (_shared is null)
			{
			throw new InvalidOperationException ("Verify1 must be called first");
			}

		byte[] outputKey = HkdfExpand (salt, outputInfo, _shared);
		byte[] inputKey = HkdfExpand (salt, inputInfo, _shared);
		return (outputKey, inputKey);
		}

	/// <summary>First pairing step. Sets up the SRP client session with the given PIN.</summary>
	/// <param name="pin">The PIN code entered by the user.</param>
	// pyatv/auth/hap_srp.py:131-141 (step1); srptools/client.py:9-24 (SRPClientSession.__init__)
	public void Step1 (int pin)
		{
		if (_authPrivate is null)
			{
			throw new InvalidOperationException ("Initialize must be called first");
			}

		// SRPClientSession(context, binascii.hexlify(self._auth_private).decode())
		// -> self._this_private = int_from_hex(private)
		_clientPrivate = new BigInteger (1, _authPrivate);

		// self._client_public = srp_context.get_client_public(self._this_private)
		_clientPublic = Generator.ModPow (_clientPrivate, Prime);

		_pin = Encoding.UTF8.GetBytes (pin.ToString (System.Globalization.CultureInfo.InvariantCulture));
		}

	/// <summary>Second pairing step. Processes the device's public key and salt.</summary>
	/// <param name="atvPubKey">The device's SRP public key.</param>
	/// <param name="atvSalt">The device's SRP salt.</param>
	/// <returns>A tuple of (client public key, client proof).</returns>
	// pyatv/auth/hap_srp.py:143-155 (step2); srptools/common.py:88-101 (process), srptools/client.py:26-33
	public (byte[] PubKey, byte[] Proof) Step2 (byte[] atvPubKey, byte[] atvSalt)
		{
		if (_clientPrivate is null || _clientPublic is null || _pin is null)
			{
			throw new InvalidOperationException ("Step1 must be called first");
			}

		// init_base(salt): self._salt = unhexlify(salt) -- atvSalt is already raw bytes here.
		_salt = atvSalt;

		// SRPClientSession.init_base: password_hash x = H(salt | H(user ":" password))
		byte[] innerHash = HashAsBytes (":", USER_NAME, Encoding.UTF8.GetString (_pin));
		BigInteger passwordHash = HashAsInt ("", _salt, innerHash);

		// init_common_secret(other_public=B)
		_serverPublic = new BigInteger (1, atvPubKey);
		if (_serverPublic.Mod (Prime).SignValue == 0)
			{
			throw new AuthenticationException ("Wrong public provided for client.");
			}

		// common_secret u = H(PAD(A) | PAD(B))
		BigInteger commonSecret = HashAsInt ("", PadBytes (_clientPublic), PadBytes (_serverPublic));

		// init_session_key: S = (B - (k * g^x)) ^ (a + (u * x)) % N
		BigInteger passwordVerifier = Generator.ModPow (passwordHash, Prime);
		BigInteger baseValue = _serverPublic.Subtract (Multiplier.Multiply (passwordVerifier)).Mod (Prime);
		BigInteger exponent = _clientPrivate.Add (commonSecret.Multiply (passwordHash));
		BigInteger premasterSecret = baseValue.ModPow (exponent, Prime);

		// K = H(S)
		_sessionKeyBytes = HashAsBytes ("", premasterSecret);

		// init_session_key_proof: M = H(H(N) xor H(g), H(user), s, A, B, K)
		BigInteger hashN = HashAsInt ("", Prime);
		BigInteger hashG = HashAsInt ("", Generator);
		BigInteger xored = hashN.Xor (hashG);
		BigInteger hashUser = HashAsInt ("", USER_NAME);
		_keyProof = HashAsBytes ("", xored, hashUser, _salt, _clientPublic, _serverPublic, _sessionKeyBytes);

		return (PadBytes (_clientPublic, 0), _keyProof);
		}

	/// <summary>Third pairing step. Builds the encrypted TLV payload with device identity.</summary>
	/// <param name="name">An optional display name to include.</param>
	/// <returns>The encrypted TLV8 payload.</returns>
	// pyatv/auth/hap_srp.py:167-201 (step3)
	public byte[] Step3 (string? name = null)
		{
		if (_sessionKeyBytes is null || _signingKey is null || _authPublic is null)
			{
			throw new InvalidOperationException ("Step2 must be called first");
			}

		byte[] iosDeviceX = HkdfExpand (
			"Pair-Setup-Controller-Sign-Salt",
			"Pair-Setup-Controller-Sign-Info",
			_sessionKeyBytes);

		_setupSessionKey = HkdfExpand ("Pair-Setup-Encrypt-Salt", "Pair-Setup-Encrypt-Info", _sessionKeyBytes);

		byte[] deviceInfo = Concat (iosDeviceX, _pairingId, _authPublic);
		byte[] deviceSignature = new byte[Ed25519PrivateKeyParameters.SignatureSize];
		_signingKey.Sign (Ed25519.Algorithm.Ed25519, Array.Empty<byte> (), deviceInfo, 0, deviceInfo.Length, deviceSignature, 0);

		var tlv = new System.Collections.Generic.Dictionary<int, byte[]>
			{
				{ (int)Tlv8.TlvValue.Identifier, _pairingId },
				{ (int)Tlv8.TlvValue.PublicKey, _authPublic },
				{ (int)Tlv8.TlvValue.Signature, deviceSignature },
			};

		if (name is not null)
			{
			// pyatv/auth/hap_srp.py:190-194
			tlv[(int)Tlv8.TlvValue.Name] = Opack.Opack.Pack (new System.Collections.Generic.Dictionary<string, object?> { { "name", name } });
			}

		var chacha = new Chacha20Cipher8ByteNonce (_setupSessionKey, _setupSessionKey);
		return chacha.Encrypt (Tlv8.Tlv8.WriteTlv (tlv), nonce: Encoding.UTF8.GetBytes ("PS-Msg05"));
		}

	/// <summary>Last pairing step. Decrypts and parses the final device response.</summary>
	/// <param name="encryptedData">The encrypted TLV8 payload from the device.</param>
	/// <returns>The resulting credentials for the paired device.</returns>
	// pyatv/auth/hap_srp.py:203-233 (step4)
	public HapCredentials Step4 (byte[] encryptedData)
		{
		if (_setupSessionKey is null || _authPrivate is null)
			{
			throw new InvalidOperationException ("Step3 must be called first");
			}

		var chacha = new Chacha20Cipher8ByteNonce (_setupSessionKey, _setupSessionKey);
		byte[] decryptedTlvBytes = chacha.Decrypt (encryptedData, nonce: Encoding.UTF8.GetBytes ("PS-Msg06"));

		if (decryptedTlvBytes.Length == 0)
			{
			throw new AuthenticationException ("data decrypt failed");
			}

		var decryptedTlv = Tlv8.Tlv8.ReadTlv (decryptedTlvBytes);

		byte[] atvIdentifier = decryptedTlv[(int)Tlv8.TlvValue.Identifier];
		byte[] atvPubKey = decryptedTlv[(int)Tlv8.TlvValue.PublicKey];

		// TODO: verify signature here (pyatv/auth/hap_srp.py:230)

		return new HapCredentials (atvPubKey, _authPrivate, atvIdentifier, _pairingId);
		}

	// srptools/context.py:63-67 (pad)
	private static byte[] PadBytes (BigInteger value, int? overridePaddingLength = null)
		{
		int paddingLength = overridePaddingLength ?? ToPyBytes (Prime).Length;
		byte[] unpadded = ToPyBytes (value);
		if (paddingLength == 0 || unpadded.Length >= paddingLength)
			{
			return unpadded;
			}

		var padded = new byte[paddingLength];
		Array.Copy (unpadded, 0, padded, paddingLength - unpadded.Length, unpadded.Length);
		return padded;
		}

	// srptools/utils.py:47-52 (int_to_bytes / hex_from): minimal big-endian bytes, no fixed width.
	private static byte[] ToPyBytes (BigInteger value)
		{
		return value.ToByteArrayUnsigned ();
		}

	// srptools/context.py:69-91 (hash): joiner.join(map(conv, args)) then sha512; as_bytes toggles
	// whether the raw digest or int_from_hex(hexdigest) is returned. Both are represented here as
	// separate helpers since we don't have Python's dynamic typing.
	private static byte[] HashAsBytes (string joiner, params object[] args)
		{
		byte[] joinerBytes = Encoding.UTF8.GetBytes (joiner);
		var digest = new Sha512Digest ();
		bool first = true;
		foreach (object arg in args)
			{
			if (!first && joinerBytes.Length > 0)
				{
				digest.BlockUpdate (joinerBytes, 0, joinerBytes.Length);
				}

			first = false;

			byte[] bytes = Conv (arg);
			digest.BlockUpdate (bytes, 0, bytes.Length);
			}

		var output = new byte[digest.GetDigestSize ()];
		digest.DoFinal (output, 0);
		return output;
		}

	private static BigInteger HashAsInt (string joiner, params object[] args)
		{
		return new BigInteger (1, HashAsBytes (joiner, args));
		}

	private static byte[] Conv (object arg)
		{
		return arg switch
			{
			BigInteger bi => ToPyBytes (bi),
			string s => Encoding.UTF8.GetBytes (s),
			byte[] b => b,
			_ => throw new NotSupportedException (arg.GetType ().ToString ()),
			};
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

	private static bool BytesEqual (byte[] a, byte[] b)
		{
		if (a.Length != b.Length)
			{
			return false;
			}

		for (int i = 0; i < a.Length; i++)
			{
			if (a[i] != b[i])
				{
				return false;
				}
			}

		return true;
		}
	}
