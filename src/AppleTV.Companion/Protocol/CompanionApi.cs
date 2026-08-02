using System;
using System.Collections.Generic;

using AppleTvControlLibrary.Auth;
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
/// High level implementation of the Companion API: system info, session lifecycle, HID
/// input, media control (volume) and attention state.
/// </summary>
/// <remarks>
/// App launching, text input and account switching are intentionally out of scope for this
/// port (Companion-only, per the porting brief).
/// </remarks>
// pyatv/protocols/companion/api.py (CompanionAPI, trimmed to WP6 scope) — line 94-475 as of pyatv 0.18.0
public sealed class CompanionApi : ICompanionProtocolListener
	{
	// pyatv/protocols/companion/api.py — line 88-89 as of pyatv 0.18.0
	private const double TOUCHPAD_WIDTH = 1000.0;
	private const double TOUCHPAD_HEIGHT = 1000.0;

	// pyatv/protocols/companion/api.py (com.apple.tvremoteservices) — line 399 as of pyatv 0.18.0
	private const string SESSION_SERVICE_TYPE = "com.apple.tvremoteservices";

	private readonly CompanionProtocol _protocol;
	private readonly HapCredentials _credentials;
	private readonly long _baseTimestamp;
	private readonly List<string> _subscribedEvents = new ();

	// pyatv/protocols/companion/__init__.py — line 439 as of pyatv 0.18.0, 448 (self._volume, zeroed when flag absent)
	private MediaControlCapabilities _mediaControlFlags = MediaControlCapabilities.NoControls;
	private double _volume;

	// pyatv/protocols/companion/__init__.py (self._power_state = PowerState.Unknown) — line 213 as of pyatv 0.18.0
	private SystemStatus _currentSystemStatus = SystemStatus.Unknown;

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
		}

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
	public void Connect ()
		{
		System.Diagnostics.Debug.WriteLine ("[CompanionApi] Connect: sending _systemInfo");
		SystemInfo ();
		System.Diagnostics.Debug.WriteLine ("[CompanionApi] Connect: sending _touchStart");
		TouchStart ();
		System.Diagnostics.Debug.WriteLine ("[CompanionApi] Connect: sending _sessionStart");
		SessionStart ();
		System.Diagnostics.Debug.WriteLine ("[CompanionApi] Connect: sending TVRCSessionStart");
		TvRcSessionStart ();
		System.Diagnostics.Debug.WriteLine ("[CompanionApi] Connect: sending _tiStart");
		TextInputStart ();
		// pyatv/protocols/companion/__init__.py (self.api.listen_to("_iMC", ...) — line 433-436 as of pyatv 0.18.0):
		// without this the device never reports its media-control capability flags, so
		// IsVolumeControlSupported stays false and volume/mute always fail.
		System.Diagnostics.Debug.WriteLine ("[CompanionApi] Connect: subscribing to _iMC");
		SubscribeEvent ("_iMC");

		// pyatv/protocols/companion/__init__.py (CompanionPower.initialize) — line 219-246 as of pyatv 0.18.0: fetch an
		// initial snapshot best-effort (newer tvOS can reply "No request handler" here, which
		// must not prevent subscribing to push updates below), then subscribe to SystemStatus/
		// TVSystemStatus so power state can still be tracked via pushed events afterwards.
		try
			{
			System.Diagnostics.Debug.WriteLine ("[CompanionApi] Connect: sending FetchAttentionState");
			_currentSystemStatus = FetchAttentionState ();
			}
		catch (Exception ex)
			{
			System.Diagnostics.Debug.WriteLine ($"[CompanionApi] Connect: FetchAttentionState failed (ignored): {ex}");
			}

		System.Diagnostics.Debug.WriteLine ("[CompanionApi] Connect: subscribing to SystemStatus/TVSystemStatus");
		SubscribeEvent ("SystemStatus");
		SubscribeEvent ("TVSystemStatus");
		System.Diagnostics.Debug.WriteLine ("[CompanionApi] Connect: bring-up complete");
		}

	// pyatv/protocols/companion/api.py (_send_command) — line 161-185 as of pyatv 0.18.0
	private Dictionary<object, object?> SendCommand (string identifier, Dictionary<string, object?> content, MessageType messageType = MessageType.Request)
		{
		try
			{
			return _protocol.ExchangeOpack (
				Connection.FrameType.E_OPACK,
				new Dictionary<string, object?>
					{
					{ "_i", identifier },
					{ "_t", (int)messageType },
					{ "_c", content },
					});
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
	private void SendEvent (string identifier, Dictionary<string, object?> content)
		{
		try
			{
			_protocol.SendOpack (
				Connection.FrameType.E_OPACK,
				new Dictionary<string, object?>
					{
					{ "_i", identifier },
					{ "_t", (int)MessageType.Event },
					{ "_c", content },
					});
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
	public void SystemInfo ()
		{
		// Bunch of semi-random values here, per pyatv's own comment (api.py:193).
		SendCommand (
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
				});
		}

	/// <summary>Subscribe to touch gestures.</summary>
	// pyatv/protocols/companion/api.py (_touch_start) — line 464-471 as of pyatv 0.18.0
	public void TouchStart ()
		{
		SendCommand (
			"_touchStart",
			new Dictionary<string, object?>
				{
				{ "_height", TOUCHPAD_HEIGHT },
				{ "_tFl", 0 },
				{ "_width", TOUCHPAD_WIDTH },
				});
		}

	/// <summary>Unsubscribe from touch gestures.</summary>
	// pyatv/protocols/companion/api.py (_touch_stop) — line 473-475 as of pyatv 0.18.0
	public void TouchStop ()
		{
		SendCommand ("_touchStop", new Dictionary<string, object?> { { "_i", 1 } });
		}

	/// <summary>Start a Companion session.</summary>
	// pyatv/protocols/companion/api.py (_session_start) — line 213-225 as of pyatv 0.18.0
	// pyatv/protocols/companion/api.py (local_sid = randint(0, 2**32 - 1) — line 214 as of pyatv 0.18.0): must stay
	// within the unsigned 32-bit range, since OPACK's integer packer treats negative values
	// (or any int < 0x28) as a single-byte tag-encoded integer, silently corrupting the frame.
	public void SessionStart ()
		{
		long localSid = (long)(uint)new Random ().Next (int.MinValue, int.MaxValue);
		var resp = SendCommand (
			"_sessionStart",
			new Dictionary<string, object?>
				{
				{ "_srvT", SESSION_SERVICE_TYPE },
				{ "_sid", localSid },
				});

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
	public void SessionStop ()
		{
		SendCommand (
			"_sessionStop",
			new Dictionary<string, object?>
				{
				{ "_srvT", SESSION_SERVICE_TYPE },
				{ "_sid", Sid },
				});
		}

	/// <summary>
	/// Open a TV Remote Client session. tvOS does not answer <c>FetchAttentionState</c> until a
	/// TV Remote Client session is registered with <c>tvremoted</c>; older devices may simply
	/// error on this command, so failures here are intentionally swallowed.
	/// </summary>
	// pyatv/protocols/companion/api.py (_tv_rc_session_start) — line 227-239 as of pyatv 0.18.0
	public void TvRcSessionStart ()
		{
		try
			{
			SendCommand ("TVRCSessionStart", new Dictionary<string, object?> { { "ProtocolVersionKey", "1.2" } });
			}
		catch (Exception ex)
			{
			// pyatv/protocols/companion/api.py — line 238-239 as of pyatv 0.18.0: logged and ignored.
			System.Diagnostics.Debug.WriteLine ($"[CompanionApi] TVRCSessionStart failed (ignored): {ex}");
			}
		}

	/// <summary>Start a text input session.</summary>
	// pyatv/protocols/companion/api.py (_text_input_start) — line 401-404 as of pyatv 0.18.0
	public Dictionary<object, object?> TextInputStart ()
		{
		return SendCommand ("_tiStart", new Dictionary<string, object?> ());
		}

	/// <summary>Stop the current text input session.</summary>
	// pyatv/protocols/companion/api.py (_text_input_stop) — line 406-407 as of pyatv 0.18.0
	public void TextInputStop ()
		{
		SendCommand ("_tiStop", new Dictionary<string, object?> ());
		}

	/// <summary>Subscribe to updates for an event.</summary>
	/// <param name="eventName">The event identifier to subscribe to.</param>
	// pyatv/protocols/companion/api.py (subscribe_event) — line 267-271 as of pyatv 0.18.0
	public void SubscribeEvent (string eventName)
		{
		if (!_subscribedEvents.Contains (eventName))
			{
			SendEvent ("_interest", new Dictionary<string, object?> { { "_regEvents", new[] { eventName } } });
			_subscribedEvents.Add (eventName);
			}
		}

	/// <summary>Unsubscribe from updates for an event.</summary>
	/// <param name="eventName">The event identifier to unsubscribe from.</param>
	// pyatv/protocols/companion/api.py (unsubscribe_event) — line 273-277 as of pyatv 0.18.0
	public void UnsubscribeEvent (string eventName)
		{
		if (_subscribedEvents.Contains (eventName))
			{
			SendEvent ("_interest", new Dictionary<string, object?> { { "_deregEvents", new[] { eventName } } });
			_subscribedEvents.Remove (eventName);
			}
		}

	/// <summary>Send a HID command.</summary>
	/// <param name="down"><see langword="true"/> for a button-down event, <see langword="false"/> for button-up.</param>
	/// <param name="command">The button being pressed or released.</param>
	// pyatv/protocols/companion/api.py (hid_command) — line 305-309 as of pyatv 0.18.0
	public void SendHidCommand (bool down, HidCommand command)
		{
		SendCommand (
			"_hidC",
			new Dictionary<string, object?>
				{
				{ "_hBtS", down ? 1 : 2 },
				{ "_hidC", (int)command },
				});
		}

	/// <summary>Send a touch event.</summary>
	/// <param name="x">The x coordinate, in the range [0, 1000].</param>
	/// <param name="y">The y coordinate, in the range [0, 1000].</param>
	/// <param name="mode">The touch phase.</param>
	// pyatv/protocols/companion/api.py (hid_event) — line 311-326 as of pyatv 0.18.0
	public void SendHidEvent (int x, int y, TouchAction mode)
		{
		x = Math.Max (x, 0);
		y = Math.Max (y, 0);
		x = Math.Min (x, (int)TOUCHPAD_WIDTH);
		y = Math.Min (y, (int)TOUCHPAD_HEIGHT);

		SendEvent (
			"_hidT",
			new Dictionary<string, object?>
				{
				{ "_ns", (DateTime.UtcNow.Ticks * 100) - _baseTimestamp },
				{ "_tFg", 1 },
				{ "_cx", x },
				{ "_tPh", (int)mode },
				{ "_cy", y },
				});
		}

	/// <summary>Fetch the current attention state (system status) from the device.</summary>
	/// <returns>The current <see cref="SystemStatus"/>.</returns>
	// pyatv/protocols/companion/api.py (fetch_attention_state) — line 454-462 as of pyatv 0.18.0
	public SystemStatus FetchAttentionState ()
		{
		var resp = SendCommand ("FetchAttentionState", new Dictionary<string, object?> ());

		if (!resp.TryGetValue ("_c", out object? contentObj) || contentObj is not Dictionary<object, object?> content)
			{
			throw new ProtocolException ("missing content");
			}

		long state = ToLong (content["state"]);
		return (SystemStatus)state;
		}

	/// <summary>Send a media control command to the device.</summary>
	/// <param name="command">The media control command to send.</param>
	/// <param name="args">Additional command-specific arguments, if any.</param>
	/// <returns>The decoded response content (the message's <c>_c</c> field).</returns>
	// pyatv/protocols/companion/api.py (mediacontrol_command) — line 395-399 as of pyatv 0.18.0
	public Dictionary<object, object?> MediaControlCommand (MediaControlCommand command, Dictionary<string, object?>? args = null)
		{
		Dictionary<string, object?> content = new () { { "_mcc", (int)command } };
		if (args is not null)
			{
			foreach (var kvp in args)
				{
				content[kvp.Key] = kvp.Value;
				}
			}

		var resp = SendCommand ("_mcc", content);
		if (!resp.TryGetValue ("_c", out object? contentObj) || contentObj is not Dictionary<object, object?> respContent)
			{
			throw new ProtocolException ("missing content");
			}

		return respContent;
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
	public SystemStatus CurrentSystemStatus => _currentSystemStatus;

	/// <summary>
	/// Raised whenever a pushed <c>SystemStatus</c>/<c>TVSystemStatus</c> event updates
	/// <see cref="CurrentSystemStatus"/>.
	/// </summary>
	// pyatv/protocols/companion/__init__.py (_handle_system_status_update) — line 249-256 as of pyatv 0.18.0
	public event EventHandler? SystemStatusChanged;

	/// <summary>Gets the current volume level, in percent ([0.0-100.0]).</summary>
	// pyatv/protocols/companion/__init__.py (GetVolume, resp["_c"]["_vol"] * 100.0) — line 441-443 as of pyatv 0.18.0
	public double GetVolume ()
		{
		Dictionary<object, object?> content = MediaControlCommand (Protocol.MediaControlCommand.GetVolume);
		_volume = ToDouble (content["_vol"]) * 100.0;
		return _volume;
		}

	/// <summary>Sets the current volume level.</summary>
	/// <param name="level">The new volume level, in percent ([0.0-100.0]).</param>
	// pyatv/protocols/companion/__init__.py (set_volume, level / 100.0) — line 459-467 as of pyatv 0.18.0
	public void SetVolume (double level)
		{
		MediaControlCommand (Protocol.MediaControlCommand.SetVolume, new Dictionary<string, object?> { { "_vol", level / 100.0 } });
		_volume = level;
		}

	/// <summary>
	/// Toggles mute by saving the current volume and setting it to zero, or restoring the
	/// previously saved volume. Requires <see cref="IsVolumeControlSupported"/>.
	/// </summary>
	/// <returns><see langword="true"/> if the device is now muted, otherwise <see langword="false"/>.</returns>
	public bool ToggleMute ()
		{
		if (!IsVolumeControlSupported)
			{
			throw new ProtocolException ("Device does not advertise volume control support (_mcF missing Volume flag)");
			}

		if (_volume > 0.0)
			{
			_preMuteVolume = _volume;
			SetVolume (0.0);
			return true;
			}

		SetVolume (_preMuteVolume);
		return false;
		}

	// pyatv/protocols/companion/__init__.py (self.api.listen_to("_iMC", ...) — line 433-436 as of pyatv 0.18.0)
	private double _preMuteVolume;

	/// <inheritdoc/>
	// pyatv/protocols/companion/__init__.py (_handle_control_flag_update) — line 438-449 as of pyatv 0.18.0
	void ICompanionProtocolListener.EventReceived (string eventName, Dictionary<object, object?> data)
		{
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
			if (updated != _currentSystemStatus)
				{
				// pyatv/protocols/companion/__init__.py (_system_status_to_power_state) — line 225-232 as of pyatv 0.18.0:
				// only Asleep maps to Off; Screensaver, Awake and Idle all map to On, so only
				// raise the event when the mapped power state actually changes (e.g. Awake ->
				// Screensaver is not a real transition from the UI's point of view).
				bool wasOn = _currentSystemStatus != SystemStatus.Asleep && _currentSystemStatus != SystemStatus.Unknown;
				bool isOn = updated != SystemStatus.Asleep && updated != SystemStatus.Unknown;
				_currentSystemStatus = updated;
				if (isOn != wasOn)
					{
					SystemStatusChanged?.Invoke (this, EventArgs.Empty);
					}
				}
			}
		}

	// Companion OPACK floats unpack as a plain double (or int/long for integral values via a
	// SizedInteger), so accept either.
	// pyatv/support/opack.py — line 31-33 as of pyatv 0.18.0, 195-201 (float pack/unpack)
	private static double ToDouble (object? value)
		{
		return value switch
			{
			null => throw new ArgumentNullException (nameof (value)),
			double d => d,
			float f => f,
			long l => l,
			int i => i,
			Opack.SizedInteger si => si.Value,
			_ => Convert.ToDouble (value, System.Globalization.CultureInfo.InvariantCulture),
			};
		}

	// Companion OPACK integers unpack as a SizedInteger (or a boxed long for small tag-encoded
	// values), not as a plain int/long usable directly with Convert.ToInt64.
	// pyatv/support/opack.py (_sized_int) — line 16-29 as of pyatv 0.18.0
	private static long ToLong (object? value)
		{
		return value switch
			{
			null => throw new ArgumentNullException (nameof (value)),
			long l => l,
			int i => i,
			Opack.SizedInteger si => si.Value,
			_ => Convert.ToInt64 (value, System.Globalization.CultureInfo.InvariantCulture),
			};
		}
	}
