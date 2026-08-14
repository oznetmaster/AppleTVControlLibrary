// Copyright (c) 2026 Neil Colvin. Licensed under the MIT License.
// See LICENSE file in the repository root for full license text.

using Org.BouncyCastle.Crypto.Parameters;

namespace AppleTvControlLibrary.Mrp.FakeDevice;

/// <summary>
/// Server-side signing/verify key pair used by <see cref="FakeMrpDevice"/>.
/// </summary>
// pyatv/protocols/mrp/server_auth.py (ServerKeys namedtuple, generate_keys) — line 23-38 as of pyatv 0.18.0
public sealed class FakeMrpServerKeys
	{
	private FakeMrpServerKeys (
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

	/// <summary>Gets the Ed25519 signing key. pyatv/protocols/mrp/server_auth.py — line 27 as of pyatv 0.18.0</summary>
	public Ed25519PrivateKeyParameters Sign
		{
		get;
		}

	/// <summary>Gets the raw Ed25519 private key bytes. pyatv/protocols/mrp/server_auth.py — line 28-32 as of pyatv 0.18.0</summary>
	public byte[] Auth
		{
		get;
		}

	/// <summary>Gets the raw Ed25519 public key bytes. pyatv/protocols/mrp/server_auth.py — line 33-35 as of pyatv 0.18.0</summary>
	public byte[] AuthPub
		{
		get;
		}

	/// <summary>Gets the X25519 verify private key. pyatv/protocols/mrp/server_auth.py — line 29 as of pyatv 0.18.0</summary>
	public X25519PrivateKeyParameters Verify
		{
		get;
		}

	/// <summary>Gets the X25519 verify public key. pyatv/protocols/mrp/server_auth.py — line 36 as of pyatv 0.18.0</summary>
	public X25519PublicKeyParameters VerifyPub
		{
		get;
		}

	/// <summary>Generate server encryption keys from a 32-byte seed.</summary>
	/// <param name="seed">The 32-byte private key seed.</param>
	/// <returns>The generated server keys.</returns>
	// pyatv/protocols/mrp/server_auth.py (generate_keys) — line 26-38 as of pyatv 0.18.0
	public static FakeMrpServerKeys Generate (byte[] seed)
		{
		var signingKey = new Ed25519PrivateKeyParameters (seed);
		var verifyPrivate = new X25519PrivateKeyParameters (seed);

		return new FakeMrpServerKeys (
			sign: signingKey,
			auth: signingKey.GetEncoded (),
			authPub: signingKey.GeneratePublicKey ().GetEncoded (),
			verify: verifyPrivate,
			verifyPub: verifyPrivate.GeneratePublicKey ());
		}
	}
