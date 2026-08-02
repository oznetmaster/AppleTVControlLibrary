// Copyright (c) 2026 Neil Colvin. Licensed under the MIT License.
// See LICENSE file in the repository root for full license text.

using System;
using System.Text;

using AppleTvControlLibrary.Auth;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using Org.BouncyCastle.Crypto.Agreement.Srp;
using Org.BouncyCastle.Crypto.Digests;
using Org.BouncyCastle.Math;
using Org.BouncyCastle.Security;

namespace AppleTV.Companion.Tests.AuthTests;

/// <summary>
/// Validates <see cref="SrpAuthHandler"/> pair-setup key agreement against an independent
/// (RFC 5054 / BouncyCastle) SRP-6a server implementation, to confirm the client-side premaster
/// secret and session key computed from the srptools-derived math in
/// <see cref="SrpAuthHandler"/> agree with a standard SRP server for the same verifier.
/// </summary>
// pyatv/auth/hap_srp.py (step1, step2) — line 131-165 as of pyatv 0.18.0; srptools/context.py, srptools/client.py
[TestClass]
public class SrpAuthHandlerTests
	{
	// pyatv/auth/hap_srp.py (SRPContext("Pair-Setup", str(pin) — line 135 as of pyatv 0.18.0, ...))
	private static readonly byte[] Identity = Encoding.UTF8.GetBytes ("Pair-Setup");

	// pyatv/auth/hap_srp.py (constants.PRIME_3072) — line 21 as of pyatv 0.18.0; mirrors the private field in SrpAuthHandler.
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

	private static readonly BigInteger Generator = BigInteger.ValueOf (5);

	[TestMethod]
	public void Step1AndStep2AgreeWithIndependentSrpServer ()
		{
		const int pin = 1234;
		byte[] password = Encoding.UTF8.GetBytes (pin.ToString (System.Globalization.CultureInfo.InvariantCulture));

		var random = new SecureRandom ();
		byte[] salt = new byte[16];
		random.NextBytes (salt);

		// Server side: generate verifier v = g^x % N for the shared identity/password/salt,
		// exactly as pyatv's srptools-backed device side would (RFC 5054 x computation, which
		// srptools also uses).
		var verifierGenerator = new Srp6VerifierGenerator ();
		verifierGenerator.Init (Prime, Generator, new Sha512Digest ());
		BigInteger verifier = verifierGenerator.GenerateVerifier (salt, Identity, password);

		var server = new Srp6Server ();
		server.Init (Prime, Generator, verifier, new Sha512Digest (), random);
		BigInteger serverPublic = server.GenerateServerCredentials ();

		// Client side: our port.
		var handler = new SrpAuthHandler ();
		handler.Initialize ();
		handler.Step1 (pin);
		(byte[] clientPubKeyBytes, byte[] _) = handler.Step2 (serverPublic.ToByteArrayUnsigned (), salt);

		var clientPublic = new BigInteger (1, clientPubKeyBytes);

		// Server derives its premaster secret/session key from the client's public value.
		BigInteger serverPremaster = server.CalculateSecret (clientPublic);
		var sessionKeyDigest = new Sha512Digest ();
		byte[] premasterBytes = serverPremaster.ToByteArrayUnsigned ();
		sessionKeyDigest.BlockUpdate (premasterBytes, 0, premasterBytes.Length);
		var serverSessionKey = new byte[sessionKeyDigest.GetDigestSize ()];
		sessionKeyDigest.DoFinal (serverSessionKey, 0);

		CollectionAssert.AreEqual (serverSessionKey, handler.SharedKey);
		}
	}
