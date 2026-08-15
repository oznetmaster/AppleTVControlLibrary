// Copyright (c) 2026 Neil Colvin. Licensed under the MIT License.
// See LICENSE file in the repository root for full license text.

using System.Collections.Generic;
using System.Text.RegularExpressions;

using AppleTvControlLibrary.Discovery.Mdns;

namespace AppleTvControlLibrary.Discovery.Mrp;

/// <summary>MRP (Media Remote Protocol) specific constants and parsing helpers for mDNS discovery.</summary>
public static class MrpServiceInfo
	{
	/// <summary>The MRP mDNS service type.</summary>
	// pyatv/helpers.py (MEDIAREMOTE_SERVICE) — line 12 as of pyatv 0.18.0
	public const string SERVICE_TYPE = "_mediaremotetv._tcp.local";

	// pyatv/protocols/mrp/__init__.py (mrp_service_handler) — line 1029-1035 as of pyatv 0.18.0:
	// matches the leading numeric build component of "SystemBuildVersion" (e.g. "19J346" -> 19).
	private static readonly Regex BuildVersionPrefix = new (@"^(\d+)[A-Z]", RegexOptions.Compiled);

	/// <summary>
	/// Returns the stable unique identifier for an MRP service, from the "UniqueIdentifier"
	/// TXT record, or <see langword="null"/> if not present.
	/// </summary>
	/// <param name="properties">The service's decoded TXT record properties.</param>
	/// <returns>The unique identifier, or <see langword="null"/>.</returns>
	// pyatv/helpers.py (get_unique_id, MEDIAREMOTE_SERVICE branch) — line 69-70 as of pyatv 0.18.0
	public static string? GetUniqueId (IReadOnlyDictionary<string, string> properties) => properties.TryGetValue ("UniqueIdentifier", out string? value) ? value : null;

	/// <summary>
	/// Returns whether MRP is considered enabled for the given service's properties. pyatv disables
	/// MRP once the "SystemBuildVersion" leading numeric build component is 19 or higher (tvOS 15+).
	/// A missing or unparseable "SystemBuildVersion" is treated as enabled, matching pyatv's
	/// behavior when its regex match fails.
	/// </summary>
	/// <param name="properties">The service's decoded TXT record properties.</param>
	/// <returns><see langword="true"/> if MRP should be considered enabled for this service.</returns>
	// pyatv/protocols/mrp/__init__.py (mrp_service_handler) — line 1029-1035 as of pyatv 0.18.0
	public static bool IsEnabled (IReadOnlyDictionary<string, string> properties)
		{
		string build = properties.TryGetValue ("SystemBuildVersion", out string? value) ? value : string.Empty;
		Match match = BuildVersionPrefix.Match (build);
		return !match.Success ? true : !(int.TryParse (match.Groups[1].Value, out int baseVersion) && baseVersion >= 19);
		}

	/// <summary>
	/// Derives the pairing requirement for an MRP service from its enabled state and "allowpairing"
	/// TXT record.
	/// </summary>
	/// <param name="isEnabled">Whether MRP is considered enabled for this service, from <see cref="IsEnabled"/>.</param>
	/// <param name="properties">The service's decoded TXT record properties.</param>
	/// <returns>The derived pairing requirement.</returns>
	// pyatv/protocols/mrp/__init__.py (service_info) — line 1085-1096 as of pyatv 0.18.0
	public static MrpPairingRequirement GetPairingRequirement (bool isEnabled, IReadOnlyDictionary<string, string> properties)
		{
		if (!isEnabled)
			{
			return MrpPairingRequirement.NotNeeded;
			}

		string allowPairing = properties.TryGetValue ("allowpairing", out string? value) ? value : "no";
		return string.Equals (allowPairing, "yes", System.StringComparison.OrdinalIgnoreCase)
			? MrpPairingRequirement.Optional
			: MrpPairingRequirement.Disabled;
		}

	/// <summary>Converts a parsed mDNS <see cref="Service"/> into an <see cref="MrpDiscoveryResult"/>.</summary>
	/// <param name="service">The parsed mDNS service.</param>
	/// <returns>The MRP-specific discovery result.</returns>
	// pyatv/protocols/mrp/__init__.py (mrp_service_handler) — line 1025-1046 as of pyatv 0.18.0
	public static MrpDiscoveryResult ToDiscoveryResult (Service service)
		{
		bool isEnabled = IsEnabled (service.Properties);
		return new MrpDiscoveryResult (
			service.Name,
			service.Address,
			service.Port,
			GetUniqueId (service.Properties),
			isEnabled,
			GetPairingRequirement (isEnabled, service.Properties),
			service.Properties);
		}
	}
