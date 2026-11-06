// Copyright (c) 2026 Neil Colvin. Licensed under the MIT License.
// See LICENSE file in the repository root for full license text.

using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

using AppleTvControlLibrary.Auth;
using AppleTvControlLibrary.Crypto;
using AppleTvControlLibrary.Mrp.Auth;
using AppleTvControlLibrary.Mrp.FakeDevice;
using AppleTvControlLibrary.Mrp.Protobuf;
using AppleTvControlLibrary.Tlv8;

using NUnit.Framework;

namespace AppleTv.Mrp.Tests.AuthTests;

/// <summary>
/// End-to-end pair-setup + pair-verify test driving <see cref="MrpPairSetupProcedure"/> and
/// <see cref="MrpPairVerifyProcedure"/> against the ported <see cref="FakeMrpDevice"/> server,
/// entirely in-memory (no sockets).
/// </summary>
/// <remarks>
/// Ported behaviorally from <c>pyatv/protocols/mrp/server_auth.py</c> combined with
/// <c>pyatv/protocols/mrp/auth.py</c> (<c>MrpPairSetupProcedure</c> and <c>MrpPairVerifyProcedure</c>),
/// which describe exactly this message sequence.
/// </remarks>
// pyatv/protocols/mrp/auth.py (MrpPairSetupProcedure, MrpPairVerifyProcedure) — line 26-121 as of pyatv 0.18.0
[TestFixture]
[FixtureLifeCycle (LifeCycle.InstancePerTestCase)]
public class MrpPairingIntegrationTests
	{
	[TestCase (false)]
	[TestCase (true)]
	public async Task PairSetupRejectsInvalidOrMissingServerProofBeforeSendingIdentityAsync (bool omit)
		{
		var fake = new FakeMrpDevice ();
		int requests = 0;
		var setup = new MrpPairSetupProcedure (request =>
			{
			ProtocolMessage response = fake.HandleMessage (request);
			if (++requests != 2)
				return Task.FromResult (response);

			Dictionary<int, byte[]> fields = MrpMessages.GetPairingData (response);
			if (omit)
				_ = fields.Remove ((int)TlvValue.Proof);
			else
				fields[(int)TlvValue.Proof][0] ^= 1;
			return Task.FromResult (MrpMessages.CryptoPairing (fields));
			}, new SrpAuthHandler ());

		await setup.StartPairingAsync ();
		await Assert.ThrowsAsync<AuthenticationException> (async () => await setup.FinishPairingAsync (FakeMrpDevice.PIN_CODE));
		Assert.That (requests, Is.EqualTo (2), "Do not disclose the controller identity to an unverified peer.");
		}

	[TestCase (false)]
	[TestCase (true)]
	public async Task PairSetupRejectsInvalidOrMissingAccessorySignatureAsync (bool omit)
		{
		var fake = new FakeMrpDevice ();
		var srp = new SrpAuthHandler ();
		int requests = 0;
		var setup = new MrpPairSetupProcedure (request =>
			{
			ProtocolMessage response = fake.HandleMessage (request);
			if (++requests != 3)
				return Task.FromResult (response);

			// Keep the authenticated encryption valid while damaging the separate signature.
			Dictionary<int, byte[]> fields = MrpMessages.GetPairingData (response);
			byte[] key = SrpAuthHandler.HkdfExpand ("Pair-Setup-Encrypt-Salt", "Pair-Setup-Encrypt-Info", srp.SharedKey);
			byte[] nonce = Encoding.ASCII.GetBytes ("PS-Msg06");
			var cipher = new Chacha20Cipher8ByteNonce (key, key);
			var identity = Tlv8.ReadTlv (cipher.Decrypt (fields[(int)TlvValue.EncryptedData], nonce: nonce));
			if (omit)
				_ = identity.Remove ((int)TlvValue.Signature);
			else
				identity[(int)TlvValue.Signature][0] ^= 1;
			fields[(int)TlvValue.EncryptedData] = cipher.Encrypt (Tlv8.WriteTlv (identity), nonce: nonce);
			return Task.FromResult (MrpMessages.CryptoPairing (fields));
			}, srp);

		await setup.StartPairingAsync ();
		await Assert.ThrowsAsync<AuthenticationException> (async () => await setup.FinishPairingAsync (FakeMrpDevice.PIN_CODE));
		Assert.That (requests, Is.EqualTo (3));
		}

	[TestCase (4, true)]
	[TestCase (3, false)]
	[TestCase (0, false)]
	public async Task PairVerifyRejectsErrorOrMissingFinalAcknowledgementAsync (int state, bool rejected)
		{
		var fake = new FakeMrpDevice ();
		var setup = new MrpPairSetupProcedure (request => Task.FromResult (fake.HandleMessage (request)), new SrpAuthHandler ());
		await setup.StartPairingAsync ();
		HapCredentials credentials = await setup.FinishPairingAsync (FakeMrpDevice.PIN_CODE);
		int requests = 0;
		var verify = new MrpPairVerifyProcedure (request =>
			{
			if (++requests == 1)
				return Task.FromResult (fake.HandleMessage (request));
			var fields = new Dictionary<int, byte[]> ();
			if (state != 0)
				fields[(int)TlvValue.SeqNo] = [(byte)state];
			if (rejected)
				fields[(int)TlvValue.Error] = [2];
			return Task.FromResult (MrpMessages.CryptoPairing (fields));
			}, new SrpAuthHandler (), credentials);

		await Assert.ThrowsAsync<AuthenticationException> (async () => await verify.VerifyCredentialsAsync ());
		Assert.That (requests, Is.EqualTo (2));
		}

	[Test]
	public async Task PairSetupThenPairVerifySucceedsAsync ()
		{
		var device = new FakeMrpDevice ();

		// --- Pair-setup ---
		var pairSetupSrp = new SrpAuthHandler ();
		var pairSetup = new MrpPairSetupProcedure (
			message => Task.FromResult (device.HandleMessage (message)),
			pairSetupSrp);

		await pairSetup.StartPairingAsync ().ConfigureAwait (false);
		HapCredentials credentials = await pairSetup.FinishPairingAsync (FakeMrpDevice.PIN_CODE).ConfigureAwait (false);

		Assert.That (device.HasPaired, Is.True);
		Assert.That (credentials.Type, Is.EqualTo (AuthenticationType.Hap));
		Assert.That (credentials.ClientId, Is.EqualTo (device.PairedClientId));

		// --- Pair-verify ---
		// pyatv/protocols/mrp/protocol.py (SRP_SALT, SRP_OUTPUT_INFO, SRP_INPUT_INFO) — line 25-27 as of pyatv 0.18.0
		var pairVerifySrp = new SrpAuthHandler ();
		var pairVerify = new MrpPairVerifyProcedure (
			message => Task.FromResult (device.HandleMessage (message)),
			pairVerifySrp,
			credentials);

		bool verified = await pairVerify.VerifyCredentialsAsync ().ConfigureAwait (false);
		Assert.That (verified, Is.True);

		(byte[] clientOutputKey, byte[] clientInputKey) = pairVerify.EncryptionKeys (
			MrpProtocolConstants.SrpSalt, MrpProtocolConstants.SrpOutputInfo, MrpProtocolConstants.SrpInputInfo);

		// Unlike Companion, MRP derives the same info string ("Write") on both client and server
		// sides for their respective output keys, so the client's output key equals the server's
		// output key (and likewise for input), rather than being cross-derived
		// (pyatv/protocols/mrp/server_auth.py — line 149-155 as of pyatv 0.18.0).
		Assert.That (device.ServerOutputKey, Is.Not.Null);
		Assert.That (device.ServerInputKey, Is.Not.Null);
		Assert.That (device.ServerOutputKey, Is.EqualTo (clientOutputKey));
		Assert.That (device.ServerInputKey, Is.EqualTo (clientInputKey));
		}
	}
