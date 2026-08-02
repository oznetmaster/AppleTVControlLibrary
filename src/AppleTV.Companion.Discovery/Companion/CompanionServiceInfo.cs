using System;
using System.Collections.Generic;
using System.Globalization;

using AppleTvControlLibrary.Discovery.Mdns;

namespace AppleTvControlLibrary.Discovery.Companion;

/// <summary>Companion Link specific constants and parsing helpers for mDNS discovery.</summary>
public static class CompanionServiceInfo
	{
	/// <summary>The Companion Link mDNS service type.</summary>
	// pyatv/helpers.py:14 (COMPANION_SERVICE)
	public const string SERVICE_TYPE = "_companion-link._tcp.local";

	// pyatv/protocols/companion/__init__.py:56-60 (PAIRING_DISABLED_MASK)
	private const int PAIRING_DISABLED_MASK = 0x04;

	// pyatv/protocols/companion/__init__.py:62-79 (PAIRING_WITH_PIN_SUPPORTED_MASK)
	private const int PAIRING_WITH_PIN_SUPPORTED_MASK = 0x4000;

	/// <summary>
	/// Returns the stable unique identifier for a Companion service, from the "rpmrtid"
	/// TXT record, or <see langword="null"/> if not present.
	/// </summary>
	/// <remarks>
	/// This identifies the Apple TV, not the client - it is unrelated to the client identity
	/// ("_i") that Companion pairing/session code generates and persists locally.
	/// Also note pyatv's own comment (helpers.py:74) only claims this is static on tvOS 16
	/// "(maybe earlier)"; there is no source statement covering tvOS 18+, so this is an
	/// observation carried from pyatv, not a documented guarantee.
	/// </remarks>
	/// <param name="properties">The service's decoded TXT record properties.</param>
	/// <returns>The unique identifier, or <see langword="null"/>.</returns>
	// pyatv/helpers.py:73-76 (get_unique_id, COMPANION_SERVICE branch)
	public static string? GetUniqueId (IReadOnlyDictionary<string, string> properties)
		{
		return properties.TryGetValue ("rpmrtid", out string? value) ? value : null;
		}

	/// <summary>
	/// Derives the pairing requirement for a Companion service from its "rpfl" TXT record.
	/// </summary>
	/// <param name="properties">The service's decoded TXT record properties.</param>
	/// <returns>The derived pairing requirement.</returns>
	// pyatv/protocols/companion/__init__.py:648-660 (service_info)
	public static CompanionPairingRequirement GetPairingRequirement (IReadOnlyDictionary<string, string> properties)
		{
		string flagsText = properties.TryGetValue ("rpfl", out string? value) ? value : "0x0";
		string trimmed = flagsText.StartsWith ("0x", StringComparison.OrdinalIgnoreCase)
			? flagsText.Substring (2)
			: flagsText;

		int flags = int.TryParse (trimmed, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int parsed) ? parsed : 0;

		if ((flags & PAIRING_DISABLED_MASK) != 0)
			{
			return CompanionPairingRequirement.Disabled;
			}

		if ((flags & PAIRING_WITH_PIN_SUPPORTED_MASK) != 0)
			{
			return CompanionPairingRequirement.Mandatory;
			}

		return CompanionPairingRequirement.Unsupported;
		}

	/// <summary>Converts a parsed mDNS <see cref="Service"/> into a <see cref="CompanionDiscoveryResult"/>.</summary>
	/// <param name="service">The parsed mDNS service.</param>
	/// <returns>The Companion-specific discovery result.</returns>
	// pyatv/protocols/companion/__init__.py:614-624 (companion_service_handler)
	public static CompanionDiscoveryResult ToDiscoveryResult (Service service)
		{
		return new CompanionDiscoveryResult (
			service.Name,
			service.Address,
			service.Port,
			GetUniqueId (service.Properties),
			GetPairingRequirement (service.Properties),
			service.Properties);
		}
	}
