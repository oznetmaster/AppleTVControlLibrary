using Org.BouncyCastle.Crypto.Parameters;

namespace AppleTvControlLibrary.FakeDevice;

/// <summary>
/// Server-side signing/verify key pair used by <see cref="FakeCompanionDevice"/>.
/// </summary>
// pyatv/protocols/companion/server_auth.py:23-24 (ServerKeys namedtuple)
public sealed class FakeCompanionServerKeys
	{
	private FakeCompanionServerKeys (
		Ed25519PrivateKeyParameters sign,
		byte[] auth,
		byte[] authPub,
		X25519PrivateKeyParameters verify,
		X25519PublicKeyParameters verifyPub)
		{
		Sign = sign;
		Auth = auth;
		AuthPub = authPub;
		Verify = verify;
		VerifyPub = verifyPub;
		}

	/// <summary>Gets the Ed25519 signing key. pyatv/protocols/companion/server_auth.py:27</summary>
	public Ed25519PrivateKeyParameters Sign
		{
		get;
		}

	/// <summary>Gets the raw Ed25519 private key bytes. pyatv/protocols/companion/server_auth.py:28-32</summary>
	public byte[] Auth
		{
		get;
		}

	/// <summary>Gets the raw Ed25519 public key bytes. pyatv/protocols/companion/server_auth.py:33-35</summary>
	public byte[] AuthPub
		{
		get;
		}

	/// <summary>Gets the X25519 verify private key. pyatv/protocols/companion/server_auth.py:29</summary>
	public X25519PrivateKeyParameters Verify
		{
		get;
		}

	/// <summary>Gets the X25519 verify public key. pyatv/protocols/companion/server_auth.py:36</summary>
	public X25519PublicKeyParameters VerifyPub
		{
		get;
		}

	/// <summary>Generate server encryption keys from a 32-byte seed.</summary>
	/// <param name="seed">The 32-byte private key seed.</param>
	/// <returns>The generated server keys.</returns>
	// pyatv/protocols/companion/server_auth.py:26-38 (generate_keys)
	public static FakeCompanionServerKeys Generate (byte[] seed)
		{
		var signingKey = new Ed25519PrivateKeyParameters (seed);
		var verifyPrivate = new X25519PrivateKeyParameters (seed);

		return new FakeCompanionServerKeys (
			sign: signingKey,
			auth: signingKey.GetEncoded (),
			authPub: signingKey.GeneratePublicKey ().GetEncoded (),
			verify: verifyPrivate,
			verifyPub: verifyPrivate.GeneratePublicKey ());
		}
	}
