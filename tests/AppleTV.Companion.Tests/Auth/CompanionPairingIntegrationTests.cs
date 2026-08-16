// Copyright (c) 2026 Neil Colvin. Licensed under the MIT License.
// See LICENSE file in the repository root for full license text.

using System;
using System.Text;

using AppleTvControlLibrary.Auth;
using AppleTvControlLibrary.Connection;
using AppleTvControlLibrary.FakeDevice;
using AppleTvControlLibrary.Tlv8;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AppleTV.Companion.Tests.AuthTests;

/// <summary>
/// End-to-end pair-setup + pair-verify test driving the client-side <see cref="SrpAuthHandler"/>
/// against the ported <see cref="FakeCompanionDevice"/> server, entirely in-memory (no sockets).
/// </summary>
/// <remarks>
/// Ported behaviorally from <c>tests/fake_device/companion.py</c> combined with
/// <c>pyatv/protocols/companion/auth.py</c> (<c>CompanionPairSetupProcedure</c> and
/// <c>CompanionPairVerifyProcedure</c>), which describe exactly this message sequence.
/// </remarks>
// pyatv/protocols/companion/auth.py (CompanionPairSetupProcedure, CompanionPairVerifyProcedure) — line 37-158 as of pyatv 0.18.0
[TestClass]
public class CompanionPairingIntegrationTests
	{
	// pyatv/protocols/companion/protocol.py (SRP_SALT, SRP_OUTPUT_INFO, SRP_INPUT_INFO) — line 40-42 as of pyatv 0.18.0
	private const string SRP_SALT = "";
	private const string SRP_OUTPUT_INFO = "ClientEncrypt-main";
	private const string SRP_INPUT_INFO = "ServerEncrypt-main";

	[TestMethod]
	public void PairSetupThenPairVerifySucceeds ()
		{
		var device = new FakeCompanionDevice ();
		var pairSetupSrp = new SrpAuthHandler ();

		// --- Pair-setup M1 ---
		// pyatv/protocols/companion/auth.py (start_pairing) — line 49-58 as of pyatv 0.18.0
		pairSetupSrp.Initialize ();

		var m1RequestTlv = Tlv8.WriteTlv (new System.Collections.Generic.Dictionary<int, byte[]>
			{
				{ (int)TlvValue.Method, new byte[] { 0 } },
				{ (int)TlvValue.SeqNo, new byte[] { 1 } },
			});
		(FrameType m2FrameType, var m2ResponseTlv) = device.HandleAuthFrame (FrameType.PS_Start, m1RequestTlv);
		Assert.AreEqual (FrameType.PS_Next, m2FrameType);

		var m2 = Tlv8.ReadTlv (m2ResponseTlv);
		var atvSalt = m2[(int)TlvValue.Salt];
		var atvPubKey = m2[(int)TlvValue.PublicKey];

		// --- Pair-setup M3 ---
		// pyatv/protocols/companion/auth.py (finish_pairing, first half) — line 66-90 as of pyatv 0.18.0
		const int pin = FakeCompanionDevice.PIN_CODE;
		pairSetupSrp.Step1 (pin);
		(var clientPubKey, var clientProof) = pairSetupSrp.Step2 (atvPubKey, atvSalt);

		var m3RequestTlv = Tlv8.WriteTlv (new System.Collections.Generic.Dictionary<int, byte[]>
			{
				{ (int)TlvValue.SeqNo, new byte[] { 3 } },
				{ (int)TlvValue.PublicKey, clientPubKey },
				{ (int)TlvValue.Proof, clientProof },
			});
		(FrameType m4FrameType, var m4ResponseTlv) = device.HandleAuthFrame (FrameType.PS_Next, m3RequestTlv);
		Assert.AreEqual (FrameType.PS_Next, m4FrameType);

		var m4 = Tlv8.ReadTlv (m4ResponseTlv);
		Assert.IsFalse (m4.ContainsKey ((int)TlvValue.Error), "Server rejected client SRP proof");

		// --- Pair-setup M5 ---
		// pyatv/protocols/companion/auth.py (finish_pairing, second half) — line 92-100 as of pyatv 0.18.0
		var m5EncryptedData = pairSetupSrp.Step3 (name: "Test Client");
		var m5RequestTlv = Tlv8.WriteTlv (new System.Collections.Generic.Dictionary<int, byte[]>
			{
				{ (int)TlvValue.SeqNo, new byte[] { 5 } },
				{ (int)TlvValue.EncryptedData, m5EncryptedData },
			});
		(FrameType m6FrameType, var m6ResponseTlv) = device.HandleAuthFrame (FrameType.PS_Next, m5RequestTlv);
		Assert.AreEqual (FrameType.PS_Next, m6FrameType);

		var m6 = Tlv8.ReadTlv (m6ResponseTlv);
		var m6EncryptedData = m6[(int)TlvValue.EncryptedData];

		HapCredentials credentials = pairSetupSrp.Step4 (m6EncryptedData);

		Assert.IsTrue (device.HasPaired);
		Assert.AreEqual (AuthenticationType.Hap, credentials.Type);
		CollectionAssert.AreEqual (device.PairedClientId, credentials.ClientId);

		// --- Pair-verify M1/M3 ---
		// pyatv/protocols/companion/auth.py (CompanionPairVerifyProcedure.verify_credentials) — line 120-158 as of pyatv 0.18.0
		var pairVerifySrp = new SrpAuthHandler ();
		(var _, var verifyPubKey) = pairVerifySrp.Initialize ();

		var pv1RequestTlv = Tlv8.WriteTlv (new System.Collections.Generic.Dictionary<int, byte[]>
			{
				{ (int)TlvValue.SeqNo, new byte[] { 1 } },
				{ (int)TlvValue.PublicKey, verifyPubKey },
			});
		(FrameType pv2FrameType, var pv2ResponseTlv) = device.HandleAuthFrame (FrameType.PV_Start, pv1RequestTlv);
		Assert.AreEqual (FrameType.PV_Next, pv2FrameType);

		var pv2 = Tlv8.ReadTlv (pv2ResponseTlv);
		var serverVerifyPubKey = pv2[(int)TlvValue.PublicKey];
		var serverEncryptedData = pv2[(int)TlvValue.EncryptedData];

		var pv3EncryptedData = pairVerifySrp.Verify1 (credentials, serverVerifyPubKey, serverEncryptedData);

		var pv3RequestTlv = Tlv8.WriteTlv (new System.Collections.Generic.Dictionary<int, byte[]>
			{
				{ (int)TlvValue.SeqNo, new byte[] { 3 } },
				{ (int)TlvValue.EncryptedData, pv3EncryptedData },
			});
		(FrameType pv4FrameType, var pv4ResponseTlv) = device.HandleAuthFrame (FrameType.PV_Next, pv3RequestTlv);
		Assert.AreEqual (FrameType.PV_Next, pv4FrameType);

		var pv4 = Tlv8.ReadTlv (pv4ResponseTlv);
		Assert.IsFalse (pv4.ContainsKey ((int)TlvValue.Error), "Server rejected pair-verify signature");
		Assert.IsTrue (device.IsEncrypted, "Server did not enable encryption after pair-verify M3");

		(var clientOutputKey, var clientInputKey) = pairVerifySrp.Verify2 (SRP_SALT, SRP_OUTPUT_INFO, SRP_INPUT_INFO);

		// The client's output key must equal the server's input key and vice versa, since
		// "ClientEncrypt-main" on the client side derives the key the server decrypts with
		// (pyatv/protocols/companion/server_auth.py — line 131-132 as of pyatv 0.18.0).
		Assert.IsNotNull (device.ServerOutputKey);
		Assert.IsNotNull (device.ServerInputKey);
		CollectionAssert.AreEqual (clientOutputKey, device.ServerInputKey);
		CollectionAssert.AreEqual (clientInputKey, device.ServerOutputKey);
		}
	}
