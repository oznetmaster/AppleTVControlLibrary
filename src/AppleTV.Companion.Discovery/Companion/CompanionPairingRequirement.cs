// Copyright (c) 2026 Neil Colvin. Licensed under the MIT License.
// See LICENSE file in the repository root for full license text.

namespace AppleTvControlLibrary.Discovery.Companion;

/// <summary>Whether pairing is required, supported, or disabled for a discovered service.</summary>
// pyatv/protocols/companion/__init__.py (service_info) — line 654-660 as of pyatv 0.18.0, pyatv/const.py (PairingRequirement)
public enum CompanionPairingRequirement
	{
	/// <summary>Pairing is not supported by this device.</summary>
	Unsupported,

	/// <summary>Pairing is supported and may be required.</summary>
	Mandatory,

	/// <summary>Pairing has been disabled on this device (e.g. restricted to same-home devices).</summary>
	Disabled,
	}
