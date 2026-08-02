using System;
using System.Collections.Generic;

using AppleTvControlLibrary.Auth;
using Opack = AppleTvControlLibrary.Opack;

namespace AppleTvControlLibrary.Protocol;

/// <summary>
/// HID command constants.
/// </summary>
// pyatv/protocols/companion/api.py:35-56 (HidCommand)
public enum HidCommand
	{
	/// <summary>pyatv/protocols/companion/api.py:38</summary>
	Up = 1,
	/// <summary>pyatv/protocols/companion/api.py:39</summary>
	Down = 2,
	/// <summary>pyatv/protocols/companion/api.py:40</summary>
	Left = 3,
	/// <summary>pyatv/protocols/companion/api.py:41</summary>
	Right = 4,
	/// <summary>pyatv/protocols/companion/api.py:42</summary>
	Menu = 5,
	/// <summary>pyatv/protocols/companion/api.py:43</summary>
	Select = 6,
	/// <summary>pyatv/protocols/companion/api.py:44</summary>
	Home = 7,
	/// <summary>pyatv/protocols/companion/api.py:45</summary>
	VolumeUp = 8,
	/// <summary>pyatv/protocols/companion/api.py:46</summary>
	VolumeDown = 9,
	/// <summary>pyatv/protocols/companion/api.py:47</summary>
	Siri = 10,
	/// <summary>pyatv/protocols/companion/api.py:48</summary>
	Screensaver = 11,
	/// <summary>pyatv/protocols/companion/api.py:49</summary>
	Sleep = 12,
	/// <summary>pyatv/protocols/companion/api.py:50</summary>
	Wake = 13,
	/// <summary>pyatv/protocols/companion/api.py:51</summary>
	PlayPause = 14,
	/// <summary>pyatv/protocols/companion/api.py:52</summary>
	ChannelIncrement = 15,
	/// <summary>pyatv/protocols/companion/api.py:53</summary>
	ChannelDecrement = 16,
	/// <summary>pyatv/protocols/companion/api.py:54</summary>
	Guide = 17,
	/// <summary>pyatv/protocols/companion/api.py:55</summary>
	PageUp = 18,
	/// <summary>pyatv/protocols/companion/api.py:56</summary>
	PageDown = 19,
	}

/// <summary>
/// Media Control command constants, used for playback and volume control (as opposed to the
/// remote-button surface exposed by <see cref="HidCommand"/>).
/// </summary>
// pyatv/protocols/companion/api.py:59-73 (MediaControlCommand)
public enum MediaControlCommand
	{
	/// <summary>pyatv/protocols/companion/api.py:62</summary>
	Play = 1,
	/// <summary>pyatv/protocols/companion/api.py:63</summary>
	Pause = 2,
	/// <summary>pyatv/protocols/companion/api.py:64</summary>
	NextTrack = 3,
	/// <summary>pyatv/protocols/companion/api.py:65</summary>
	PreviousTrack = 4,
	/// <summary>pyatv/protocols/companion/api.py:66</summary>
	GetVolume = 5,
	/// <summary>pyatv/protocols/companion/api.py:67</summary>
	SetVolume = 6,
	/// <summary>pyatv/protocols/companion/api.py:68</summary>
	SkipBy = 7,
	/// <summary>pyatv/protocols/companion/api.py:69</summary>
	FastForwardBegin = 8,
	/// <summary>pyatv/protocols/companion/api.py:70</summary>
	FastForwardEnd = 9,
	/// <summary>pyatv/protocols/companion/api.py:71</summary>
	RewindBegin = 10,
	/// <summary>pyatv/protocols/companion/api.py:72</summary>
	RewindEnd = 11,
	/// <summary>pyatv/protocols/companion/api.py:73</summary>
	GetCaptionSettings = 12,
	/// <summary>pyatv/protocols/companion/api.py:74</summary>
	SetCaptionSettings = 13,
	}

/// <summary>
/// Bitmask flags advertised by the <c>_iMC</c> event (<c>_mcF</c> field), indicating which media
/// controls the currently active app on the device supports. Notably, <see cref="Volume"/> being
/// clear means the device has no Companion-addressable volume/mute control at all (audio is
/// managed over HDMI-CEC instead), so callers must check this before using
/// <see cref="MediaControlCommand.GetVolume"/>/<see cref="MediaControlCommand.SetVolume"/>.
/// </summary>
// pyatv/protocols/companion/__init__.py:87-99 (MediaControlFlags)
[Flags]
public enum MediaControlCapabilities
	{
	/// <summary>pyatv/protocols/companion/__init__.py:90</summary>
	NoControls = 0x0000,
	/// <summary>pyatv/protocols/companion/__init__.py:91</summary>
	Play = 0x0001,
	/// <summary>pyatv/protocols/companion/__init__.py:92</summary>
	Pause = 0x0002,
	/// <summary>pyatv/protocols/companion/__init__.py:93</summary>
	NextTrack = 0x0004,
	/// <summary>pyatv/protocols/companion/__init__.py:94</summary>
	PreviousTrack = 0x0008,
	/// <summary>pyatv/protocols/companion/__init__.py:95</summary>
	FastForward = 0x0010,
	/// <summary>pyatv/protocols/companion/__init__.py:96</summary>
	Rewind = 0x0020,
	// 0x0040 and 0x0080 are unused/unknown in pyatv (pyatv/protocols/companion/__init__.py:97-98).
	/// <summary>pyatv/protocols/companion/__init__.py:99</summary>
	Volume = 0x0100,
	/// <summary>pyatv/protocols/companion/__init__.py:100</summary>
	SkipForward = 0x0200,
	/// <summary>pyatv/protocols/companion/__init__.py:101</summary>
	SkipBackward = 0x0400,
	}

/// <summary>
/// Current system state, as returned by <see cref="CompanionApi.FetchAttentionState"/>.
/// </summary>
// pyatv/protocols/companion/api.py:77-85 (SystemStatus)
public enum SystemStatus
	{
	/// <summary>Not a valid protocol entry, only used internally. pyatv/protocols/companion/api.py:80</summary>
	Unknown = 0x00,
	/// <summary>pyatv/protocols/companion/api.py:82</summary>
	Asleep = 0x01,
	/// <summary>pyatv/protocols/companion/api.py:83</summary>
	Screensaver = 0x02,
	/// <summary>pyatv/protocols/companion/api.py:84</summary>
	Awake = 0x03,
	/// <summary>Not verified against a real device. pyatv/protocols/companion/api.py:85</summary>
	Idle = 0x04,
	}

/// <summary>
/// Touch action constants.
/// </summary>
// pyatv/const.py:460-466 (TouchAction)
public enum TouchAction
	{
	/// <summary>pyatv/const.py:463</summary>
	Press = 1,
	/// <summary>pyatv/const.py:464</summary>
	Hold = 3,
	/// <summary>pyatv/const.py:465</summary>
	Release = 4,
	/// <summary>pyatv/const.py:466</summary>
	Click = 5,
	}

/// <summary>
/// Type of input when pressing a button.
/// </summary>
// pyatv/const.py:200-210 (InputAction)
public enum InputAction
	{
	/// <summary>Press and release quickly. pyatv/const.py:203</summary>
	SingleTap = 0,
	/// <summary>Press and release twice quickly. pyatv/const.py:206</summary>
	DoubleTap = 1,
	/// <summary>Press and hold for one second before releasing. pyatv/const.py:209</summary>
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
// pyatv/protocols/companion/api.py:94-475 (CompanionAPI, trimmed to WP6 scope)
public sealed class CompanionApi : ICompanionProtocolListener
	{
	// pyatv/protocols/companion/api.py:88-89
	private const double TOUCHPAD_WIDTH = 1000.0;
	private const double TOUCHPAD_HEIGHT = 1000.0;

	// pyatv/protocols/companion/api.py:399 (com.apple.tvremoteservices)
	private const string SESSION_SERVICE_TYPE = "com.apple.tvremoteservices";

	private readonly CompanionProtocol _protocol;
	private readonly HapCredentials _credentials;
	private readonly long _baseTimestamp;
	private readonly List<string> _subscribedEvents = new ();

	// pyatv/protocols/companion/__init__.py:439, 448 (self._volume, zeroed when flag absent)
	private MediaControlCapabilities _mediaControlFlags = MediaControlCapabilities.NoControls;
	private double _volume;

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
	// pyatv/protocols/companion/api.py:99-107 (__init__)
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

		// pyatv/protocols/companion/api.py:107 (self._base_timestamp = time.time_ns())
		_baseTimestamp = DateTime.UtcNow.Ticks * 100;

		// pyatv/protocols/companion/__init__.py:436 (self.api.listen_to("_iMC", ...))
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
	// pyatv/protocols/companion/api.py:106 (self.sid: int = 0)
	public long Sid
		{
		get;
		private set;
		}

	/// <summary>
	/// Runs the connection bring-up sequence: system info, touch subscription, session start,
	/// TV Remote Client session start, and text input session start.
	/// </summary>
	// pyatv/protocols/companion/api.py:135-160 (connect), trimmed per porting brief step 5
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
		// pyatv/protocols/companion/__init__.py:433-436 (self.api.listen_to("_iMC", ...)):
		// without this the device never reports its media-control capability flags, so
		// IsVolumeControlSupported stays false and volume/mute always fail.
		System.Diagnostics.Debug.WriteLine ("[CompanionApi] Connect: subscribing to _iMC");
		SubscribeEvent ("_iMC");
		System.Diagnostics.Debug.WriteLine ("[CompanionApi] Connect: bring-up complete");
		}

	// pyatv/protocols/companion/api.py:161-185 (_send_command)
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

	// pyatv/protocols/companion/api.py:247-266 (_send_event)
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
	// pyatv/protocols/companion/api.py:187-211 (system_info)
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
	// pyatv/protocols/companion/api.py:464-471 (_touch_start)
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
	// pyatv/protocols/companion/api.py:473-475 (_touch_stop)
	public void TouchStop ()
		{
		SendCommand ("_touchStop", new Dictionary<string, object?> { { "_i", 1 } });
		}

	/// <summary>Start a Companion session.</summary>
	// pyatv/protocols/companion/api.py:213-225 (_session_start)
	// pyatv/protocols/companion/api.py:214 (local_sid = randint(0, 2**32 - 1)): must stay
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
		// pyatv/protocols/companion/api.py:224 (self.sid = (remote_sid << 32) | local_sid)
		Sid = (remoteSid << 32) | (uint)localSid;
		}

	/// <summary>Stop the current Companion session.</summary>
	// pyatv/protocols/companion/api.py:241-245 (_session_stop)
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
	// pyatv/protocols/companion/api.py:227-239 (_tv_rc_session_start)
	public void TvRcSessionStart ()
		{
		try
			{
			SendCommand ("TVRCSessionStart", new Dictionary<string, object?> { { "ProtocolVersionKey", "1.2" } });
			}
		catch (Exception ex)
			{
			// pyatv/protocols/companion/api.py:238-239: logged and ignored.
			System.Diagnostics.Debug.WriteLine ($"[CompanionApi] TVRCSessionStart failed (ignored): {ex}");
			}
		}

	/// <summary>Start a text input session.</summary>
	// pyatv/protocols/companion/api.py:401-404 (_text_input_start)
	public Dictionary<object, object?> TextInputStart ()
		{
		return SendCommand ("_tiStart", new Dictionary<string, object?> ());
		}

	/// <summary>Stop the current text input session.</summary>
	// pyatv/protocols/companion/api.py:406-407 (_text_input_stop)
	public void TextInputStop ()
		{
		SendCommand ("_tiStop", new Dictionary<string, object?> ());
		}

	/// <summary>Subscribe to updates for an event.</summary>
	/// <param name="eventName">The event identifier to subscribe to.</param>
	// pyatv/protocols/companion/api.py:267-271 (subscribe_event)
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
	// pyatv/protocols/companion/api.py:273-277 (unsubscribe_event)
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
	// pyatv/protocols/companion/api.py:305-309 (hid_command)
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
	// pyatv/protocols/companion/api.py:311-326 (hid_event)
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
	// pyatv/protocols/companion/api.py:454-462 (fetch_attention_state)
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
	// pyatv/protocols/companion/api.py:395-399 (mediacontrol_command)
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
	// pyatv/protocols/companion/__init__.py:439-449 (_handle_control_flag_update)
	public bool IsVolumeControlSupported => (_mediaControlFlags & MediaControlCapabilities.Volume) != 0;

	/// <summary>
	/// Raised whenever an updated <c>_iMC</c> event is received and the device's advertised
	/// media-control capability flags (including <see cref="IsVolumeControlSupported"/>) may
	/// have changed.
	/// </summary>
	public event EventHandler? MediaControlCapabilitiesChanged;

	/// <summary>Gets the current volume level, in percent ([0.0-100.0]).</summary>
	// pyatv/protocols/companion/__init__.py:441-443 (GetVolume, resp["_c"]["_vol"] * 100.0)
	public double GetVolume ()
		{
		Dictionary<object, object?> content = MediaControlCommand (Protocol.MediaControlCommand.GetVolume);
		_volume = ToDouble (content["_vol"]) * 100.0;
		return _volume;
		}

	/// <summary>Sets the current volume level.</summary>
	/// <param name="level">The new volume level, in percent ([0.0-100.0]).</param>
	// pyatv/protocols/companion/__init__.py:459-467 (set_volume, level / 100.0)
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

	// pyatv/protocols/companion/__init__.py:433-436 (self.api.listen_to("_iMC", ...))
	private double _preMuteVolume;

	/// <inheritdoc/>
	// pyatv/protocols/companion/__init__.py:438-449 (_handle_control_flag_update)
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
		}

	// Companion OPACK floats unpack as a plain double (or int/long for integral values via a
	// SizedInteger), so accept either.
	// pyatv/support/opack.py:31-33, 195-201 (float pack/unpack)
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
	// pyatv/support/opack.py:16-29 (_sized_int)
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
