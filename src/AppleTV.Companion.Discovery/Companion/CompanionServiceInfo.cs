// Copyright (c) 2026 Neil Colvin. Licensed under the MIT License.
// See LICENSE file in the repository root for full license text.

using System;
using System.Collections.Generic;

using AppleTvControlLibrary.Discovery.Mdns;

namespace AppleTvControlLibrary.Discovery.Companion;

/// <summary>Companion Link specific constants and parsing helpers for mDNS discovery.</summary>
public static class CompanionServiceInfo
	{
	/// <summary>The Companion Link mDNS service type.</summary>
	// pyatv/helpers.py (COMPANION_SERVICE) — line 14 as of pyatv 0.18.0
	public const string SERVICE_TYPE = "_companion-link._tcp.local";

	// pyatv/protocols/companion/__init__.py (PAIRING_DISABLED_MASK) — line 56-60 as of pyatv 0.18.0
	private const int PAIRING_DISABLED_MASK = 0x04;

	// pyatv/protocols/companion/__init__.py (PAIRING_WITH_PIN_SUPPORTED_MASK) — line 62-79 as of pyatv 0.18.0
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
	// pyatv/helpers.py (get_unique_id, COMPANION_SERVICE branch) — line 73-76 as of pyatv 0.18.0
	public static string? GetUniqueId (IReadOnlyDictionary<string, string> properties) => properties.TryGetValue ("rpmrtid", out string? value) ? value : null;

	/// <summary>
	/// Derives the pairing requirement for a Companion service from its "rpfl" TXT record.
	/// </summary>
	/// <param name="properties">The service's decoded TXT record properties.</param>
	/// <returns>The derived pairing requirement.</returns>
	// pyatv/protocols/companion/__init__.py (service_info) — line 648-660 as of pyatv 0.18.0
	public static CompanionPairingRequirement GetPairingRequirement (IReadOnlyDictionary<string, string> properties)
		{
		string flagsText = properties.TryGetValue ("rpfl", out string? value) ? value : "0x0";
		ReadOnlySpan<char> trimmed = flagsText.StartsWith ("0x", StringComparison.OrdinalIgnoreCase)
			? flagsText.AsSpan (2)
			: flagsText.AsSpan ();

		int flags = TryParseHex (trimmed, out int parsed) ? parsed : 0;

		return (flags & PAIRING_DISABLED_MASK) != 0
			? CompanionPairingRequirement.Disabled
			: (flags & PAIRING_WITH_PIN_SUPPORTED_MASK) != 0
			? CompanionPairingRequirement.Mandatory
			: CompanionPairingRequirement.Unsupported;
		}

	// int.TryParse(ReadOnlySpan<char>, NumberStyles, ...) is not available on net472, even with
	// Microsoft.Bcl.Memory (which only provides the Span<T>/Memory<T> types themselves, not new
	// BCL method overloads), so hex digits are parsed manually here to avoid a Substring allocation.
	private static bool TryParseHex (ReadOnlySpan<char> text, out int value)
		{
		value = 0;
		if (text.Length == 0)
			{
			return false;
			}

		foreach (char c in text)
			{
			int digit = c switch
				{
				>= '0' and <= '9' => c - '0',
				>= 'a' and <= 'f' => c - 'a' + 10,
				>= 'A' and <= 'F' => c - 'A' + 10,
				_ => -1,
				};

			if (digit < 0)
				{
				value = 0;
				return false;
				}

			value = (value << 4) | digit;
			}

		return true;
		}

	/// <summary>Converts a parsed mDNS <see cref="Service"/> into a <see cref="CompanionDiscoveryResult"/>.</summary>
	/// <param name="service">The parsed mDNS service.</param>
	/// <returns>The Companion-specific discovery result.</returns>
	// pyatv/protocols/companion/__init__.py (companion_service_handler) — line 614-624 as of pyatv 0.18.0
	public static CompanionDiscoveryResult ToDiscoveryResult (Service service) => new (
			service.Name,
			service.Address,
			service.Port,
			GetUniqueId (service.Properties),
			GetPairingRequirement (service.Properties),
			service.Properties);
	}
