// Copyright (c) 2026 Neil Colvin. Licensed under the MIT License.
// See LICENSE file in the repository root for full license text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

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
	[TestMethod]
	public async System.Threading.Tasks.Task SendOpackAsyncHonorsCancellationBeforeSending ()
		{
		var protocol = new CompanionProtocol (new CompanionConnection (), new SrpAuthHandler ());
		using var cancellationSource = new System.Threading.CancellationTokenSource ();
		cancellationSource.Cancel ();

		await Assert.ThrowsAsync<System.Threading.Tasks.TaskCanceledException> (() =>
			protocol.SendOpackAsync (FrameType.E_OPACK, new Dictionary<string, object?> (), cancellationSource.Token));
		}

	// pyatv/protocols/companion/connection.py (connection_lost, exc is not None) — line 161-167 as of
	// pyatv 0.18.0: an unexpected transport failure must be observable by a CompanionApi consumer.
	[TestMethod]
	public void ConnectionClosedFiresWithExceptionWhenConnectionIsFaulted ()
		{
		var device = new FakeCompanionOpackDevice ();
		var api = CreateConnectedApi (device, out CompanionProtocol protocol);
		api.Connect ();

		ConnectionClosedEventArgs? received = null;
		api.ConnectionClosed += (sender, args) => received = args;

		var failure = new InvalidOperationException ("simulated transport failure");
		protocol.AsyncSender = _ => throw failure;

		Assert.Throws<ProtocolException> (() => api.SendHidCommand (down: true, HidCommand.Select));

		Assert.IsNotNull (received);
		Assert.IsNotNull (received!.Exception);
		Assert.AreEqual (failure, received.Exception);
		}

	// pyatv/protocols/companion/protocol.py has no direct equivalent of Dispose faulting the
	// connection, but CompanionProtocol.Dispose intentionally faults its CompanionConnection with an
	// ObjectDisposedException as a defined teardown signal; CompanionApi must surface that too.
	[TestMethod]
	public void ConnectionClosedFiresOnProtocolDispose ()
		{
		var device = new FakeCompanionOpackDevice ();
		var api = CreateConnectedApi (device, out CompanionProtocol protocol);
		api.Connect ();

		bool raised = false;
		Exception? observedException = null;
		api.ConnectionClosed += (sender, args) =>
			{
			raised = true;
			observedException = args.Exception;
			};

		protocol.Dispose ();

		Assert.IsTrue (raised);
		Assert.IsInstanceOfType<ObjectDisposedException> (observedException);
		}

	[TestMethod]
	public async System.Threading.Tasks.Task ConnectAsyncRunsFullBringUpSequence ()
		{
		var device = new FakeCompanionOpackDevice ();
		CompanionApi api = CreateConnectedApi (device, out _);

		await api.ConnectAsync ();

		Assert.IsTrue (api.Sid != 0);
		}

	[TestMethod]
	public async System.Threading.Tasks.Task AsyncHidSessionVolumeAndTextOperationsRoundTrip ()
		{
		var device = new FakeCompanionOpackDevice ();
		CompanionApi api = CreateConnectedApi (device, out _);
		await api.ConnectAsync ();

		await api.SendHidCommandAsync (down: true, HidCommand.Select);
		Assert.IsTrue (device.PressedButtons.Contains (HidCommand.Select));

		await api.SetVolumeAsync (42.0);
		Assert.AreEqual (42.0, await api.GetVolumeAsync (), 0.001);

		await api.TextSetAsync ("async text");
		Assert.AreEqual ("async text", await api.TextGetAsync ());

		await api.SessionStopAsync ();
		Assert.IsFalse (device.HasSessionStarted);
		}

	[TestMethod]
	public async System.Threading.Tasks.Task AsyncSubscriptionAndAttentionStateRoundTrip ()
		{
		var device = new FakeCompanionOpackDevice ();
		device.SetSystemStatus (SystemStatus.Screensaver);
		CompanionApi api = CreateConnectedApi (device, out _);
		await api.ConnectAsync ();

		await api.SubscribeEventAsync ("_iMC");
		await api.UnsubscribeEventAsync ("_iMC");
		Assert.AreEqual (SystemStatus.Screensaver, await api.FetchAttentionStateAsync ());
		}

	[TestMethod]
	public async Task ConcurrentCommandsCorrelateResponsesByXid ()
		{
		var device = new FakeCompanionOpackDevice ();
		CompanionProtocol protocol = CreateQueuedProtocol (device, out Action deliverResponses);

		Task<Dictionary<object, object?>>[] commands = Enumerable.Range (0, 48)
			.Select (requestNumber => protocol.ExchangeOpackAsync (
				FrameType.E_OPACK,
				new Dictionary<string, object?>
					{
					["_i"] = "FetchAttentionState",
					["_t"] = (int)MessageType.Request,
					["_c"] = new Dictionary<string, object?> { ["requestNumber"] = requestNumber },
					}))
			.ToArray ();
		deliverResponses ();

		Dictionary<object, object?>[] responses = await Task.WhenAll (commands);
		CollectionAssert.AreEquivalent (
			Enumerable.Range (0, 48).Select (value => (long)value).ToArray (),
			responses.Select (response => ToLong (((Dictionary<object, object?>)response["_c"]!)["requestNumber"])).ToArray ());
		}

	[TestMethod]
	public async Task TouchSwipeAndStatusQueriesCanRunConcurrently ()
		{
		var device = new FakeCompanionOpackDevice ();
		device.SetSystemStatus (SystemStatus.Screensaver);
		CompanionApi api = CreateConnectedApi (device, out _);
		await api.ConnectAsync ();

		Task swipe = Task.Run (async () =>
			{
			for (int x = 0; x <= 1000; x += 100)
				{
				await api.SendHidEventAsync (x, 500, x == 0 ? TouchAction.Press : TouchAction.Hold);
				await Task.Yield ();
				}
			await api.SendHidEventAsync (1000, 500, TouchAction.Release);
			});
		Task<SystemStatus[]> queries = Task.WhenAll (Enumerable.Range (0, 24).Select (_ => api.FetchAttentionStateAsync ()));

		await Task.WhenAll (swipe, queries);
		CollectionAssert.AreEqual (Enumerable.Repeat (SystemStatus.Screensaver, 24).ToArray (), queries.Result);
		}

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

		companionProtocol.AsyncSender = frame =>
			{
			serverConnection.ReceiveData (frame);
			return System.Threading.Tasks.Task.CompletedTask;
			};

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

	private static CompanionProtocol CreateQueuedProtocol (FakeCompanionOpackDevice device, out Action deliverResponses)
		{
		var clientConnection = new CompanionConnection ();
		var serverConnection = new CompanionConnection ();
		var protocol = new CompanionProtocol (clientConnection, new SrpAuthHandler ());
		var responses = new List<byte[]> ();

		serverConnection.FrameReceived += (sender, frameType, data) =>
			{
			var request = (Dictionary<object, object?>)AppleTvControlLibrary.Opack.Opack.Unpack (data, out _)!;
			Dictionary<object, object?> response = device.HandleOpackFrame (request)!;
			if (request["_c"] is Dictionary<object, object?> content && content.TryGetValue ("requestNumber", out object? requestNumber))
				{
				((Dictionary<object, object?>)response["_c"]!)["requestNumber"] = requestNumber;
				}
			responses.Add (serverConnection.BuildFrame (frameType, AppleTvControlLibrary.Opack.Opack.Pack (response)));
			};

		protocol.AsyncSender = frame =>
			{
			serverConnection.ReceiveData (frame);
			return Task.CompletedTask;
			};
		deliverResponses = () =>
			{
			for (int index = responses.Count - 1; index >= 0; index--)
				{
				clientConnection.ReceiveData (responses[index]);
				}
			};
		return protocol;
		}

	private static long ToLong (object? value)
		{
		return value switch
			{
			SizedInteger sizedInteger => sizedInteger.Value,
			long number => number,
			int number => number,
			_ => throw new AssertFailedException ($"Expected an OPACK integer but received {value?.GetType ().FullName ?? "null"}."),
			};
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
	public async System.Threading.Tasks.Task RtiFocusStateChangeRaisesEventAndUpdatesApi ()
		{
		var device = new FakeCompanionOpackDevice ();
		var api = CreateConnectedApi (device, out _);
		api.Connect ();

		Assert.AreEqual (KeyboardFocusState.Focused, api.TextFocusState);

		var raised = new System.Threading.Tasks.TaskCompletionSource<object?> (System.Threading.Tasks.TaskCreationOptions.RunContinuationsAsynchronously);
		api.TextFocusStateChanged += (sender, args) => raised.TrySetResult (null);

		device.SetRtiFocusState (KeyboardFocusState.Unfocused);

		await raised.Task;
		Assert.AreEqual (KeyboardFocusState.Unfocused, api.TextFocusState);
		}
	}
