// Copyright (c) 2026 Neil Colvin. Licensed under the MIT License.
// See LICENSE file in the repository root for full license text.

using System;
using System.Collections.Generic;

using AppleTvControlLibrary.Auth;
using AppleTvControlLibrary.Connection;
using AppleTvControlLibrary.FakeDevice;
using AppleTvControlLibrary.Opack;
using AppleTvControlLibrary.Protocol;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AppleTV.Companion.Tests.ProtocolTests;

/// <summary>
/// End-to-end test driving <see cref="CompanionApi"/> (and, underneath it, <see cref="CompanionProtocol"/>)
/// against <see cref="FakeCompanionOpackDevice"/>, entirely in-memory (no sockets, no encryption --
/// pairing/encryption is validated separately by <c>CompanionPairingIntegrationTests</c>).
/// </summary>
/// <remarks>
/// Ported behaviorally from <c>tests/fake_device/companion.py</c> combined with
/// <c>pyatv/protocols/companion/api.py</c> (<c>CompanionAPI.connect</c>), which describe exactly
/// this bring-up sequence and command surface.
/// </remarks>
// pyatv/protocols/companion/api.py (connect) — line 135-160 as of pyatv 0.18.0; tests/fake_device/companion.py (FakeCompanionService)
[TestClass]
public class CompanionApiIntegrationTests
	{
	// Wires a client-side CompanionConnection/CompanionProtocol pair to a FakeCompanionOpackDevice
	// by looping the framed bytes through a second, "server-side" CompanionConnection used purely
	// for (de)framing (neither side enables encryption, matching how E_OPACK frames are exercised
	// here independently of the PV-established ChaCha20 channel).
	private static CompanionApi CreateConnectedApi (FakeCompanionOpackDevice device, out CompanionProtocol protocol)
		{
		var clientConnection = new CompanionConnection ();
		var serverConnection = new CompanionConnection ();

		var srp = new SrpAuthHandler ();
		var companionProtocol = new CompanionProtocol (clientConnection, srp);

		serverConnection.FrameReceived += (sender, frameType, data) =>
			{
			object? unpacked = AppleTvControlLibrary.Opack.Opack.Unpack (data, out _);
			if (unpacked is not Dictionary<object, object?> request)
				{
				return;
				}

			Dictionary<object, object?>? response = device.HandleOpackFrame (request);
			if (response is not null)
				{
				byte[] responseFrame = serverConnection.BuildFrame (frameType, AppleTvControlLibrary.Opack.Opack.Pack (response));
				clientConnection.ReceiveData (responseFrame);
				}
			};

		// tests/fake_device/companion.py (FakeCompanionState._send_rti) — line 133-134 as of pyatv 0.18.0:
		// unsolicited events pushed by the fake device (e.g. _tiStarted/_tiStopped) must also be
		// framed and delivered to the client connection, since they are not a response to a request.
		device.EventEmitted += (identifier, content) =>
			{
			var eventFrame = new Dictionary<object, object?>
				{
				{ "_i", identifier },
				{ "_t", (int)MessageType.Event },
				{ "_c", content },
				};
			byte[] frame = serverConnection.BuildFrame (AppleTvControlLibrary.Connection.FrameType.E_OPACK, AppleTvControlLibrary.Opack.Opack.Pack (eventFrame));
			clientConnection.ReceiveData (frame);
			};

		companionProtocol.Sender = frame => serverConnection.ReceiveData (frame);

		var credentials = new HapCredentials (
			ltpk: new byte[] { 1 },
			ltsk: new byte[] { 2 },
			atvId: System.Text.Encoding.UTF8.GetBytes ("atv-id"),
			clientId: System.Text.Encoding.UTF8.GetBytes ("client-id"));

		protocol = companionProtocol;
		return new CompanionApi (
			companionProtocol,
			credentials,
			stableIdentifier: "aabbccddeeff",
			deviceId: "00:11:22:33:44:55",
			model: "AppleTV14,1",
			name: "Living Room");
		}

	[TestMethod]
	public void ConnectRunsFullBringUpSequence ()
		{
		var device = new FakeCompanionOpackDevice ();
		var api = CreateConnectedApi (device, out _);

		api.Connect ();

		Assert.IsNotNull (device.ReceivedSystemInfo);
		Assert.AreEqual ("aabbccddeeff", device.ReceivedSystemInfo!["_i"]);
		Assert.AreEqual ("00:11:22:33:44:55", device.ReceivedSystemInfo!["_pubID"]);
		Assert.AreEqual ("AppleTV14,1", device.ReceivedSystemInfo!["model"]);
		Assert.AreEqual ("Living Room", device.ReceivedSystemInfo!["name"]);

		Assert.IsTrue (device.HasTouchStarted);
		Assert.IsTrue (device.HasSessionStarted);
		Assert.AreEqual ("com.apple.tvremoteservices", device.ServiceType);
		Assert.AreEqual ("1.2", device.TvRcProtocolVersion);
		Assert.IsTrue (device.HasTextInputStarted);

		// pyatv/protocols/companion/api.py (self.sid = (remote_sid << 32) — line 224 as of pyatv 0.18.0 | local_sid)
		Assert.AreEqual (5555L << 32 | (uint)device.LocalSid, api.Sid);
		}

	[TestMethod]
	public void HidCommandIsDeliveredToDevice ()
		{
		var device = new FakeCompanionOpackDevice ();
		var api = CreateConnectedApi (device, out _);
		api.Connect ();

		api.SendHidCommand (down: true, HidCommand.Select);

		Assert.IsTrue (device.PressedButtons.Contains (HidCommand.Select));
		}

	[TestMethod]
	public void FetchAttentionStateReturnsDeviceStatus ()
		{
		var device = new FakeCompanionOpackDevice ();
		device.SetSystemStatus (SystemStatus.Screensaver);
		var api = CreateConnectedApi (device, out _);
		api.Connect ();

		SystemStatus status = api.FetchAttentionState ();

		Assert.AreEqual (SystemStatus.Screensaver, status);
		}

	[TestMethod]
	public void SubscribeAndUnsubscribeEventDoNotThrow ()
		{
		var device = new FakeCompanionOpackDevice ();
		var api = CreateConnectedApi (device, out _);
		api.Connect ();

		api.SubscribeEvent ("_iMC");
		api.UnsubscribeEvent ("_iMC");
		}

	[TestMethod]
	public void SessionStopClearsSessionState ()
		{
		var device = new FakeCompanionOpackDevice ();
		var api = CreateConnectedApi (device, out _);
		api.Connect ();

		api.SessionStop ();

		Assert.IsFalse (device.HasSessionStarted);
		}

	// pyatv/protocols/companion/api.py (mediacontrol_command) — line 395-399 as of pyatv 0.18.0,
	// pyatv/protocols/companion/__init__.py (GetVolume/set_volume) — line 441-467 as of pyatv 0.18.0
	[TestMethod]
	public void SetVolumeThenGetVolumeRoundTrips ()
		{
		var device = new FakeCompanionOpackDevice ();
		var api = CreateConnectedApi (device, out _);
		api.Connect ();

		api.SetVolume (42.0);

		Assert.AreEqual (42.0, device.Volume, 0.001);
		Assert.AreEqual (42.0, api.GetVolume (), 0.001);
		}

	// pyatv/protocols/companion/__init__.py (MediaControlFlags.Volume) — line 99 as of pyatv 0.18.0, 439-449 (_handle_control_flag_update)
	[TestMethod]
	public void ToggleMuteSavesAndRestoresVolume ()
		{
		var device = new FakeCompanionOpackDevice ();
		var api = CreateConnectedApi (device, out _);
		api.Connect ();

		api.SetVolume (60.0);
		Assert.IsFalse (api.IsVolumeControlSupported);

		((ICompanionProtocolListener)api).EventReceived ("_iMC", new Dictionary<object, object?> { { "_mcF", (long)MediaControlCapabilities.Volume } });
		Assert.IsTrue (api.IsVolumeControlSupported);

		bool muted = api.ToggleMute ();
		Assert.IsTrue (muted);
		Assert.AreEqual (0.0, device.Volume, 0.001);

		bool unmuted = api.ToggleMute ();
		Assert.IsFalse (unmuted);
		Assert.AreEqual (60.0, device.Volume, 0.001);
		}

	// pyatv/protocols/companion/__init__.py (CompanionKeyboard.text_get) — line 517-519 as of pyatv 0.18.0
	[TestMethod]
	public void TextGetReturnsInitialRtiText ()
		{
		var device = new FakeCompanionOpackDevice ();
		var api = CreateConnectedApi (device, out _);
		api.Connect ();

		string? text = api.TextGet ();

		Assert.AreEqual ("Fake Companion Keyboard Text", text);
		}

	// pyatv/protocols/companion/__init__.py (CompanionKeyboard.text_clear) — line 521-523 as of pyatv 0.18.0
	[TestMethod]
	public void TextClearEmptiesRtiText ()
		{
		var device = new FakeCompanionOpackDevice ();
		var api = CreateConnectedApi (device, out _);
		api.Connect ();

		api.TextClear ();

		Assert.AreEqual (string.Empty, device.RtiText);
		}

	// pyatv/protocols/companion/__init__.py (CompanionKeyboard.text_append) — line 525-527 as of pyatv 0.18.0
	[TestMethod]
	public void TextAppendAddsToExistingRtiText ()
		{
		var device = new FakeCompanionOpackDevice ();
		var api = CreateConnectedApi (device, out _);
		api.Connect ();

		api.TextAppend (" more");

		Assert.AreEqual ("Fake Companion Keyboard Text more", device.RtiText);
		}

	// pyatv/protocols/companion/__init__.py (CompanionKeyboard.text_set) — line 529-532 as of pyatv 0.18.0
	[TestMethod]
	public void TextSetReplacesRtiText ()
		{
		var device = new FakeCompanionOpackDevice ();
		var api = CreateConnectedApi (device, out _);
		api.Connect ();

		api.TextSet ("replacement");

		Assert.AreEqual ("replacement", device.RtiText);
		}

	// pyatv/protocols/companion/__init__.py (CompanionKeyboard._handle_text_input) — line 505-510 as of pyatv 0.18.0
	[TestMethod]
	public void RtiFocusStateChangeRaisesEventAndUpdatesApi ()
		{
		var device = new FakeCompanionOpackDevice ();
		var api = CreateConnectedApi (device, out _);
		api.Connect ();

		Assert.AreEqual (KeyboardFocusState.Focused, api.TextFocusState);

		bool raised = false;
		api.TextFocusStateChanged += (sender, args) => raised = true;

		device.SetRtiFocusState (KeyboardFocusState.Unfocused);

		Assert.IsTrue (raised);
		Assert.AreEqual (KeyboardFocusState.Unfocused, api.TextFocusState);
		}
	}
