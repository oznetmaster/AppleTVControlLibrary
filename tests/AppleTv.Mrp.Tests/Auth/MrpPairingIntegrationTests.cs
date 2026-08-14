// Copyright (c) 2026 Neil Colvin. Licensed under the MIT License.
// See LICENSE file in the repository root for full license text.

using System.Threading.Tasks;

using AppleTvControlLibrary.Auth;
using AppleTvControlLibrary.Mrp.Auth;
using AppleTvControlLibrary.Mrp.FakeDevice;
using AppleTvControlLibrary.Mrp.Protobuf;

using Microsoft.VisualStudio.TestTools.UnitTesting;

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
[TestClass]
public class MrpPairingIntegrationTests
	{
	[TestMethod]
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

		Assert.IsTrue (device.HasPaired);
		Assert.AreEqual (AuthenticationType.Hap, credentials.Type);
		CollectionAssert.AreEqual (device.PairedClientId, credentials.ClientId);

		// --- Pair-verify ---
		// pyatv/protocols/mrp/protocol.py (SRP_SALT, SRP_OUTPUT_INFO, SRP_INPUT_INFO) — line 25-27 as of pyatv 0.18.0
		var pairVerifySrp = new SrpAuthHandler ();
		var pairVerify = new MrpPairVerifyProcedure (
			message => Task.FromResult (device.HandleMessage (message)),
			pairVerifySrp,
			credentials);

		bool verified = await pairVerify.VerifyCredentialsAsync ().ConfigureAwait (false);
		Assert.IsTrue (verified);

		(byte[] clientOutputKey, byte[] clientInputKey) = pairVerify.EncryptionKeys (
			MrpProtocolConstants.SrpSalt, MrpProtocolConstants.SrpOutputInfo, MrpProtocolConstants.SrpInputInfo);

		// Unlike Companion, MRP derives the same info string ("Write") on both client and server
		// sides for their respective output keys, so the client's output key equals the server's
		// output key (and likewise for input), rather than being cross-derived
		// (pyatv/protocols/mrp/server_auth.py — line 149-155 as of pyatv 0.18.0).
		Assert.IsNotNull (device.ServerOutputKey);
		Assert.IsNotNull (device.ServerInputKey);
		CollectionAssert.AreEqual (clientOutputKey, device.ServerOutputKey);
		CollectionAssert.AreEqual (clientInputKey, device.ServerInputKey);
		}
	}
