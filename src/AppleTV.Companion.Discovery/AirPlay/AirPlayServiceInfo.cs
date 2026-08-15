// Copyright (c) 2026 Neil Colvin. Licensed under the MIT License.
// See LICENSE file in the repository root for full license text.

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

using AppleTvControlLibrary.Discovery.Mdns;

namespace AppleTvControlLibrary.Discovery.AirPlay;

/// <summary>AirPlay specific constants and parsing helpers for mDNS discovery.</summary>
public static class AirPlayServiceInfo
	{
	/// <summary>The AirPlay mDNS service type.</summary>
	// pyatv/helpers.py (AIRPLAY_SERVICE) — line 16 as of pyatv 0.18.0
	public const string SERVICE_TYPE = "_airplay._tcp.local";

	// pyatv/protocols/airplay/utils.py (PIN_REQUIRED) — line 24 as of pyatv 0.18.0
	private const int PIN_REQUIRED = 0x8;

	// pyatv/protocols/airplay/utils.py (LEGACY_PAIRING_BIT) — line 26 as of pyatv 0.18.0
	private const int LEGACY_PAIRING_BIT = 0x200;

	// pyatv/protocols/airplay/utils.py (UNSUPPORTED_MODELS) — line 31 as of pyatv 0.18.0
	private static readonly Regex[] UnsupportedModels = new[]
		{
		new Regex (@"^Mac\d+,\d+$", RegexOptions.Compiled),
		};

	// pyatv/support/device_info.py (_MODEL_LIST) — line 11-18 as of pyatv 0.18.0 lists only
	// "AppleTV<n>,<n>" style identifiers for Apple TV hardware (AirPortExpress, AudioAccessory,
	// and other non-Apple-TV AirPlay endpoints use different identifier prefixes).
	private static readonly Regex AppleTvModel = new (@"^AppleTV\d+,\d+$", RegexOptions.Compiled);

	// mDNS appends " (n)" to an instance name when it collides with another advertised service
	// name on the network. Not a pyatv concept — this is standard DNS-SD instance-name
	// disambiguation (RFC 6763 §6.6) applied by whatever mDNS responder is on the LAN.
	private static readonly Regex NameCollisionSuffix = new (@" \(\d+\)$", RegexOptions.Compiled);

	/// <summary>
	/// Determines whether a discovered AirPlay service's "model" TXT record identifies it as an
	/// Apple TV, as opposed to another AirPlay-capable device (e.g. a receiver or speaker).
	/// </summary>
	/// <param name="properties">The service's decoded TXT record properties.</param>
	/// <returns><see langword="true"/> if the "model" property matches the Apple TV identifier pattern.</returns>
	// pyatv/support/device_info.py (_MODEL_LIST, lookup_model) — line 7-19, 101-103 as of pyatv 0.18.0
	public static bool IsAppleTv (IReadOnlyDictionary<string, string> properties) => properties.TryGetValue ("model", out string? model) && AppleTvModel.IsMatch (model);

	/// <summary>
	/// Removes a trailing mDNS instance-name disambiguation suffix (e.g. " (2)") from a service
	/// name, for display and storage purposes. The suffix is added by the network's mDNS
	/// responder when the same name is advertised by more than one device (e.g. an Apple TV and
	/// an unrelated AirPlay device both named "Office") and carries no protocol meaning.
	/// </summary>
	/// <param name="name">The raw mDNS service instance name.</param>
	/// <returns>The name with any trailing " (n)" suffix removed.</returns>
	public static string RemoveNameCollisionSuffix (string name) => NameCollisionSuffix.Replace (name, string.Empty);

	/// <summary>
	/// Returns the stable unique identifier for an AirPlay service, from the "deviceid"
	/// TXT record, or <see langword="null"/> if not present.
	/// </summary>
	/// <param name="properties">The service's decoded TXT record properties.</param>
	/// <returns>The unique identifier, or <see langword="null"/>.</returns>
	// pyatv/helpers.py (get_unique_id, AIRPLAY_SERVICE branch) — line 69-70 as of pyatv 0.18.0
	public static string? GetUniqueId (IReadOnlyDictionary<string, string> properties) => properties.TryGetValue ("deviceid", out string? value) ? value : null;

	// pyatv/protocols/airplay/utils.py (_get_flags) — line 40-42 as of pyatv 0.18.0
	private static int GetFlags (IReadOnlyDictionary<string, string> properties)
		{
		string flagsText = properties.TryGetValue ("sf", out string? sf)
			? sf
			: properties.TryGetValue ("flags", out string? flags) ? flags : "0x0";

		ReadOnlySpan<char> trimmed = flagsText.StartsWith ("0x", StringComparison.OrdinalIgnoreCase)
			? flagsText.AsSpan (2)
			: flagsText.AsSpan ();

		return TryParseHex (trimmed, out int parsed) ? parsed : 0;
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

	/// <summary>
	/// Derives the pairing requirement for an AirPlay service from its "acl", "model", and
	/// "sf"/"flags"/"act" TXT records.
	/// </summary>
	/// <param name="properties">The service's decoded TXT record properties.</param>
	/// <returns>The derived pairing requirement.</returns>
	// pyatv/protocols/airplay/utils.py (update_service_details, get_pairing_requirement) — line 262-278, 139-157 as of pyatv 0.18.0
	public static AirPlayPairingRequirement GetPairingRequirement (IReadOnlyDictionary<string, string> properties)
		{
		// pyatv/protocols/airplay/utils.py (update_service_details) — line 266-269 as of pyatv 0.18.0
		if (properties.TryGetValue ("acl", out string? acl) && acl == "1")
			{
			return AirPlayPairingRequirement.Disabled;
			}

		// pyatv/protocols/airplay/utils.py (update_service_details) — line 270-276 as of pyatv 0.18.0
		if (properties.TryGetValue ("model", out string? model))
			{
			foreach (Regex unsupportedModel in UnsupportedModels)
				{
				if (unsupportedModel.IsMatch (model))
					{
					return AirPlayPairingRequirement.Unsupported;
					}
				}
			}

		// pyatv/protocols/airplay/utils.py (get_pairing_requirement) — line 151-152 as of pyatv 0.18.0
		if ((GetFlags (properties) & (LEGACY_PAIRING_BIT | PIN_REQUIRED)) != 0)
			{
			return AirPlayPairingRequirement.Mandatory;
			}

		// pyatv/protocols/airplay/utils.py (get_pairing_requirement, "Current User" not supported) — line 154-156 as of pyatv 0.18.0
		if (properties.TryGetValue ("act", out string? act) && act == "2")
			{
			return AirPlayPairingRequirement.Unsupported;
			}

		// pyatv/protocols/airplay/utils.py (get_pairing_requirement) — line 157 as of pyatv 0.18.0
		return AirPlayPairingRequirement.NotNeeded;
		}

	/// <summary>Converts a parsed mDNS <see cref="Service"/> into an <see cref="AirPlayDiscoveryResult"/>.</summary>
	/// <param name="service">The parsed mDNS service.</param>
	/// <returns>The AirPlay-specific discovery result.</returns>
	// pyatv/protocols/airplay/__init__.py (airplay_service_handler) — line 180-190 as of pyatv 0.18.0
	public static AirPlayDiscoveryResult ToDiscoveryResult (Service service) => new (
			service.Name,
			service.Address,
			service.Port,
			GetUniqueId (service.Properties),
			GetPairingRequirement (service.Properties),
			service.Properties);
	}
