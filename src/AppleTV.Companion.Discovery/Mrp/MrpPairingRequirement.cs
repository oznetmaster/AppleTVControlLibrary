// Copyright (c) 2026 Neil Colvin. Licensed under the MIT License.
// See LICENSE file in the repository root for full license text.

namespace AppleTvControlLibrary.Discovery.Mrp;

/// <summary>Whether pairing is required, supported, or disabled for a discovered MRP service.</summary>
/// <remarks>
/// pyatv's <c>PairingRequirement</c> enum (pyatv/const.py — line 213-229 as of pyatv 0.18.0) has five
/// values (Unsupported, Disabled, NotNeeded, Optional, Mandatory), but MRP's own
/// <c>service_info</c> (pyatv/protocols/mrp/__init__.py — line 1085-1096 as of pyatv 0.18.0) only ever
/// produces three of them, so only those three are modeled here rather than inventing meanings for
/// the unused values.
/// </remarks>
// pyatv/protocols/mrp/__init__.py (service_info) — line 1085-1096 as of pyatv 0.18.0
public enum MrpPairingRequirement
	{
	/// <summary>Pairing is not needed (the service is disabled, e.g. tvOS 15+). pyatv/protocols/mrp/__init__.py — line 1091-1092 as of pyatv 0.18.0</summary>
	NotNeeded,

	/// <summary>Pairing is supported but not required ("allowpairing" is "yes"). pyatv/protocols/mrp/__init__.py — line 1093-1094 as of pyatv 0.18.0</summary>
	Optional,

	/// <summary>Pairing has been disabled on this device. pyatv/protocols/mrp/__init__.py — line 1095-1096 as of pyatv 0.18.0</summary>
	Disabled,
	}
