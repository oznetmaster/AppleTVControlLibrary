// Copyright (c) 2026 Neil Colvin. Licensed under the MIT License.
// See LICENSE file in the repository root for full license text.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using AppleTvControlLibrary.Auth;
using AppleTvControlLibrary.Text;
using Opack = AppleTvControlLibrary.Opack;

namespace AppleTvControlLibrary.Protocol;

/// <summary>
/// HID command constants.
/// </summary>
// pyatv/protocols/companion/api.py (HidCommand) — line 35-56 as of pyatv 0.18.0
public enum HidCommand
	{
	/// <summary>pyatv/protocols/companion/api.py — line 38 as of pyatv 0.18.0</summary>
	Up = 1,
	/// <summary>pyatv/protocols/companion/api.py — line 39 as of pyatv 0.18.0</summary>
	Down = 2,
	/// <summary>pyatv/protocols/companion/api.py — line 40 as of pyatv 0.18.0</summary>
	Left = 3,
	/// <summary>pyatv/protocols/companion/api.py — line 41 as of pyatv 0.18.0</summary>
	Right = 4,
	/// <summary>pyatv/protocols/companion/api.py — line 42 as of pyatv 0.18.0</summary>
	Menu = 5,
	/// <summary>pyatv/protocols/companion/api.py — line 43 as of pyatv 0.18.0</summary>
	Select = 6,
	/// <summary>pyatv/protocols/companion/api.py — line 44 as of pyatv 0.18.0</summary>
	Home = 7,
	/// <summary>pyatv/protocols/companion/api.py — line 45 as of pyatv 0.18.0</summary>
	VolumeUp = 8,
	/// <summary>pyatv/protocols/companion/api.py — line 46 as of pyatv 0.18.0</summary>
	VolumeDown = 9,
	/// <summary>pyatv/protocols/companion/api.py — line 47 as of pyatv 0.18.0</summary>
	Siri = 10,
	/// <summary>pyatv/protocols/companion/api.py — line 48 as of pyatv 0.18.0</summary>
	Screensaver = 11,
	/// <summary>pyatv/protocols/companion/api.py — line 49 as of pyatv 0.18.0</summary>
	Sleep = 12,
	/// <summary>pyatv/protocols/companion/api.py — line 50 as of pyatv 0.18.0</summary>
	Wake = 13,
	/// <summary>pyatv/protocols/companion/api.py — line 51 as of pyatv 0.18.0</summary>
	PlayPause = 14,
	/// <summary>pyatv/protocols/companion/api.py — line 52 as of pyatv 0.18.0</summary>
	ChannelIncrement = 15,
	/// <summary>pyatv/protocols/companion/api.py — line 53 as of pyatv 0.18.0</summary>
	ChannelDecrement = 16,
	/// <summary>pyatv/protocols/companion/api.py — line 54 as of pyatv 0.18.0</summary>
	Guide = 17,
	/// <summary>pyatv/protocols/companion/api.py — line 55 as of pyatv 0.18.0</summary>
	PageUp = 18,
	/// <summary>pyatv/protocols/companion/api.py — line 56 as of pyatv 0.18.0</summary>
	PageDown = 19,
	}

/// <summary>
/// Media Control command constants, used for playback and volume control (as opposed to the
/// remote-button surface exposed by <see cref="HidCommand"/>).
/// </summary>
// pyatv/protocols/companion/api.py (MediaControlCommand) — line 59-73 as of pyatv 0.18.0
public enum MediaControlCommand
	{
	/// <summary>pyatv/protocols/companion/api.py — line 62 as of pyatv 0.18.0</summary>
	Play = 1,
	/// <summary>pyatv/protocols/companion/api.py — line 63 as of pyatv 0.18.0</summary>
	Pause = 2,
	/// <summary>pyatv/protocols/companion/api.py — line 64 as of pyatv 0.18.0</summary>
	NextTrack = 3,
	/// <summary>pyatv/protocols/companion/api.py — line 65 as of pyatv 0.18.0</summary>
	PreviousTrack = 4,
	/// <summary>pyatv/protocols/companion/api.py — line 66 as of pyatv 0.18.0</summary>
	GetVolume = 5,
	/// <summary>pyatv/protocols/companion/api.py — line 67 as of pyatv 0.18.0</summary>
	SetVolume = 6,
	/// <summary>pyatv/protocols/companion/api.py — line 68 as of pyatv 0.18.0</summary>
	SkipBy = 7,
	/// <summary>pyatv/protocols/companion/api.py — line 69 as of pyatv 0.18.0</summary>
	FastForwardBegin = 8,
	/// <summary>pyatv/protocols/companion/api.py — line 70 as of pyatv 0.18.0</summary>
	FastForwardEnd = 9,
	/// <summary>pyatv/protocols/companion/api.py — line 71 as of pyatv 0.18.0</summary>
	RewindBegin = 10,
	/// <summary>pyatv/protocols/companion/api.py — line 72 as of pyatv 0.18.0</summary>
	RewindEnd = 11,
	/// <summary>pyatv/protocols/companion/api.py — line 73 as of pyatv 0.18.0</summary>
	GetCaptionSettings = 12,
	/// <summary>pyatv/protocols/companion/api.py — line 74 as of pyatv 0.18.0</summary>
	SetCaptionSettings = 13,
	}

#pragma warning restore CS0618

/// <summary>
/// Bitmask flags advertised by the <c>_iMC</c> event (<c>_mcF</c> field), indicating which media
/// controls the currently active app on the device supports. Notably, <see cref="Volume"/> being
/// clear means the device has no Companion-addressable volume/mute control at all (audio is
/// managed over HDMI-CEC instead), so callers must check this before using
/// <see cref="MediaControlCommand.GetVolume"/>/<see cref="MediaControlCommand.SetVolume"/>.
/// </summary>
// pyatv/protocols/companion/__init__.py (MediaControlFlags) — line 87-99 as of pyatv 0.18.0
[Flags]
public enum MediaControlCapabilities
	{
	/// <summary>pyatv/protocols/companion/__init__.py — line 90 as of pyatv 0.18.0</summary>
	NoControls = 0x0000,
	/// <summary>pyatv/protocols/companion/__init__.py — line 91 as of pyatv 0.18.0</summary>
	Play = 0x0001,
	/// <summary>pyatv/protocols/companion/__init__.py — line 92 as of pyatv 0.18.0</summary>
	Pause = 0x0002,
	/// <summary>pyatv/protocols/companion/__init__.py — line 93 as of pyatv 0.18.0</summary>
	NextTrack = 0x0004,
	/// <summary>pyatv/protocols/companion/__init__.py — line 94 as of pyatv 0.18.0</summary>
	PreviousTrack = 0x0008,
	/// <summary>pyatv/protocols/companion/__init__.py — line 95 as of pyatv 0.18.0</summary>
	FastForward = 0x0010,
	/// <summary>pyatv/protocols/companion/__init__.py — line 96 as of pyatv 0.18.0</summary>
	Rewind = 0x0020,
	// 0x0040 and 0x0080 are unused/unknown in pyatv (pyatv/protocols/companion/__init__.py — line 97-98 as of pyatv 0.18.0).
	/// <summary>pyatv/protocols/companion/__init__.py — line 99 as of pyatv 0.18.0</summary>
	Volume = 0x0100,
	/// <summary>pyatv/protocols/companion/__init__.py — line 100 as of pyatv 0.18.0</summary>
	SkipForward = 0x0200,
	/// <summary>pyatv/protocols/companion/__init__.py — line 101 as of pyatv 0.18.0</summary>
	SkipBackward = 0x0400,
	}

/// <summary>
/// Current system state, as returned by <see cref="CompanionApi.FetchAttentionState"/>.
/// </summary>
// pyatv/protocols/companion/api.py (SystemStatus) — line 77-85 as of pyatv 0.18.0
public enum SystemStatus
	{
	/// <summary>Not a valid protocol entry, only used internally. pyatv/protocols/companion/api.py — line 80 as of pyatv 0.18.0</summary>
	Unknown = 0x00,
	/// <summary>pyatv/protocols/companion/api.py — line 82 as of pyatv 0.18.0</summary>
	Asleep = 0x01,
	/// <summary>pyatv/protocols/companion/api.py — line 83 as of pyatv 0.18.0</summary>
	Screensaver = 0x02,
	/// <summary>pyatv/protocols/companion/api.py — line 84 as of pyatv 0.18.0</summary>
	Awake = 0x03,
	/// <summary>Not verified against a real device. pyatv/protocols/companion/api.py — line 85 as of pyatv 0.18.0</summary>
	Idle = 0x04,
	}

/// <summary>
/// Touch action constants.
/// </summary>
// pyatv/const.py (TouchAction) — line 460-466 as of pyatv 0.18.0
public enum TouchAction
	{
	/// <summary>pyatv/const.py — line 463 as of pyatv 0.18.0</summary>
	Press = 1,
	/// <summary>pyatv/const.py — line 464 as of pyatv 0.18.0</summary>
	Hold = 3,
	/// <summary>pyatv/const.py — line 465 as of pyatv 0.18.0</summary>
	Release = 4,
	/// <summary>pyatv/const.py — line 466 as of pyatv 0.18.0</summary>
	Click = 5,
	}

/// <summary>
/// Type of input when pressing a button.
/// </summary>
// pyatv/const.py (InputAction) — line 200-210 as of pyatv 0.18.0
public enum InputAction
	{
	/// <summary>Press and release quickly. pyatv/const.py — line 203 as of pyatv 0.18.0</summary>
	SingleTap = 0,
	/// <summary>Press and release twice quickly. pyatv/const.py — line 206 as of pyatv 0.18.0</summary>
	DoubleTap = 1,
	/// <summary>Press and hold for one second before releasing. pyatv/const.py — line 209 as of pyatv 0.18.0</summary>
	Hold = 2,
	}

/// <summary>
/// All supported keyboard (RTI text input) focus states.
/// </summary>
// pyatv/const.py (KeyboardFocusState) — line 114-124 as of pyatv 0.18.0
public enum KeyboardFocusState
	{
	/// <summary>Keyboard focus state is not determinable. pyatv/const.py — line 117-118 as of pyatv 0.18.0</summary>
	Unknown = 0,
	/// <summary>Keyboard is not focused. pyatv/const.py — line 120-121 as of pyatv 0.18.0</summary>
	Unfocused = 1,
	/// <summary>Keyboard is focused. pyatv/const.py — line 123-124 as of pyatv 0.18.0</summary>
	Focused = 2,
	}

/// <summary>
/// High level implementation of the Companion API: system info, session lifecycle, HID
/// input, media control (volume), attention state, app listing/launching, and account
/// listing/switching.
/// </summary>
/// <remarks>
/// Text input is intentionally out of scope for this port (Companion-only, per the porting
/// brief).
/// </remarks>
// pyatv/protocols/companion/api.py (CompanionAPI, trimmed to WP6 scope) — line 94-475 as of pyatv 0.18.0
public sealed class CompanionApi : ICompanionProtocolListener
	{
	// pyatv/protocols/companion/api.py — line 88-89 as of pyatv 0.18.0
	private const double TOUCHPAD_WIDTH = 1000.0;
	private const double TOUCHPAD_HEIGHT = 1000.0;

	// pyatv/protocols/companion/api.py (com.apple.tvremoteservices) — line 399 as of pyatv 0.18.0
	private const string SESSION_SERVICE_TYPE = "com.apple.tvremoteservices";

	// pyatv/protocols/companion/api.py (["sessionUUID"]) — line 436 as of pyatv 0.18.0
	private static readonly string[] SessionUuidPath = ["sessionUUID"];

	// pyatv/protocols/companion/api.py (["documentState", "docSt", "contextBeforeInput"]) — line 437 as of pyatv 0.18.0
	private static readonly string[] CurrentTextPath = ["documentState", "docSt", "contextBeforeInput"];

	private readonly CompanionProtocol _protocol;
	private readonly HapCredentials _credentials;
	private readonly long _baseTimestamp;
	private readonly List<string> _subscribedEvents = [];

	// pyatv/protocols/companion/__init__.py — line 439 as of pyatv 0.18.0, 448 (self._volume, zeroed when flag absent)
	private MediaControlCapabilities _mediaControlFlags = MediaControlCapabilities.NoControls;
	private double _volume;

	// pyatv/protocols/companion/__init__.py (self._power_state = PowerState.Unknown) — line 213 as of pyatv 0.18.0

	/// <summary>Initializes a new instance of the <see cref="CompanionApi"/> class.</summary>
	/// <param name="protocol">The underlying Companion protocol instance.</param>
	/// <param name="credentials">The paired credentials, used to build the <c>_idsID</c> system info field.</param>
	/// <param name="stableIdentifier">
	/// A stable, persisted identifier for the <c>_systemInfo</c> <c>_i</c> field (typically six
	/// random bytes, hex-encoded, generated once at pair time and never regenerated -- see
	/// porting brief WP6 notes on <c>_i</c>).
	/// </param>
	/// <param name="deviceId">The device identifier reported as <c>_pubID</c>.</param>
	/// <param name="model">The device model string reported in <c>_systemInfo</c>.</param>
	/// <param name="name">The device name string reported in <c>_systemInfo</c>.</param>
	// pyatv/protocols/companion/api.py (__init__) — line 99-107 as of pyatv 0.18.0
	public CompanionApi (
		CompanionProtocol protocol,
		HapCredentials credentials,
		string stableIdentifier,
		string deviceId,
		string model,
		string name)
		{
		_protocol = protocol;
		_credentials = credentials;
		StableIdentifier = stableIdentifier;
		DeviceId = deviceId;
		Model = model;
		Name = name;

		// pyatv/protocols/companion/api.py (self._base_timestamp = time.time_ns() — line 107 as of pyatv 0.18.0)
		_baseTimestamp = DateTime.UtcNow.Ticks * 100;

		// pyatv/protocols/companion/__init__.py (self.api.listen_to("_iMC", ...) — line 436 as of pyatv 0.18.0)
		_protocol.Listener = this;
		_protocol.ConnectionFaulted += (sender, args) => ConnectionClosed?.Invoke (this, args);
		}

	/// <summary>
	/// Raised when the connection to the device is closed or lost, whether cleanly (e.g. the
	/// remote end closing the socket) or unexpectedly (e.g. a transport, decrypt, or dispatch
	/// failure). Inspect <see cref="ConnectionClosedEventArgs.Exception"/> to distinguish the two.
	/// </summary>
	/// <remarks>
	/// Mirrors pyatv's <c>DeviceListener.connection_lost</c>/<c>connection_closed</c> callbacks
	/// (<c>pyatv/interface.py</c>). Unlike pyatv, this port does not implement automatic
	/// reconnection; consumers that want to reconnect must do so themselves in response to this
	/// event.
	/// </remarks>
	public event EventHandler<ConnectionClosedEventArgs>? ConnectionClosed;

	/// <summary>Gets the stable identifier used as the <c>_systemInfo</c> <c>_i</c> field.</summary>
	public string StableIdentifier
		{
		get;
		}

	/// <summary>Gets the device identifier reported as <c>_pubID</c>.</summary>
	public string DeviceId
		{
		get;
		}

	/// <summary>Gets the device model string.</summary>
	public string Model
		{
		get;
		}

	/// <summary>Gets the device name string.</summary>
	public string Name
		{
		get;
		}

	/// <summary>Gets the combined session id established by <see cref="SessionStart"/>.</summary>
	// pyatv/protocols/companion/api.py (self.sid: int = 0) — line 106 as of pyatv 0.18.0
	public long Sid
		{
		get;
		private set;
		}

	/// <summary>
	/// Runs the connection bring-up sequence: system info, touch subscription, session start,
	/// TV Remote Client session start, and text input session start.
	/// </summary>
	// pyatv/protocols/companion/api.py (connect) — line 135-160 as of pyatv 0.18.0, trimmed per porting brief step 5
	// (_systemInfo -> _touchStart -> _sessionStart -> TVRCSessionStart -> _tiStart)
	[Obsolete ("Use ConnectAsync instead.")]
	public void Connect () => ConnectAsync ().ConfigureAwait (false).GetAwaiter ().GetResult ();

	/// <summary>Asynchronously runs the connection bring-up sequence.</summary>
	public async Task ConnectAsync ()
		{
		System.Diagnostics.Debug.WriteLine ("[CompanionApi] Connect: sending _systemInfo");
		await SystemInfoAsync ().ConfigureAwait (false);
		System.Diagnostics.Debug.WriteLine ("[CompanionApi] Connect: sending _touchStart");
		await TouchStartAsync ().ConfigureAwait (false);
		System.Diagnostics.Debug.WriteLine ("[CompanionApi] Connect: sending _sessionStart");
		await SessionStartAsync ().ConfigureAwait (false);
		System.Diagnostics.Debug.WriteLine ("[CompanionApi] Connect: sending TVRCSessionStart");
		await TvRcSessionStartAsync ().ConfigureAwait (false);
		System.Diagnostics.Debug.WriteLine ("[CompanionApi] Connect: sending _tiStart");
		_ = await TextInputStartAsync ().ConfigureAwait (false);
		// pyatv/protocols/companion/__init__.py (self.api.listen_to("_iMC", ...) — line 433-436 as of pyatv 0.18.0):
		// without this the device never reports its media-control capability flags, so
		// IsVolumeControlSupported stays false and volume/mute always fail.
		System.Diagnostics.Debug.WriteLine ("[CompanionApi] Connect: subscribing to _iMC");
		await SubscribeEventAsync ("_iMC").ConfigureAwait (false);

		// pyatv/protocols/companion/__init__.py (CompanionPower.initialize) — line 219-246 as of pyatv 0.18.0: fetch an
		// initial snapshot best-effort (newer tvOS can reply "No request handler" here, which
		// must not prevent subscribing to push updates below), then subscribe to SystemStatus/
		// TVSystemStatus so power state can still be tracked via pushed events afterwards.
		try
			{
			System.Diagnostics.Debug.WriteLine ("[CompanionApi] Connect: sending FetchAttentionState");
			CurrentSystemStatus = await FetchAttentionStateAsync ().ConfigureAwait (false);
			}
		catch (Exception ex)
			{
			System.Diagnostics.Debug.WriteLine ($"[CompanionApi] Connect: FetchAttentionState failed (ignored): {ex}");
			}

		System.Diagnostics.Debug.WriteLine ("[CompanionApi] Connect: subscribing to SystemStatus/TVSystemStatus");
		await SubscribeEventAsync ("SystemStatus").ConfigureAwait (false);
		await SubscribeEventAsync ("TVSystemStatus").ConfigureAwait (false);
		System.Diagnostics.Debug.WriteLine ("[CompanionApi] Connect: bring-up complete");
		}

	// pyatv/protocols/companion/api.py (_send_command) — line 161-185 as of pyatv 0.18.0
	private Dictionary<object, object?> SendCommand (string identifier, Dictionary<string, object?> content, MessageType messageType = MessageType.Request) => SendCommandAsync (identifier, content, messageType).ConfigureAwait (false).GetAwaiter ().GetResult ();

	private async Task<Dictionary<object, object?>> SendCommandAsync (string identifier, Dictionary<string, object?> content, MessageType messageType = MessageType.Request)
		{
		try
			{
			return await _protocol.ExchangeOpackAsync (
				Connection.FrameType.E_OPACK,
				new Dictionary<string, object?>
					{
					{ "_i", identifier },
					{ "_t", (int)messageType },
					{ "_c", content },
					}).ConfigureAwait (false);
			}
		catch (ProtocolException ex)
			{
			throw new ProtocolException ($"Command {identifier} failed: {ex.Message}", ex);
			}
		catch (Exception ex)
			{
			throw new ProtocolException ($"Command {identifier} failed", ex);
			}
		}

	// pyatv/protocols/companion/api.py (_send_event) — line 247-266 as of pyatv 0.18.0
	private void SendEvent (string identifier, Dictionary<string, object?> content) => SendEventAsync (identifier, content).ConfigureAwait (false).GetAwaiter ().GetResult ();

	private async Task SendEventAsync (string identifier, Dictionary<string, object?> content)
		{
		try
			{
			await _protocol.SendOpackAsync (
				Connection.FrameType.E_OPACK,
				new Dictionary<string, object?>
					{
					{ "_i", identifier },
					{ "_t", (int)MessageType.Event },
					{ "_c", content },
					}).ConfigureAwait (false);
			}
		catch (ProtocolException)
			{
			throw;
			}
		catch (Exception ex)
			{
			throw new ProtocolException ("Send event failed", ex);
			}
		}

	/// <summary>Send system information to the device.</summary>
	// pyatv/protocols/companion/api.py (system_info) — line 187-211 as of pyatv 0.18.0
	[Obsolete ("Use SystemInfoAsync instead.")]
	public void SystemInfo () => SystemInfoAsync ().ConfigureAwait (false).GetAwaiter ().GetResult ();

	/// <summary>Asynchronously sends system information to the device.</summary>
	public async Task SystemInfoAsync () =>
		// Bunch of semi-random values here, per pyatv's own comment (api.py:193).
		_ = await SendCommandAsync (
			"_systemInfo",
			new Dictionary<string, object?>
				{
				{ "_bf", 0 },
				{ "_cf", 512 },
				{ "_clFl", 128 },
				{ "_i", StableIdentifier },
				{ "_idsID", _credentials.ClientId },
				{ "_pubID", DeviceId },
				{ "_sf", 256 },
				{ "_sv", "170.18" },
				{ "model", Model },
				{ "name", Name },
				}).ConfigureAwait (false);

	/// <summary>Subscribe to touch gestures.</summary>
	// pyatv/protocols/companion/api.py (_touch_start) — line 464-471 as of pyatv 0.18.0
	[Obsolete ("Use TouchStartAsync instead.")]
	public void TouchStart () => TouchStartAsync ().ConfigureAwait (false).GetAwaiter ().GetResult ();

	/// <summary>Asynchronously subscribes to touch gestures.</summary>
	public async Task TouchStartAsync () => _ = await SendCommandAsync (
			"_touchStart",
			new Dictionary<string, object?>
				{
				{ "_height", TOUCHPAD_HEIGHT },
				{ "_tFl", 0 },
				{ "_width", TOUCHPAD_WIDTH },
				}).ConfigureAwait (false);

	/// <summary>Unsubscribe from touch gestures.</summary>
	// pyatv/protocols/companion/api.py (_touch_stop) — line 473-475 as of pyatv 0.18.0
	[Obsolete ("Use TouchStopAsync instead.")]
	public void TouchStop () => TouchStopAsync ().ConfigureAwait (false).GetAwaiter ().GetResult ();

	/// <summary>Asynchronously unsubscribes from touch gestures.</summary>
	public async Task TouchStopAsync () => _ = await SendCommandAsync ("_touchStop", new Dictionary<string, object?> { { "_i", 1 } }).ConfigureAwait (false);

	/// <summary>Start a Companion session.</summary>
	// pyatv/protocols/companion/api.py (_session_start) — line 213-225 as of pyatv 0.18.0
	// pyatv/protocols/companion/api.py (local_sid = randint(0, 2**32 - 1) — line 214 as of pyatv 0.18.0): must stay
	// within the unsigned 32-bit range, since OPACK's integer packer treats negative values
	// (or any int < 0x28) as a single-byte tag-encoded integer, silently corrupting the frame.
	[Obsolete ("Use SessionStartAsync instead.")]
	public void SessionStart () => SessionStartAsync ().ConfigureAwait (false).GetAwaiter ().GetResult ();

	/// <summary>Asynchronously starts a Companion session.</summary>
	public async Task SessionStartAsync ()
		{
		long localSid = (long)(uint)new Random ().Next (int.MinValue, int.MaxValue);
		Dictionary<object, object?> resp = await SendCommandAsync (
			"_sessionStart",
			new Dictionary<string, object?>
				{
				{ "_srvT", SESSION_SERVICE_TYPE },
				{ "_sid", localSid },
				}).ConfigureAwait (false);

		if (!resp.TryGetValue ("_c", out object? contentObj) || contentObj is not Dictionary<object, object?> content)
			{
			throw new ProtocolException ("missing content");
			}

		long remoteSid = ToLong (content["_sid"]);
		// pyatv/protocols/companion/api.py (self.sid = (remote_sid << 32) — line 224 as of pyatv 0.18.0 | local_sid)
		Sid = (remoteSid << 32) | (uint)localSid;
		}

	/// <summary>Stop the current Companion session.</summary>
	// pyatv/protocols/companion/api.py (_session_stop) — line 241-245 as of pyatv 0.18.0
	[Obsolete ("Use SessionStopAsync instead.")]
	public void SessionStop () => SessionStopAsync ().ConfigureAwait (false).GetAwaiter ().GetResult ();

	/// <summary>Asynchronously stops the current Companion session.</summary>
	public async Task SessionStopAsync () => _ = await SendCommandAsync (
			"_sessionStop",
			new Dictionary<string, object?>
				{
				{ "_srvT", SESSION_SERVICE_TYPE },
				{ "_sid", Sid },
				}).ConfigureAwait (false);

	/// <summary>
	/// Open a TV Remote Client session. tvOS does not answer <c>FetchAttentionState</c> until a
	/// TV Remote Client session is registered with <c>tvremoted</c>; older devices may simply
	/// error on this command, so failures here are intentionally swallowed.
	/// </summary>
	// pyatv/protocols/companion/api.py (_tv_rc_session_start) — line 227-239 as of pyatv 0.18.0
	[Obsolete ("Use TvRcSessionStartAsync instead.")]
	public void TvRcSessionStart () => TvRcSessionStartAsync ().ConfigureAwait (false).GetAwaiter ().GetResult ();

	/// <summary>Asynchronously opens a TV Remote Client session.</summary>
	public async Task TvRcSessionStartAsync ()
		{
		try
			{
			_ = await SendCommandAsync ("TVRCSessionStart", new Dictionary<string, object?> { { "ProtocolVersionKey", "1.2" } }).ConfigureAwait (false);
			}
		catch (Exception ex)
			{
			// pyatv/protocols/companion/api.py — line 238-239 as of pyatv 0.18.0: logged and ignored.
			System.Diagnostics.Debug.WriteLine ($"[CompanionApi] TVRCSessionStart failed (ignored): {ex}");
			}
		}

	/// <summary>Start a text input session.</summary>
	// pyatv/protocols/companion/api.py (_text_input_start) — line 401-404 as of pyatv 0.18.0
	[Obsolete ("Use TextInputStartAsync instead.")]
	public Dictionary<object, object?> TextInputStart () => TextInputStartAsync ().ConfigureAwait (false).GetAwaiter ().GetResult ();

	/// <summary>Asynchronously starts a text input session.</summary>
	public async Task<Dictionary<object, object?>> TextInputStartAsync ()
		{
		Dictionary<object, object?> response = await SendCommandAsync ("_tiStart", []).ConfigureAwait (false);

		// pyatv/protocols/companion/api.py (await asyncio.gather(*self.dispatch("_tiStart", response.get("_c", {})))) — line 404 as of pyatv 0.18.0:
		// _tiStart is a command, but its response content is also dispatched to the same
		// focus-state handler used for the _tiStarted/_tiStopped events.
		Dictionary<object, object?> content = response.TryGetValue ("_c", out object? c) && c is Dictionary<object, object?> dict
			? dict
			: [];
		HandleTextInputFocusUpdate (content);

		return response;
		}

	/// <summary>Stop the current text input session.</summary>
	// pyatv/protocols/companion/api.py (_text_input_stop) — line 406-407 as of pyatv 0.18.0
	[Obsolete ("Use TextInputStopAsync instead.")]
	public void TextInputStop () => TextInputStopAsync ().ConfigureAwait (false).GetAwaiter ().GetResult ();

	/// <summary>Asynchronously stops the current text input session.</summary>
	public async Task TextInputStopAsync () => _ = await SendCommandAsync ("_tiStop", []).ConfigureAwait (false);

	/// <summary>
	/// Gets the current keyboard (RTI text input) focus state, as last reported by the
	/// <c>_tiStarted</c>/<c>_tiStopped</c> events or the <c>_tiStart</c> response.
	/// </summary>
	// pyatv/protocols/companion/__init__.py (CompanionKeyboard.text_focus_state) — line 512-515 as of pyatv 0.18.0
	public KeyboardFocusState TextFocusState
		{
		get;
		private set;
		} = KeyboardFocusState.Unknown;

	/// <summary>
	/// Raised whenever <see cref="TextFocusState"/> is updated by a <c>_tiStarted</c>,
	/// <c>_tiStopped</c> event, or a <c>_tiStart</c> response.
	/// </summary>
	public event EventHandler? TextFocusStateChanged;

	// pyatv/protocols/companion/__init__.py (CompanionKeyboard._handle_text_input) — line 505-510 as of pyatv 0.18.0:
	// focus state is derived from whether _tiD is present in the event/response data, not
	// from a standalone flag.
	private void HandleTextInputFocusUpdate (Dictionary<object, object?> data)
		{
		KeyboardFocusState state = data.ContainsKey ("_tiD") ? KeyboardFocusState.Focused : KeyboardFocusState.Unfocused;
		if (state != TextFocusState)
			{
			TextFocusState = state;
			TextFocusStateChanged?.Invoke (this, EventArgs.Empty);
			}
		}

	/// <summary>
	/// Send a text input command: refreshes the RTI session, optionally clears the current
	/// text, then optionally inserts new text.
	/// </summary>
	/// <param name="text">The text to insert, or an empty string to insert nothing.</param>
	/// <param name="clearPreviousInput">Whether to clear the existing text before inserting.</param>
	/// <returns>The resulting text field contents, or <see langword="null"/> if there is no focused text field.</returns>
	// pyatv/protocols/companion/api.py (text_input_command) — line 421-451 as of pyatv 0.18.0
	[Obsolete ("Use TextInputCommandAsync instead.")]
	public string? TextInputCommand (string text, bool clearPreviousInput = false) => TextInputCommandAsync (text, clearPreviousInput).ConfigureAwait (false).GetAwaiter ().GetResult ();

	/// <summary>Asynchronously sends a text input command.</summary>
	public async Task<string?> TextInputCommandAsync (string text, bool clearPreviousInput = false)
		{
		// pyatv/protocols/companion/api.py (# restart the text input session so that we have up-to-date data) — line 426-428 as of pyatv 0.18.0
		await TextInputStopAsync ().ConfigureAwait (false);
		Dictionary<object, object?> response = await TextInputStartAsync ().ConfigureAwait (false);

		Dictionary<object, object?> content = response.TryGetValue ("_c", out object? c) && c is Dictionary<object, object?> dict
			? dict
			: [];

		// pyatv/protocols/companion/api.py (ti_data = response.get("_c", {}).get("_tiD")) — line 429 as of pyatv 0.18.0
		if (!content.TryGetValue ("_tiD", out object? tiDataObj) || tiDataObj is not byte[] tiData)
			{
			// pyatv/protocols/companion/api.py (if ti_data is None: return None) — line 431-432 as of pyatv 0.18.0
			return null;
			}

		// pyatv/protocols/companion/api.py (keyed_archiver.read_archive_properties) — line 434-438 as of pyatv 0.18.0
		object?[] properties = KeyedArchiver.ReadArchiveProperties (
			tiData,
			SessionUuidPath,
			CurrentTextPath);

		if (properties[0] is not byte[] sessionUuid)
			{
			return null;
			}

		// pyatv/protocols/companion/api.py (if current_text is None: current_text = "") — line 440-441 as of pyatv 0.18.0
		string currentText = properties[1] as string ?? string.Empty;

		if (clearPreviousInput)
			{
			// pyatv/protocols/companion/api.py (self._send_event("_tiC", {"_tiV": 1, "_tiD": get_rti_clear_text_payload(session_uuid)})) — line 443-449 as of pyatv 0.18.0:
			// _tiC is an event, not a command -- it must not go through the request/reply path.
			await SendEventAsync ("_tiC", new Dictionary<string, object?> { { "_tiV", 1 }, { "_tiD", RtiTextOperations.GetRtiClearTextPayload (sessionUuid) } }).ConfigureAwait (false);
			currentText = string.Empty;
			}

		if (!string.IsNullOrEmpty (text))
			{
			// pyatv/protocols/companion/api.py (self._send_event("_tiC", {"_tiV": 1, "_tiD": get_rti_input_text_payload(session_uuid, text)})) — line 451-457 as of pyatv 0.18.0
			await SendEventAsync ("_tiC", new Dictionary<string, object?> { { "_tiV", 1 }, { "_tiD", RtiTextOperations.GetRtiInputTextPayload (sessionUuid, text) } }).ConfigureAwait (false);
			currentText += text;
			}

		return currentText;
		}

	/// <summary>Get the current virtual keyboard text.</summary>
	// pyatv/protocols/companion/__init__.py (CompanionKeyboard.text_get) — line 517-519 as of pyatv 0.18.0
	[Obsolete ("Use TextGetAsync instead.")]
	public string? TextGet () => TextGetAsync ().ConfigureAwait (false).GetAwaiter ().GetResult ();

	/// <summary>Asynchronously gets the current virtual keyboard text.</summary>
	public Task<string?> TextGetAsync () => TextInputCommandAsync (string.Empty, clearPreviousInput: false);

	/// <summary>Clear the virtual keyboard text.</summary>
	// pyatv/protocols/companion/__init__.py (CompanionKeyboard.text_clear) — line 521-523 as of pyatv 0.18.0
	[Obsolete ("Use TextClearAsync instead.")]
	public void TextClear () => TextClearAsync ().ConfigureAwait (false).GetAwaiter ().GetResult ();

	/// <summary>Asynchronously clears the virtual keyboard text.</summary>
	public async Task TextClearAsync () => await TextInputCommandAsync (string.Empty, clearPreviousInput: true).ConfigureAwait (false);

	/// <summary>Append text to the virtual keyboard.</summary>
	/// <param name="text">The text to insert.</param>
	// pyatv/protocols/companion/__init__.py (CompanionKeyboard.text_append) — line 525-527 as of pyatv 0.18.0
	[Obsolete ("Use TextAppendAsync instead.")]
	public void TextAppend (string text) => TextAppendAsync (text).ConfigureAwait (false).GetAwaiter ().GetResult ();

	/// <summary>Asynchronously appends text to the virtual keyboard.</summary>
	public async Task TextAppendAsync (string text) => await TextInputCommandAsync (text, clearPreviousInput: false).ConfigureAwait (false);

	/// <summary>Replace the virtual keyboard text.</summary>
	/// <param name="text">The new text.</param>
	// pyatv/protocols/companion/__init__.py (CompanionKeyboard.text_set) — line 529-531 as of pyatv 0.18.0
	[Obsolete ("Use TextSetAsync instead.")]
	public void TextSet (string text) => TextSetAsync (text).ConfigureAwait (false).GetAwaiter ().GetResult ();

	/// <summary>Asynchronously replaces the virtual keyboard text.</summary>
	public async Task TextSetAsync (string text) => await TextInputCommandAsync (text, clearPreviousInput: true).ConfigureAwait (false);

	/// <summary>Subscribe to updates for an event.</summary>
	/// <param name="eventName">The event identifier to subscribe to.</param>
	// pyatv/protocols/companion/api.py (subscribe_event) — line 267-271 as of pyatv 0.18.0
	[Obsolete ("Use SubscribeEventAsync instead.")]
	public void SubscribeEvent (string eventName) => SubscribeEventAsync (eventName).ConfigureAwait (false).GetAwaiter ().GetResult ();

	/// <summary>Asynchronously subscribes to updates for an event.</summary>
	public async Task SubscribeEventAsync (string eventName)
		{
		if (!_subscribedEvents.Contains (eventName))
			{
			await SendEventAsync ("_interest", new Dictionary<string, object?> { { "_regEvents", new[] { eventName } } }).ConfigureAwait (false);
			_subscribedEvents.Add (eventName);
			}
		}

	/// <summary>Unsubscribe from updates for an event.</summary>
	/// <param name="eventName">The event identifier to unsubscribe from.</param>
	// pyatv/protocols/companion/api.py (unsubscribe_event) — line 273-277 as of pyatv 0.18.0
	[Obsolete ("Use UnsubscribeEventAsync instead.")]
	public void UnsubscribeEvent (string eventName) => UnsubscribeEventAsync (eventName).ConfigureAwait (false).GetAwaiter ().GetResult ();

	/// <summary>Asynchronously unsubscribes from updates for an event.</summary>
	public async Task UnsubscribeEventAsync (string eventName)
		{
		if (_subscribedEvents.Contains (eventName))
			{
			await SendEventAsync ("_interest", new Dictionary<string, object?> { { "_deregEvents", new[] { eventName } } }).ConfigureAwait (false);
			_ = _subscribedEvents.Remove (eventName);
			}
		}

	/// <summary>Send a HID command.</summary>
	/// <param name="down"><see langword="true"/> for a button-down event, <see langword="false"/> for button-up.</param>
	/// <param name="command">The button being pressed or released.</param>
	// pyatv/protocols/companion/api.py (hid_command) — line 305-309 as of pyatv 0.18.0
	[Obsolete ("Use SendHidCommandAsync instead.")]
	public void SendHidCommand (bool down, HidCommand command) => SendHidCommandAsync (down, command).ConfigureAwait (false).GetAwaiter ().GetResult ();

	/// <summary>Asynchronously sends a HID command.</summary>
	public async Task SendHidCommandAsync (bool down, HidCommand command) => _ = await SendCommandAsync (
			"_hidC",
			new Dictionary<string, object?>
				{
				{ "_hBtS", down ? 1 : 2 },
				{ "_hidC", (int)command },
				}).ConfigureAwait (false);

	/// <summary>Send a touch event.</summary>
	/// <param name="x">The x coordinate, in the range [0, 1000].</param>
	/// <param name="y">The y coordinate, in the range [0, 1000].</param>
	/// <param name="mode">The touch phase.</param>
	// pyatv/protocols/companion/api.py (hid_event) — line 311-326 as of pyatv 0.18.0
	[Obsolete ("Use SendHidEventAsync instead.")]
	public void SendHidEvent (int x, int y, TouchAction mode) => SendHidEventAsync (x, y, mode).ConfigureAwait (false).GetAwaiter ().GetResult ();

	/// <summary>Asynchronously sends a touch event.</summary>
	public async Task SendHidEventAsync (int x, int y, TouchAction mode)
		{
		x = Math.Max (x, 0);
		y = Math.Max (y, 0);
		x = Math.Min (x, (int)TOUCHPAD_WIDTH);
		y = Math.Min (y, (int)TOUCHPAD_HEIGHT);

		await SendEventAsync (
			"_hidT",
			new Dictionary<string, object?>
				{
				{ "_ns", (DateTime.UtcNow.Ticks * 100) - _baseTimestamp },
				{ "_tFg", 1 },
				{ "_cx", x },
				{ "_tPh", (int)mode },
				{ "_cy", y },
				}).ConfigureAwait (false);
		}

	/// <summary>
	/// Send a touch click (tap on the touch surface, distinct from a directional-pad
	/// <see cref="HidCommand.Select"/> press). This is what pyatv's remote-widget "select"
	/// gesture actually sends when driven from a touchpad rather than a D-pad: a
	/// <see cref="HidCommand.Select"/> button press/release (button code 6) followed by a
	/// touch <see cref="TouchAction.Click"/> event in the bottom-right corner of the touch
	/// surface.
	/// </summary>
	/// <param name="action">The click gesture: single tap, double tap, or press-and-hold.</param>
	// pyatv/protocols/companion/api.py (click) — line 373-393 as of pyatv 0.18.0
	[Obsolete ("Use SendClickAsync instead.")]
	public void SendClick (InputAction action) => SendClickAsync (action).ConfigureAwait (false).GetAwaiter ().GetResult ();

	/// <summary>Asynchronously sends a touch click.</summary>
	public async Task SendClickAsync (InputAction action)
		{
		if (action is InputAction.SingleTap or InputAction.DoubleTap)
			{
			int count = action == InputAction.SingleTap ? 1 : 2;
			for (int i = 0; i < count; i++)
				{
				_ = await SendCommandAsync ("_hidC", new Dictionary<string, object?> { { "_hBtS", 1 }, { "_hidC", (int)HidCommand.Select } }).ConfigureAwait (false);
				await Task.Delay (20).ConfigureAwait (false);
				_ = await SendCommandAsync ("_hidC", new Dictionary<string, object?> { { "_hBtS", 2 }, { "_hidC", (int)HidCommand.Select } }).ConfigureAwait (false);
				await SendHidEventAsync ((int)TOUCHPAD_WIDTH, (int)TOUCHPAD_HEIGHT, TouchAction.Click).ConfigureAwait (false);
				}
			}
		else // Hold
			{
			_ = await SendCommandAsync ("_hidC", new Dictionary<string, object?> { { "_hBtS", 1 }, { "_hidC", (int)HidCommand.Select } }).ConfigureAwait (false);
			await Task.Delay (1000).ConfigureAwait (false);
			_ = await SendCommandAsync ("_hidC", new Dictionary<string, object?> { { "_hBtS", 2 }, { "_hidC", (int)HidCommand.Select } }).ConfigureAwait (false);
			await SendHidEventAsync ((int)TOUCHPAD_WIDTH, (int)TOUCHPAD_HEIGHT, TouchAction.Click).ConfigureAwait (false);
			}
		}

	/// <summary>Fetch the current attention state (system status) from the device.</summary>
	/// <returns>The current <see cref="SystemStatus"/>.</returns>
	// pyatv/protocols/companion/api.py (fetch_attention_state) — line 454-462 as of pyatv 0.18.0
	[Obsolete ("Use FetchAttentionStateAsync instead.")]
	public SystemStatus FetchAttentionState () => FetchAttentionStateAsync ().ConfigureAwait (false).GetAwaiter ().GetResult ();

	/// <summary>Asynchronously fetches the current attention state.</summary>
	public async Task<SystemStatus> FetchAttentionStateAsync ()
		{
		Dictionary<object, object?> resp = await SendCommandAsync ("FetchAttentionState", []).ConfigureAwait (false);

		if (!resp.TryGetValue ("_c", out object? contentObj) || contentObj is not Dictionary<object, object?> content)
			{
			throw new ProtocolException ("missing content");
			}

		long state = ToLong (content["state"]);
		return (SystemStatus)state;
		}

	/// <summary>
	/// Launch an app on the device, by bundle identifier or by URL/URL scheme (for deep-linking
	/// into content rather than opening an app cold).
	/// </summary>
	/// <param name="bundleIdOrUrl">
	/// A bundle identifier (e.g. <c>com.apple.TVWatchList</c>), or a URL/URL scheme to open.
	/// </param>
	// pyatv/protocols/companion/api.py (launch_app) — line 279-289 as of pyatv 0.18.0
	// pyatv/support/url.py (is_url_or_scheme, bool(urlparse(url).scheme)) — line 11-14 as of pyatv 0.18.0
	[Obsolete ("Use LaunchAppAsync instead.")]
	public void LaunchApp (string bundleIdOrUrl) => LaunchAppAsync (bundleIdOrUrl).ConfigureAwait (false).GetAwaiter ().GetResult ();

	/// <summary>Asynchronously launches an app on the device.</summary>
	public async Task LaunchAppAsync (string bundleIdOrUrl)
		{
		string launchCommandKey = Uri.TryCreate (bundleIdOrUrl, UriKind.Absolute, out Uri? uri) && !string.IsNullOrEmpty (uri.Scheme)
			? "_urlS"
			: "_bundleID";

		_ = await SendCommandAsync (
			"_launchApp",
			new Dictionary<string, object?>
				{
				{ launchCommandKey, bundleIdOrUrl },
				}).ConfigureAwait (false);
		}

	/// <summary>
	/// Fetch the list of launchable apps on the device, as a mapping of bundle identifier to
	/// display name.
	/// </summary>
	/// <returns>
	/// A mapping of bundle identifier to display name. This is the only Companion feature with a
	/// confirmed history of returning empty on some tvOS point releases, so callers must treat an
	/// empty (or missing) result as a normal outcome rather than an error and must not hard-depend
	/// on the list being non-empty.
	/// </returns>
	// pyatv/protocols/companion/api.py (app_list, FetchLaunchableApplicationsEvent) — line 291-293 as of pyatv 0.18.0
	// pyatv/protocols/companion/__init__.py (CompanionApps.app_list, content.items()) — line 168-175 as of pyatv 0.18.0
	[Obsolete ("Use AppListAsync instead.")]
	public Dictionary<string, string> AppList () => AppListAsync ().ConfigureAwait (false).GetAwaiter ().GetResult ();

	/// <summary>Asynchronously fetches the list of launchable apps.</summary>
	public async Task<Dictionary<string, string>> AppListAsync ()
		{
		Dictionary<object, object?> resp = await SendCommandAsync ("FetchLaunchableApplicationsEvent", []).ConfigureAwait (false);

		Dictionary<string, string> apps = [];
		if (!resp.TryGetValue ("_c", out object? contentObj) || contentObj is not Dictionary<object, object?> content)
			{
			return apps;
			}

		foreach (KeyValuePair<object, object?> kvp in content)
			{
			if (kvp.Key is string bundleId && kvp.Value is string displayName)
				{
				apps[bundleId] = displayName;
				}
			}

		return apps;
		}

	/// <summary>
	/// Fetch the list of user accounts that can be switched to on the device, as a mapping of
	/// account identifier to display name.
	/// </summary>
	/// <returns>
	/// A mapping of account identifier to display name. Like <see cref="AppList"/>, this is not
	/// guaranteed to be populated on every device/tvOS combination, so callers must treat an
	/// empty (or missing) result as a normal outcome rather than an error.
	/// </returns>
	// pyatv/protocols/companion/api.py (account_list, FetchUserAccountsEvent) — line 301-303 as of pyatv 0.18.0
	// pyatv/protocols/companion/__init__.py (CompanionUserAccounts.account_list, content.items()) — line 190-197 as of pyatv 0.18.0
	[Obsolete ("Use AccountListAsync instead.")]
	public Dictionary<string, string> AccountList () => AccountListAsync ().ConfigureAwait (false).GetAwaiter ().GetResult ();

	/// <summary>Asynchronously fetches the list of accounts.</summary>
	public async Task<Dictionary<string, string>> AccountListAsync ()
		{
		Dictionary<object, object?> resp = await SendCommandAsync ("FetchUserAccountsEvent", []).ConfigureAwait (false);

		Dictionary<string, string> accounts = [];
		if (!resp.TryGetValue ("_c", out object? contentObj) || contentObj is not Dictionary<object, object?> content)
			{
			return accounts;
			}

		foreach (KeyValuePair<object, object?> kvp in content)
			{
			if (kvp.Key is string accountId && kvp.Value is string displayName)
				{
				accounts[accountId] = displayName;
				}
			}

		return accounts;
		}

	/// <summary>
	/// Diagnostic-only: fetch the raw, untyped <c>_c</c> content of a <c>FetchUserAccountsEvent</c>
	/// response, with no coercion to <see cref="Dictionary{TKey, TValue}"/> of
	/// <see cref="string"/>/<see cref="string"/>. <see cref="AccountList"/> silently drops any
	/// entry whose value is not a plain string, which would hide a richer per-account payload
	/// (e.g. a nested dict carrying a "current"/"active" flag) if the device ever sends one. This
	/// exists to let that be checked against real hardware rather than assumed from the pyatv
	/// source, per the brief's rule 2 (do not invent, do not infer past what the source says).
	/// </summary>
	/// <returns>The raw <c>_c</c> content, or an empty dictionary if missing/malformed.</returns>
	[Obsolete ("Use AccountListRawAsync instead.")]
	public Dictionary<object, object?> AccountListRaw () => AccountListRawAsync ().ConfigureAwait (false).GetAwaiter ().GetResult ();

	/// <summary>Asynchronously fetches the raw account list response content.</summary>
	public async Task<Dictionary<object, object?>> AccountListRawAsync ()
		{
		Dictionary<object, object?> resp = await SendCommandAsync ("FetchUserAccountsEvent", []).ConfigureAwait (false);

		return !resp.TryGetValue ("_c", out object? contentObj) || contentObj is not Dictionary<object, object?> content
			? []
			: content;
		}

	/// <summary>Switch the active user account on the device.</summary>
	/// <param name="accountId">The account identifier to switch to, as returned by <see cref="AccountList"/>.</param>
	// pyatv/protocols/companion/api.py (switch_account, SwitchUserAccountEvent/SwitchAccountID) — line 295-299 as of pyatv 0.18.0
	[Obsolete ("Use SwitchAccountAsync instead.")]
	public void SwitchAccount (string accountId) => SwitchAccountAsync (accountId).ConfigureAwait (false).GetAwaiter ().GetResult ();

	/// <summary>Asynchronously switches the active user account.</summary>
	public async Task SwitchAccountAsync (string accountId) => _ = await SendCommandAsync (
			"SwitchUserAccountEvent",
			new Dictionary<string, object?>
				{
				{ "SwitchAccountID", accountId },
				}).ConfigureAwait (false);

	/// <summary>Send a media control command to the device.</summary>
	/// <param name="command">The media control command to send.</param>
	/// <param name="args">Additional command-specific arguments, if any.</param>
	/// <returns>The decoded response content (the message's <c>_c</c> field).</returns>
	// pyatv/protocols/companion/api.py (mediacontrol_command) — line 395-399 as of pyatv 0.18.0
	[Obsolete ("Use MediaControlCommandAsync instead.")]
	public Dictionary<object, object?> MediaControlCommand (MediaControlCommand command, Dictionary<string, object?>? args = null) => MediaControlCommandAsync (command, args).ConfigureAwait (false).GetAwaiter ().GetResult ();

	/// <summary>Asynchronously sends a media control command.</summary>
	public async Task<Dictionary<object, object?>> MediaControlCommandAsync (MediaControlCommand command, Dictionary<string, object?>? args = null)
		{
		Dictionary<string, object?> content = new () { { "_mcc", (int)command } };
		if (args is not null)
			{
			foreach (KeyValuePair<string, object?> kvp in args)
				{
				content[kvp.Key] = kvp.Value;
				}
			}

		Dictionary<object, object?> resp = await SendCommandAsync ("_mcc", content).ConfigureAwait (false);
		return !resp.TryGetValue ("_c", out object? contentObj) || contentObj is not Dictionary<object, object?> respContent
			? throw new ProtocolException ("missing content")
			: respContent;
		}

	/// <summary>
	/// Gets a value indicating whether the device currently advertises volume control support
	/// via the <c>_iMC</c> event's <c>_mcF</c> bitmask (<see cref="MediaControlCapabilities.Volume"/>).
	/// When this is <see langword="false"/>, audio is managed outside Companion (e.g. HDMI-CEC)
	/// and <see cref="GetVolume"/>/<see cref="SetVolume"/> must not be used.
	/// </summary>
	// pyatv/protocols/companion/__init__.py (_handle_control_flag_update) — line 439-449 as of pyatv 0.18.0
	public bool IsVolumeControlSupported => (_mediaControlFlags & MediaControlCapabilities.Volume) != 0;

	/// <summary>
	/// Raised whenever an updated <c>_iMC</c> event is received and the device's advertised
	/// media-control capability flags (including <see cref="IsVolumeControlSupported"/>) may
	/// have changed.
	/// </summary>
	public event EventHandler? MediaControlCapabilitiesChanged;

	/// <summary>
	/// Gets the most recently known system status (power state), updated by the initial
	/// <see cref="FetchAttentionState"/> snapshot taken during <see cref="Connect"/> and by
	/// subsequently pushed <c>SystemStatus</c>/<c>TVSystemStatus</c> events.
	/// </summary>
	// pyatv/protocols/companion/__init__.py — line 213 as of pyatv 0.18.0, 247-248 (self._power_state, power_state property)
	public SystemStatus CurrentSystemStatus { get; private set; } = SystemStatus.Unknown;

	/// <summary>
	/// Raised whenever a pushed <c>SystemStatus</c>/<c>TVSystemStatus</c> event changes
	/// <see cref="CurrentSystemStatus"/>, including transitions between non-<see cref="SystemStatus.Asleep"/>
	/// states (e.g. <see cref="SystemStatus.Awake"/> to <see cref="SystemStatus.Screensaver"/>). Unlike
	/// pyatv, which only notifies on the collapsed on/off boundary, this event fires on every raw state
	/// change; inspect <see cref="CurrentSystemStatus"/> from the handler for the granular value, or
	/// compare it against <see cref="SystemStatus.Asleep"/> if only on/off matters.
	/// </summary>
	// pyatv/protocols/companion/__init__.py (_handle_system_status_update) — line 249-256 as of pyatv 0.18.0
	public event EventHandler? SystemStatusChanged;

	/// <summary>Gets the current volume level, in percent ([0.0-100.0]).</summary>
	// pyatv/protocols/companion/__init__.py (GetVolume, resp["_c"]["_vol"] * 100.0) — line 441-443 as of pyatv 0.18.0
	[Obsolete ("Use GetVolumeAsync instead.")]
	public double GetVolume () => GetVolumeAsync ().ConfigureAwait (false).GetAwaiter ().GetResult ();

	/// <summary>Asynchronously gets the current volume level.</summary>
	public async Task<double> GetVolumeAsync ()
		{
		Dictionary<object, object?> content = await MediaControlCommandAsync (Protocol.MediaControlCommand.GetVolume).ConfigureAwait (false);
		_volume = ToDouble (content["_vol"]) * 100.0;
		return _volume;
		}

	/// <summary>Sets the current volume level.</summary>
	/// <param name="level">The new volume level, in percent ([0.0-100.0]).</param>
	// pyatv/protocols/companion/__init__.py (set_volume, level / 100.0) — line 459-467 as of pyatv 0.18.0
	[Obsolete ("Use SetVolumeAsync instead.")]
	public void SetVolume (double level) => SetVolumeAsync (level).ConfigureAwait (false).GetAwaiter ().GetResult ();

	/// <summary>Asynchronously sets the current volume level.</summary>
	public async Task SetVolumeAsync (double level)
		{
		_ = await MediaControlCommandAsync (Protocol.MediaControlCommand.SetVolume, new Dictionary<string, object?> { { "_vol", level / 100.0 } }).ConfigureAwait (false);
		_volume = level;
		}

	/// <summary>
	/// Toggles mute by saving the current volume and setting it to zero, or restoring the
	/// previously saved volume. Requires <see cref="IsVolumeControlSupported"/>.
	/// </summary>
	/// <returns><see langword="true"/> if the device is now muted, otherwise <see langword="false"/>.</returns>
	[Obsolete ("Use ToggleMuteAsync instead.")]
	public bool ToggleMute () => ToggleMuteAsync ().ConfigureAwait (false).GetAwaiter ().GetResult ();

	/// <summary>Asynchronously toggles mute.</summary>
	public async Task<bool> ToggleMuteAsync ()
		{
		if (!IsVolumeControlSupported)
			{
			throw new ProtocolException ("Device does not advertise volume control support (_mcF missing Volume flag)");
			}

		if (_volume > 0.0)
			{
			_preMuteVolume = _volume;
			await SetVolumeAsync (0.0).ConfigureAwait (false);
			return true;
			}

		await SetVolumeAsync (_preMuteVolume).ConfigureAwait (false);
		return false;
		}

	// pyatv/protocols/companion/__init__.py (self.api.listen_to("_iMC", ...) — line 433-436 as of pyatv 0.18.0)
	private double _preMuteVolume;

	/// <inheritdoc/>
	// pyatv/protocols/companion/__init__.py (_handle_control_flag_update) — line 438-449 as of pyatv 0.18.0
	void ICompanionProtocolListener.EventReceived (string eventName, Dictionary<object, object?> data)
		{
		// pyatv/protocols/companion/__init__.py (CompanionKeyboard.__init__, listen_to "_tiStarted"/"_tiStopped") — line 494-497 as of pyatv 0.18.0:
		// _tiStarted is not sent if the session starts while a field is already focused, so
		// _tiStopped and the _tiStart response (handled in TextInputStart) must also update state.
		if (string.Equals (eventName, "_tiStarted", StringComparison.Ordinal)
				|| string.Equals (eventName, "_tiStopped", StringComparison.Ordinal))
			{
			HandleTextInputFocusUpdate (data);
			}

		if (string.Equals (eventName, "_iMC", StringComparison.Ordinal) && data.TryGetValue ("_mcF", out object? mcf))
			{
			MediaControlCapabilities updated = (MediaControlCapabilities)ToLong (mcf);
			if (updated != _mediaControlFlags)
				{
				_mediaControlFlags = updated;
				MediaControlCapabilitiesChanged?.Invoke (this, EventArgs.Empty);
				}
			}

		// pyatv/protocols/companion/__init__.py — line 240-244 as of pyatv 0.18.0, 249-261 (SystemStatus/TVSystemStatus
		// both feed _handle_system_status_update; either name can carry "state").
		if ((string.Equals (eventName, "SystemStatus", StringComparison.Ordinal)
				|| string.Equals (eventName, "TVSystemStatus", StringComparison.Ordinal))
				&& data.TryGetValue ("state", out object? state))
			{
			SystemStatus updated = (SystemStatus)ToLong (state);
			if (updated != CurrentSystemStatus)
				{
				// pyatv collapses this to an on/off PowerState and only notifies on that boundary
				// (_system_status_to_power_state, __init__.py — line 225-232 as of pyatv 0.18.0). This
				// library instead raises SystemStatusChanged on every raw state change (e.g. Awake ->
				// Screensaver) and exposes the granular value via CurrentSystemStatus, so a caller that
				// only cares about on/off can still derive it (state != SystemStatus.Asleep) while callers
				// that want finer detail are no longer prevented from seeing it.
				CurrentSystemStatus = updated;
				SystemStatusChanged?.Invoke (this, EventArgs.Empty);
				}
			}
		}

	// Companion OPACK floats unpack as a plain double (or int/long for integral values via a
	// SizedInteger), so accept either.
	// pyatv/support/opack.py — line 31-33 as of pyatv 0.18.0, 195-201 (float pack/unpack)
	private static double ToDouble (object? value) => value switch
		{
			null => throw new ArgumentNullException (nameof (value)),
			double d => d,
			float f => f,
			long l => l,
			int i => i,
			Opack.SizedInteger si => si.Value,
			_ => Convert.ToDouble (value, System.Globalization.CultureInfo.InvariantCulture),
			};

	// Companion OPACK integers unpack as a SizedInteger (or a boxed long for small tag-encoded
	// values), not as a plain int/long usable directly with Convert.ToInt64.
	// pyatv/support/opack.py (_sized_int) — line 16-29 as of pyatv 0.18.0
	private static long ToLong (object? value) => value switch
		{
			null => throw new ArgumentNullException (nameof (value)),
			long l => l,
			int i => i,
			Opack.SizedInteger si => si.Value,
			_ => Convert.ToInt64 (value, System.Globalization.CultureInfo.InvariantCulture),
			};
	}
