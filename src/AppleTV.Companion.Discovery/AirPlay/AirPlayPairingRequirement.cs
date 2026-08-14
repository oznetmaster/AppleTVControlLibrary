// Copyright (c) 2026 Neil Colvin. Licensed under the MIT License.
// See LICENSE file in the repository root for full license text.

namespace AppleTvControlLibrary.Discovery.AirPlay;

/// <summary>Whether pairing is required, supported, or disabled for a discovered AirPlay service.</summary>
/// <remarks>
/// pyatv's <c>PairingRequirement</c> enum (pyatv/const.py — line 213-229 as of pyatv 0.18.0) has five
/// values (Unsupported, Disabled, NotNeeded, Optional, Mandatory), but AirPlay's own
/// <c>update_service_details</c>/<c>get_pairing_requirement</c>
/// (pyatv/protocols/airplay/utils.py — line 139-157, 262-278 as of pyatv 0.18.0) only ever produces
/// four of them, so only those four are modeled here rather than inventing meanings for the unused
/// value.
/// </remarks>
// pyatv/protocols/airplay/utils.py (update_service_details, get_pairing_requirement) — line 139-157, 262-278 as of pyatv 0.18.0
public enum AirPlayPairingRequirement
	{
	/// <summary>Pairing is not needed. pyatv/protocols/airplay/utils.py — line 157 as of pyatv 0.18.0</summary>
	NotNeeded,

	/// <summary>Pairing is mandatory (legacy pairing or PIN required). pyatv/protocols/airplay/utils.py — line 151-152 as of pyatv 0.18.0</summary>
	Mandatory,

	/// <summary>
	/// Access control disallows pairing (e.g. only devices belonging to the same home).
	/// pyatv/protocols/airplay/utils.py — line 266-269 as of pyatv 0.18.0
	/// </summary>
	Disabled,

	/// <summary>
	/// Pairing is not supported by pyatv for this device, either due to an unsupported model or an
	/// unsupported access control type ("Current User").
	/// pyatv/protocols/airplay/utils.py — line 155-156, 270-276 as of pyatv 0.18.0
	/// </summary>
	Unsupported,
	}
