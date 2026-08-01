using System;
using System.Text;

using Org.BouncyCastle.Crypto.Digests;
using Org.BouncyCastle.Math;
using Org.BouncyCastle.Security;

namespace AppleTvControlLibrary.FakeDevice;

/// <summary>
/// Server-side SRP session used by <see cref="FakeCompanionDevice"/> to emulate the Apple TV's
/// half of pair-setup.
/// </summary>
/// <remarks>
/// Ported from srptools (the library pyatv's fake device itself uses via
/// <c>SRPContext</c>/<c>SRPServerSession</c>), not from a generic SRP-6a reference, to keep the
/// same byte-for-byte behavior already validated for the client side in
/// <c>SrpAuthHandler</c>/<c>SrpAuthHandlerTests</c>.
/// </remarks>
// pyatv/protocols/companion/server_auth.py:41-63 (new_server_session);
// srptools/context.py, srptools/server.py, srptools/common.py (SRPServerSession)
public sealed class FakeCompanionSrpServer
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

	private readonly BigInteger _serverPrivate;
	private readonly BigInteger _passwordVerifier;
	private readonly byte[] _salt;

	private BigInteger? _clientPublic;
	private BigInteger? _commonSecret;
	private byte[]? _sessionKeyBytes;

	private FakeCompanionSrpServer (BigInteger serverPrivate, BigInteger passwordVerifier, byte[] salt, BigInteger serverPublic)
		{
		_serverPrivate = serverPrivate;
		_passwordVerifier = passwordVerifier;
		_salt = salt;
		ServerPublic = serverPublic;
		}

	/// <summary>Gets the server's SRP public key (B).</summary>
	public BigInteger ServerPublic
		{
		get;
		}

	/// <summary>Gets the raw salt used for this session.</summary>
	public byte[] Salt => _salt;

	/// <summary>Gets the SRP session key (K), available after <see cref="ProcessClientPublicKey"/>.</summary>
	public byte[] SessionKey
		{
		get
			{
			if (_sessionKeyBytes is null)
				{
				throw new InvalidOperationException ("ProcessClientPublicKey must be called first");
				}

			return _sessionKeyBytes;
			}
		}

	/// <summary>Create a new server session for the given PIN, generating a random salt and server private key.</summary>
	/// <param name="pin">The PIN code the client is expected to provide.</param>
	/// <returns>A new server session.</returns>
	// pyatv/protocols/companion/server_auth.py:41-63 (new_server_session);
	// srptools/context.py:244-254 (get_user_data_triplet); srptools/server.py:13-27 (__init__)
	public static FakeCompanionSrpServer Create (int pin)
		{
		var random = new SecureRandom ();

		// srptools/context.py:250 (salt = self.generate_salt(), bits_salt=128)
		var salt = new byte[16];
		random.NextBytes (salt);

		// srptools/context.py:79-87 (get_common_password_hash): x = H(s | H(I ":" P))
		byte[] innerHash = HashAsBytes (":", USER_NAME, pin.ToString (System.Globalization.CultureInfo.InvariantCulture));
		BigInteger passwordHash = HashAsInt ("", salt, innerHash);

		// srptools/context.py:193-199 (get_common_password_verifier): v = g^x % N
		BigInteger passwordVerifier = Generator.ModPow (passwordHash, Prime);

		// srptools/context.py:159-165 (generate_server_private): b = random()
		var serverPrivateBytes = new byte[128];
		random.NextBytes (serverPrivateBytes);
		BigInteger serverPrivate = new BigInteger (1, serverPrivateBytes);

		// srptools/context.py:174-182 (get_server_public): B = (k*v + g^b) % N
		BigInteger serverPublic = Multiplier.Multiply (passwordVerifier)
			.Add (Generator.ModPow (serverPrivate, Prime))
			.Mod (Prime);

		return new FakeCompanionSrpServer (serverPrivate, passwordVerifier, salt, serverPublic);
		}

	/// <summary>Process the client's public key (A), deriving the shared session key.</summary>
	/// <param name="clientPublicKey">The client's SRP public key.</param>
	// srptools/common.py:117-125 (init_common_secret); srptools/server.py:29-33 (init_session_key)
	public void ProcessClientPublicKey (byte[] clientPublicKey)
		{
		_clientPublic = new BigInteger (1, clientPublicKey);
		if (_clientPublic.Mod (Prime).SignValue == 0)
			{
			throw new AppleTvControlLibrary.Auth.AuthenticationException ("Wrong public provided for server.");
			}

		// u = H(PAD(A) | PAD(B))
		_commonSecret = HashAsInt ("", PadBytes (_clientPublic), PadBytes (ServerPublic));

		// srptools/context.py:167-172 (get_server_premaster_secret): S = (A * v^u) ^ b % N
		BigInteger premasterSecret = _clientPublic
			.Multiply (_passwordVerifier.ModPow (_commonSecret, Prime))
			.Mod (Prime)
			.ModPow (_serverPrivate, Prime);

		// K = H(S)
		_sessionKeyBytes = HashAsBytes ("", premasterSecret);
		}

	/// <summary>Verify the client's proof (M1) and return the server's proof (M2, key_proof_hash).</summary>
	/// <param name="clientProof">The client-provided proof.</param>
	/// <returns>The server's proof to send back, or <see langword="null"/> if verification failed.</returns>
	// srptools/server.py:35-38 (verify_proof); srptools/common.py:127-132 (init_session_key_proof)
	public byte[]? VerifyClientProofAndGetServerProof (byte[] clientProof)
		{
		if (_clientPublic is null || _sessionKeyBytes is null)
			{
			throw new InvalidOperationException ("ProcessClientPublicKey must be called first");
			}

		// M = H(H(N) xor H(g), H(U), s, A, B, K)
		BigInteger hashN = HashAsInt ("", Prime);
		BigInteger hashG = HashAsInt ("", Generator);
		BigInteger xored = hashN.Xor (hashG);
		BigInteger hashUser = HashAsInt ("", USER_NAME);
		byte[] expectedClientProof = HashAsBytes ("", xored, hashUser, _salt, _clientPublic, ServerPublic, _sessionKeyBytes);

		if (!BytesEqual (expectedClientProof, clientProof))
			{
			return null;
			}

		// srptools/context.py:236-238 (get_common_session_key_proof_hash): H(A, M, K)
		return HashAsBytes ("", _clientPublic, expectedClientProof, _sessionKeyBytes);
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

	private static byte[] ToPyBytes (BigInteger value)
		{
		return value.ToByteArrayUnsigned ();
		}

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
