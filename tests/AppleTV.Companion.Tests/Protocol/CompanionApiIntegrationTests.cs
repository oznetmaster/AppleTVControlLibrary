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

using NUnit.Framework;

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
[TestFixture]
[FixtureLifeCycle (LifeCycle.InstancePerTestCase)]
public class CompanionApiIntegrationTests
	{
	[Test]
	public async System.Threading.Tasks.Task SendOpackAsyncHonorsCancellationBeforeSending ()
		{
		var protocol = new CompanionProtocol (new CompanionConnection (), new SrpAuthHandler ());
		using var cancellationSource = new System.Threading.CancellationTokenSource ();
		cancellationSource.Cancel ();

		await Assert.ThatAsync (() =>
			protocol.SendOpackAsync (FrameType.E_OPACK, new Dictionary<string, object?> (), cancellationSource.Token), Throws.InstanceOf<System.Threading.Tasks.TaskCanceledException> ());
		}

	// pyatv/protocols/companion/connection.py (connection_lost, exc is not None) — line 161-167 as of
	// pyatv 0.18.0: an unexpected transport failure must be observable by a CompanionApi consumer.
	[Test]
	public void ConnectionClosedFiresWithExceptionWhenConnectionIsFaulted ()
		{
		var device = new FakeCompanionOpackDevice ();
		var api = CreateConnectedApi (device, out CompanionProtocol protocol);
		api.Connect ();

		ConnectionClosedEventArgs? received = null;
		api.ConnectionClosed += (sender, args) => received = args;

		var failure = new InvalidOperationException ("simulated transport failure");
		protocol.AsyncSender = _ => throw failure;

		Assert.Catch<ProtocolException> (() => api.SendHidCommand (down: true, HidCommand.Select));

		Assert.That (received, Is.Not.Null);
		Assert.That (received!.Exception, Is.Not.Null);
		Assert.That (received.Exception, Is.EqualTo (failure));
		}

	// pyatv/protocols/companion/protocol.py has no direct equivalent of Dispose faulting the
	// connection, but CompanionProtocol.Dispose intentionally faults its CompanionConnection with an
	// ObjectDisposedException as a defined teardown signal; CompanionApi must surface that too.
	[Test]
	public void ConnectionClosedFiresOnProtocolDispose ()
		{
		var device = new FakeCompanionOpackDevice ();
		var api = CreateConnectedApi (device, out CompanionProtocol protocol);
		api.Connect ();

		var raised = false;
		Exception? observedException = null;
		api.ConnectionClosed += (sender, args) =>
			{
			raised = true;
			observedException = args.Exception;
			};

		protocol.Dispose ();

		Assert.That (raised, Is.True);
		Assert.That (observedException, Is.InstanceOf<ObjectDisposedException> ());
		}

	[Test]
	public async System.Threading.Tasks.Task ConnectAsyncRunsFullBringUpSequence ()
		{
		var device = new FakeCompanionOpackDevice ();
		CompanionApi api = CreateConnectedApi (device, out _);

		await api.ConnectAsync ();

		Assert.That (api.Sid, Is.Not.EqualTo (0));
		}

	[Test]
	public async System.Threading.Tasks.Task AsyncHidSessionVolumeAndTextOperationsRoundTrip ()
		{
		var device = new FakeCompanionOpackDevice ();
		CompanionApi api = CreateConnectedApi (device, out _);
		await api.ConnectAsync ();

		await api.SendHidCommandAsync (down: true, HidCommand.Select);
		Assert.That (device.PressedButtons, Does.Contain (HidCommand.Select));

		await api.SetVolumeAsync (42.0);
		Assert.That (await api.GetVolumeAsync (), Is.EqualTo (42.0).Within (0.001));

		await api.TextSetAsync ("async text");
		Assert.That (await api.TextGetAsync (), Is.EqualTo ("async text"));

		await api.SessionStopAsync ();
		Assert.That (device.HasSessionStarted, Is.False);
		}

	[Test]
	public async System.Threading.Tasks.Task AsyncSubscriptionAndAttentionStateRoundTrip ()
		{
		var device = new FakeCompanionOpackDevice ();
		device.SetSystemStatus (SystemStatus.Screensaver);
		CompanionApi api = CreateConnectedApi (device, out _);
		await api.ConnectAsync ();

		await api.SubscribeEventAsync ("_iMC");
		await api.UnsubscribeEventAsync ("_iMC");
		Assert.That (await api.FetchAttentionStateAsync (), Is.EqualTo (SystemStatus.Screensaver));
		}

	[Test]
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
		Assert.That (responses.Select (response => ToLong (((Dictionary<object, object?>)response["_c"]!)["requestNumber"])).ToArray (), Is.EquivalentTo (Enumerable.Range (0, 48).Select (value => (long)value).ToArray ()));
		}

	[Test]
	public async Task TouchSwipeAndStatusQueriesCanRunConcurrently ()
		{
		var device = new FakeCompanionOpackDevice ();
		device.SetSystemStatus (SystemStatus.Screensaver);
		CompanionApi api = CreateConnectedApi (device, out _);
		await api.ConnectAsync ();

		Task swipe = Task.Run (async () =>
			{
			for (var x = 0; x <= 1000; x += 100)
				{
				await api.SendHidEventAsync (x, 500, x == 0 ? TouchAction.Press : TouchAction.Hold);
				await Task.Yield ();
				}

			await api.SendHidEventAsync (1000, 500, TouchAction.Release);
			});
		Task<SystemStatus[]> queries = Task.WhenAll (Enumerable.Range (0, 24).Select (_ => api.FetchAttentionStateAsync ()));

		await Task.WhenAll (swipe, queries);
		Assert.That (queries.Result, Is.EqualTo (Enumerable.Repeat (SystemStatus.Screensaver, 24).ToArray ()));
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
			var unpacked = AppleTvControlLibrary.Opack.Opack.Unpack (data, out _);
			if (unpacked is not Dictionary<object, object?> request)
				{
				return;
				}

			Dictionary<object, object?>? response = device.HandleOpackFrame (request);
			if (response is not null)
				{
				var responseFrame = serverConnection.BuildFrame (frameType, AppleTvControlLibrary.Opack.Opack.Pack (response));
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
			var frame = serverConnection.BuildFrame (AppleTvControlLibrary.Connection.FrameType.E_OPACK, AppleTvControlLibrary.Opack.Opack.Pack (eventFrame));
			clientConnection.ReceiveData (frame);
			};

		companionProtocol.AsyncSender = frame =>
			{
			serverConnection.ReceiveData (frame);
			return System.Threading.Tasks.Task.CompletedTask;
			};

		var credentials = new HapCredentials (
			ltpk: [1],
			ltsk: [2],
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
			if (request["_c"] is Dictionary<object, object?> content && content.TryGetValue ("requestNumber", out var requestNumber))
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
			for (var index = responses.Count - 1; index >= 0; index--)
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
			_ => throw new AssertionException ($"Expected an OPACK integer but received {value?.GetType ().FullName ?? "null"}."),
			};
		}

	[Test]
	public void ConnectRunsFullBringUpSequence ()
		{
		var device = new FakeCompanionOpackDevice ();
		var api = CreateConnectedApi (device, out _);

		api.Connect ();

		Assert.That (device.ReceivedSystemInfo, Is.Not.Null);
		Assert.That (device.ReceivedSystemInfo!["_i"], Is.EqualTo ("aabbccddeeff"));
		Assert.That (device.ReceivedSystemInfo!["_pubID"], Is.EqualTo ("00:11:22:33:44:55"));
		Assert.That (device.ReceivedSystemInfo!["model"], Is.EqualTo ("AppleTV14,1"));
		Assert.That (device.ReceivedSystemInfo!["name"], Is.EqualTo ("Living Room"));

		Assert.That (device.HasTouchStarted, Is.True);
		Assert.That (device.HasSessionStarted, Is.True);
		Assert.That (device.ServiceType, Is.EqualTo ("com.apple.tvremoteservices"));
		Assert.That (device.TvRcProtocolVersion, Is.EqualTo ("1.2"));
		Assert.That (device.HasTextInputStarted, Is.True);

		// pyatv/protocols/companion/api.py (self.sid = (remote_sid << 32) — line 224 as of pyatv 0.18.0 | local_sid)
		Assert.That (api.Sid, Is.EqualTo (5555L << 32 | (uint)device.LocalSid));
		}

	[Test]
	public void HidCommandIsDeliveredToDevice ()
		{
		var device = new FakeCompanionOpackDevice ();
		var api = CreateConnectedApi (device, out _);
		api.Connect ();

		api.SendHidCommand (down: true, HidCommand.Select);

		Assert.That (device.PressedButtons, Does.Contain (HidCommand.Select));
		}

	[Test]
	public void FetchAttentionStateReturnsDeviceStatus ()
		{
		var device = new FakeCompanionOpackDevice ();
		device.SetSystemStatus (SystemStatus.Screensaver);
		var api = CreateConnectedApi (device, out _);
		api.Connect ();

		SystemStatus status = api.FetchAttentionState ();

		Assert.That (status, Is.EqualTo (SystemStatus.Screensaver));
		}

	[Test]
	public void SubscribeAndUnsubscribeEventDoNotThrow ()
		{
		var device = new FakeCompanionOpackDevice ();
		var api = CreateConnectedApi (device, out _);
		api.Connect ();

		api.SubscribeEvent ("_iMC");
		api.UnsubscribeEvent ("_iMC");
		}

	[Test]
	public void SessionStopClearsSessionState ()
		{
		var device = new FakeCompanionOpackDevice ();
		var api = CreateConnectedApi (device, out _);
		api.Connect ();

		api.SessionStop ();

		Assert.That (device.HasSessionStarted, Is.False);
		}

	// pyatv/protocols/companion/api.py (mediacontrol_command) — line 395-399 as of pyatv 0.18.0,
	// pyatv/protocols/companion/__init__.py (GetVolume/set_volume) — line 441-467 as of pyatv 0.18.0
	[Test]
	public void SetVolumeThenGetVolumeRoundTrips ()
		{
		var device = new FakeCompanionOpackDevice ();
		var api = CreateConnectedApi (device, out _);
		api.Connect ();

		api.SetVolume (42.0);

		Assert.That (device.Volume, Is.EqualTo (42.0).Within (0.001));
		Assert.That (api.GetVolume (), Is.EqualTo (42.0).Within (0.001));
		}

	// pyatv/protocols/companion/__init__.py (MediaControlFlags.Volume) — line 99 as of pyatv 0.18.0, 439-449 (_handle_control_flag_update)
	[Test]
	public void ToggleMuteSavesAndRestoresVolume ()
		{
		var device = new FakeCompanionOpackDevice ();
		var api = CreateConnectedApi (device, out _);
		api.Connect ();

		api.SetVolume (60.0);
		Assert.That (api.IsVolumeControlSupported, Is.False);

		((ICompanionProtocolListener)api).EventReceived ("_iMC", new Dictionary<object, object?> { { "_mcF", (long)MediaControlCapabilities.Volume } });
		Assert.That (api.IsVolumeControlSupported, Is.True);

		var muted = api.ToggleMute ();
		Assert.That (muted, Is.True);
		Assert.That (device.Volume, Is.EqualTo (0.0).Within (0.001));

		var unmuted = api.ToggleMute ();
		Assert.That (unmuted, Is.False);
		Assert.That (device.Volume, Is.EqualTo (60.0).Within (0.001));
		}

	// pyatv/protocols/companion/__init__.py (CompanionKeyboard.text_get) — line 517-519 as of pyatv 0.18.0
	[Test]
	public void TextGetReturnsInitialRtiText ()
		{
		var device = new FakeCompanionOpackDevice ();
		var api = CreateConnectedApi (device, out _);
		api.Connect ();

		var text = api.TextGet ();

		Assert.That (text, Is.EqualTo ("Fake Companion Keyboard Text"));
		}

	// pyatv/protocols/companion/__init__.py (CompanionKeyboard.text_clear) — line 521-523 as of pyatv 0.18.0
	[Test]
	public void TextClearEmptiesRtiText ()
		{
		var device = new FakeCompanionOpackDevice ();
		var api = CreateConnectedApi (device, out _);
		api.Connect ();

		api.TextClear ();

		Assert.That (device.RtiText, Is.EqualTo (string.Empty));
		}

	// pyatv/protocols/companion/__init__.py (CompanionKeyboard.text_append) — line 525-527 as of pyatv 0.18.0
	[Test]
	public void TextAppendAddsToExistingRtiText ()
		{
		var device = new FakeCompanionOpackDevice ();
		var api = CreateConnectedApi (device, out _);
		api.Connect ();

		api.TextAppend (" more");

		Assert.That (device.RtiText, Is.EqualTo ("Fake Companion Keyboard Text more"));
		}

	// pyatv/protocols/companion/__init__.py (CompanionKeyboard.text_set) — line 529-532 as of pyatv 0.18.0
	[Test]
	public void TextSetReplacesRtiText ()
		{
		var device = new FakeCompanionOpackDevice ();
		var api = CreateConnectedApi (device, out _);
		api.Connect ();

		api.TextSet ("replacement");

		Assert.That (device.RtiText, Is.EqualTo ("replacement"));
		}

	// pyatv/protocols/companion/__init__.py (CompanionKeyboard._handle_text_input) — line 505-510 as of pyatv 0.18.0
	[Test]
	public async System.Threading.Tasks.Task RtiFocusStateChangeRaisesEventAndUpdatesApi ()
		{
		var device = new FakeCompanionOpackDevice ();
		var api = CreateConnectedApi (device, out _);
		api.Connect ();

		Assert.That (api.TextFocusState, Is.EqualTo (KeyboardFocusState.Focused));

		var raised = new System.Threading.Tasks.TaskCompletionSource<object?> (System.Threading.Tasks.TaskCreationOptions.RunContinuationsAsynchronously);
		api.TextFocusStateChanged += (sender, args) => raised.TrySetResult (null);

		device.SetRtiFocusState (KeyboardFocusState.Unfocused);

		await raised.Task;
		Assert.That (api.TextFocusState, Is.EqualTo (KeyboardFocusState.Unfocused));
		}
	}