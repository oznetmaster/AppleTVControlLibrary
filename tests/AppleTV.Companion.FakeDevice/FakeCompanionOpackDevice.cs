// Copyright (c) 2026 Neil Colvin. Licensed under the MIT License.
// See LICENSE file in the repository root for full license text.

using System;
using System.Collections.Generic;

using AppleTvControlLibrary.Connection;
using AppleTvControlLibrary.Opack;
using AppleTvControlLibrary.Protocol;

namespace AppleTvControlLibrary.FakeDevice;

/// <summary>
/// In-memory fake Companion Apple TV that additionally understands the OPACK (E_OPACK) command
/// surface needed to validate <see cref="CompanionApi"/>: system info, touch and text-input
/// session lifecycle, session start/stop, TV Remote Client session start, HID commands, event
/// subscription and attention state.
/// </summary>
/// <remarks>
/// Ported from <c>tests/fake_device/companion.py</c> (<c>FakeCompanionService</c>), operating
/// directly on decoded OPACK dictionaries via <see cref="HandleOpackFrame"/> rather than raw
/// sockets, mirroring how <see cref="FakeCompanionDevice"/> handles auth frames for WP5.
/// </remarks>
// tests/fake_device/companion.py:227-563 (FakeCompanionService, trimmed to WP6 scope)
public sealed class FakeCompanionOpackDevice
	{
	// pyatv/protocols/companion/api.py (SystemStatus.Awake is the fake device's initial state) — line 77-85 as of pyatv 0.18.0
	// tests/fake_device/companion.py:90 (self._system_status: SystemStatus = SystemStatus.Awake)
	private SystemStatus _systemStatus = SystemStatus.Awake;

	private readonly HashSet<string> _interests = new (StringComparer.Ordinal);

	/// <summary>Gets the most recently received <c>_systemInfo</c> content, if any.</summary>
	// tests/fake_device/companion.py:489-491 (handle__systeminfo)
	public Dictionary<object, object?>? ReceivedSystemInfo
		{
		get;
		private set;
		}

	/// <summary>Gets the local session id most recently supplied via <c>_sessionStart</c>.</summary>
	// tests/fake_device/companion.py:98, 477-480 (self.sid, handle__sessionstart)
	public long LocalSid
		{
		get;
		private set;
		}

	/// <summary>Gets a value indicating whether a session has been started.</summary>
	public bool HasSessionStarted
		{
		get;
		private set;
		}

	/// <summary>Gets the service type most recently supplied via <c>_sessionStart</c>.</summary>
	public string? ServiceType
		{
		get;
		private set;
		}

	/// <summary>Gets the protocol version most recently supplied via <c>TVRCSessionStart</c>.</summary>
	// tests/fake_device/companion.py:493-495 (handle_tvrcsessionstart)
	public string? TvRcProtocolVersion
		{
		get;
		private set;
		}

	/// <summary>Gets a value indicating whether a touch session has been started.</summary>
	public bool HasTouchStarted
		{
		get;
		private set;
		}

	/// <summary>Gets a value indicating whether a text input session has been started.</summary>
	public bool HasTextInputStarted
		{
		get;
		private set;
		}

	/// <summary>Gets the set of pressed HID buttons that have not yet been released.</summary>
	public HashSet<HidCommand> PressedButtons
		{
		get;
		} = new ();

	/// <summary>Gets a value indicating whether the device has been put to sleep via a HID command.</summary>
	// tests/fake_device/companion.py:387-389 (Sleep sets self.state.powered_on = False)
	public bool IsAsleep
		{
		get;
		private set;
		}

	// tests/fake_device/companion.py:350-351 (self.state.volume)
	private double _volume;

	/// <summary>Gets or sets a value indicating whether volume control (<c>_mcF</c>'s Volume bit) is advertised.</summary>
	public bool SupportsVolumeControl
		{
		get;
		set;
		} = true;

	/// <summary>Gets the current volume level, in percent ([0.0-100.0]).</summary>
	public double Volume => _volume;

	/// <summary>Sets the system status reported by <c>FetchAttentionState</c>.</summary>
	/// <param name="status">The new status.</param>
	public void SetSystemStatus (SystemStatus status)
		{
		_systemStatus = status;
		}

	/// <summary>Handle an incoming E_OPACK frame and produce the response frame, if any.</summary>
	/// <param name="request">The decoded request dictionary.</param>
	/// <returns>The response dictionary to encode and send back, or <see langword="null"/> for events (no response expected).</returns>
	// tests/fake_device/companion.py:271-307 (data_received, dispatch portion)
	public Dictionary<object, object?>? HandleOpackFrame (Dictionary<object, object?> request)
		{
		string identifier = (string)request["_i"]!;
		long messageType = ToLong (request["_t"]);

		// tests/fake_device/companion.py:300 (handler_method_name = f"handle_{unpacked['_i'].lower()}")
		return identifier.ToUpperInvariant () switch
			{
			"_SYSTEMINFO" => HandleSystemInfo (request),
			"_TOUCHSTART" => HandleTouchStart (request),
			"_TOUCHSTOP" => HandleTouchStop (request),
			"_SESSIONSTART" => HandleSessionStart (request),
			"_SESSIONSTOP" => HandleSessionStop (request),
			"TVRCSESSIONSTART" => HandleTvRcSessionStart (request),
			"_TISTART" when messageType == (long)MessageType.Request => HandleTextInputStart (request),
			"_TISTOP" when messageType == (long)MessageType.Request => HandleTextInputStop (request),
			"_HIDC" => HandleHidCommand (request),
			"_MCC" => HandleMediaControlCommand (request),
			"_INTEREST" => HandleInterest (request),
			"FETCHATTENTIONSTATE" => HandleFetchAttentionState (request),
			_ => HandleNotSupported (request),
			};
		}

	// tests/fake_device/companion.py:309-318 (send_response)
	private static Dictionary<object, object?> Response (Dictionary<object, object?> request, Dictionary<object, object?> content)
		{
		return new Dictionary<object, object?>
			{
			{ "_i", request["_i"] },
			{ "_x", request["_x"] },
			{ "_t", (int)MessageType.Response },
			{ "_c", content },
			};
		}

	// tests/fake_device/companion.py:331-344 (send_error)
	private static Dictionary<object, object?> Error (Dictionary<object, object?> request, string message, int code = 1337, string domain = "RPErrorDomain")
		{
		return new Dictionary<object, object?>
			{
			{ "_i", request["_i"] },
			{ "_x", request["_x"] },
			{ "_t", (int)MessageType.Response },
			{ "_ec", code },
			{ "_ed", domain },
			{ "_em", message },
			};
		}

	// tests/fake_device/companion.py:346-348 (send_handler_not_supported)
	private static Dictionary<object, object?> HandleNotSupported (Dictionary<object, object?> request)
		{
		return Error (request, "No request handler", code: 58822);
		}

	// tests/fake_device/companion.py:489-491 (handle__systeminfo)
	private Dictionary<object, object?> HandleSystemInfo (Dictionary<object, object?> request)
		{
		ReceivedSystemInfo = (Dictionary<object, object?>)request["_c"]!;
		return Response (request, new Dictionary<object, object?> ());
		}

	// tests/fake_device/companion.py:464-471 (server side accepts _touchStart unconditionally)
	private Dictionary<object, object?> HandleTouchStart (Dictionary<object, object?> request)
		{
		HasTouchStarted = true;
		return Response (request, new Dictionary<object, object?> ());
		}

	private Dictionary<object, object?> HandleTouchStop (Dictionary<object, object?> request)
		{
		HasTouchStarted = false;
		return Response (request, new Dictionary<object, object?> ());
		}

	// tests/fake_device/companion.py:477-480 (handle__sessionstart)
	private Dictionary<object, object?> HandleSessionStart (Dictionary<object, object?> request)
		{
		var content = (Dictionary<object, object?>)request["_c"]!;
		LocalSid = ToLong (content["_sid"]);
		ServiceType = (string)content["_srvT"]!;
		HasSessionStarted = true;

		// tests/fake_device/companion.py:480 (self.send_response(message, {"_sid": 5555}))
		return Response (request, new Dictionary<object, object?> { { "_sid", 5555L } });
		}

	// tests/fake_device/companion.py:482-487 (handle__sessionstop)
	private Dictionary<object, object?> HandleSessionStop (Dictionary<object, object?> request)
		{
		var content = (Dictionary<object, object?>)request["_c"]!;
		long sid = ToLong (content["_sid"]);

		// tests/fake_device/companion.py:483 ((5555 << 32 | self.state.sid))
		if (sid == ((5555L << 32) | (uint)LocalSid))
			{
			HasSessionStarted = false;
			return Response (request, new Dictionary<object, object?> ());
			}

		return Error (request, "Invalid SID");
		}

	// tests/fake_device/companion.py:493-495 (handle_tvrcsessionstart)
	private Dictionary<object, object?> HandleTvRcSessionStart (Dictionary<object, object?> request)
		{
		var content = (Dictionary<object, object?>)(request.TryGetValue ("_c", out object? c) ? c! : new Dictionary<object, object?> ());
		TvRcProtocolVersion = content.TryGetValue ("ProtocolVersionKey", out object? version) ? (string?)version : null;
		return Response (request, content);
		}

	// tests/fake_device/companion.py:510-521 (handle__tistart)
	private Dictionary<object, object?> HandleTextInputStart (Dictionary<object, object?> request)
		{
		HasTextInputStarted = true;
		return Response (request, new Dictionary<object, object?> ());
		}

	// tests/fake_device/companion.py:523-531 (handle__tistop)
	private Dictionary<object, object?> HandleTextInputStop (Dictionary<object, object?> request)
		{
		HasTextInputStarted = false;
		return Response (request, new Dictionary<object, object?> ());
		}

	// tests/fake_device/companion.py:380-402 (handle__hidc, trimmed to Sleep/Wake/press tracking)
	private Dictionary<object, object?> HandleHidCommand (Dictionary<object, object?> request)
		{
		var content = (Dictionary<object, object?>)request["_c"]!;
		long buttonState = ToLong (content["_hBtS"]);
		var buttonCode = (HidCommand)ToLong (content["_hidC"]);

		if (buttonState == 1)
			{
			PressedButtons.Add (buttonCode);
			}
		else if (buttonState == 2 && buttonCode == HidCommand.Sleep)
			{
			IsAsleep = true;
			}
		else if (buttonState == 2 && buttonCode == HidCommand.Wake)
			{
			IsAsleep = false;
			}

		return Response (request, new Dictionary<object, object?> ());
		}

	// tests/fake_device/companion.py:457-467 (handle__mcc, trimmed to GetVolume/SetVolume)
	private Dictionary<object, object?> HandleMediaControlCommand (Dictionary<object, object?> request)
		{
		var content = (Dictionary<object, object?>)request["_c"]!;
		var mcc = (MediaControlCommand)ToLong (content["_mcc"]);

		if (mcc == MediaControlCommand.SetVolume)
			{
			double newVolume = ToDouble (content["_vol"]) * 100.0;
			_volume = Math.Min (Math.Max (newVolume, 0.0), 100.0);
			return Response (request, new Dictionary<object, object?> ());
			}

		if (mcc == MediaControlCommand.GetVolume)
			{
			return Response (request, new Dictionary<object, object?> { { "_vol", _volume / 100.0 } });
			}

		return Response (request, new Dictionary<object, object?> ());
		}

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
			AppleTvControlLibrary.Opack.SizedInteger si => si.Value,
			_ => Convert.ToDouble (value, System.Globalization.CultureInfo.InvariantCulture),
			};
		}

	// tests/fake_device/companion.py:497-508 (handle__interest)
	private Dictionary<object, object?>? HandleInterest (Dictionary<object, object?> request)
		{
		var content = (Dictionary<object, object?>)request["_c"]!;
		if (content.TryGetValue ("_regEvents", out object? regEvents) && regEvents is IEnumerable<object> registered)
			{
			foreach (object eventName in registered)
				{
				_interests.Add ((string)eventName);
				}
			}
		else if (content.TryGetValue ("_deregEvents", out object? deregEvents) && deregEvents is IEnumerable<object> unregistered)
			{
			foreach (object eventName in unregistered)
				{
				_interests.Remove ((string)eventName);
				}
			}

		// _interest is sent as an Event (no XID-based response expected client-side).
		return null;
		}

	// tests/fake_device/companion.py:558-563 (handle_fetchattentionstate)
	private Dictionary<object, object?> HandleFetchAttentionState (Dictionary<object, object?> request)
		{
		return Response (request, new Dictionary<object, object?> { { "state", (long)_systemStatus } });
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
			AppleTvControlLibrary.Opack.SizedInteger si => si.Value,
			_ => Convert.ToInt64 (value, System.Globalization.CultureInfo.InvariantCulture),
			};
		}
	}
