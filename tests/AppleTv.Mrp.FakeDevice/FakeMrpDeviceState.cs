// Copyright (c) 2026 Neil Colvin. Licensed under the MIT License.
// See LICENSE file in the repository root for full license text.

using System.Collections.Generic;

namespace AppleTvControlLibrary.Mrp.FakeDevice;

/// <summary>
/// Input action associated with a completed button press, mirroring pyatv's
/// <c>const.InputAction</c> values used by the fake MRP device.
/// </summary>
// pyatv/const.py (InputAction) — line as of pyatv 0.18.0
public enum FakeMrpInputAction
	{
	/// <summary>A single tap.</summary>
	SingleTap,

	/// <summary>A double tap.</summary>
	DoubleTap,

	/// <summary>A press-and-hold.</summary>
	Hold,
	}

/// <summary>
/// Shared, in-memory device state for a <see cref="FakeMrpDevice"/>, mirroring the state held by
/// pyatv's fake MRP test device outside of the auth handshake.
/// </summary>
// pyatv/tests/fake_device/mrp.py (FakeMrpState) — line 218-350 as of pyatv 0.18.0
public sealed class FakeMrpDeviceState
	{
	/// <summary>Gets the per-player now-playing metadata, keyed by player (bundle) identifier.</summary>
	public Dictionary<string, PlayingState> States { get; } = [];

	/// <summary>Gets or sets the currently active player identifier, or <see langword="null"/> if nothing is playing.</summary>
	public string? ActivePlayer { get; set; }

	/// <summary>Gets or sets a value indicating whether the device reports itself as powered on.</summary>
	public bool PoweredOn { get; set; } = true;

	/// <summary>Gets or sets the last connection state reported via SET_CONNECTION_STATE_MESSAGE.</summary>
	public int ConnectionState { get; set; }

	/// <summary>Gets or sets the current device volume, in the range 0.0-1.0.</summary>
	public double Volume { get; set; }

	/// <summary>Gets the list of currently attached output device identifiers.</summary>
	public List<string> OutputDevices { get; } = [];

	/// <summary>Gets or sets the number of GENERIC_MESSAGE heartbeats received.</summary>
	public int HeartbeatCount { get; set; }

	/// <summary>Gets or sets the last pressed button, as looked up from a SEND_HID_EVENT_MESSAGE key-up.</summary>
	public string? LastButtonPressed { get; set; }

	/// <summary>Gets or sets the input action (tap/double-tap/hold) associated with the last button press.</summary>
	public FakeMrpInputAction? LastButtonAction { get; set; }

	/// <summary>Gets the set of outstanding (pressed-but-not-released) HID key-down timestamps, keyed by (usePage, usage).</summary>
	public Dictionary<(int UsePage, int Usage), long> OutstandingKeypresses { get; } = [];

	/// <summary>Gets or returns the metadata for the given player identifier, creating it if necessary.</summary>
	/// <param name="identifier">The player (bundle) identifier.</param>
	/// <returns>The tracked <see cref="PlayingState"/> for that player.</returns>
	// pyatv/tests/fake_device/mrp.py (FakeMrpState.get_player_state) — line 239-240 as of pyatv 0.18.0
	public PlayingState GetPlayerState (string identifier)
		{
		if (!States.TryGetValue (identifier, out PlayingState? state))
			{
			state = new PlayingState ();
			States[identifier] = state;
			}

		return state;
		}
	}
