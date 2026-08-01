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
/// input, and attention state.
/// </summary>
/// <remarks>
/// App launching, media control, text input and account switching are intentionally out of
/// scope for this port (Companion-only, per the porting brief).
/// </remarks>
// pyatv/protocols/companion/api.py:94-475 (CompanionAPI, trimmed to WP6 scope)
public sealed class CompanionApi
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
		SystemInfo ();
		TouchStart ();
		SessionStart ();
		TvRcSessionStart ();
		TextInputStart ();
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
		catch (ProtocolException)
			{
			throw;
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
		catch (Exception)
			{
			// pyatv/protocols/companion/api.py:238-239: logged and ignored.
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
