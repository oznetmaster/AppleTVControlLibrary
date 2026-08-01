using System.Collections.Generic;

using AppleTvControlLibrary.Discovery.Companion;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AppleTV.Companion.Tests.Discovery;

/// <summary>
/// Targeted tests for Companion-specific TXT-record parsing, since pyatv doesn't ship a
/// dedicated discovery test file for these rules.
/// </summary>
[TestClass]
public class CompanionServiceInfoTests
	{
	// pyatv/helpers.py:73-76 (get_unique_id, COMPANION_SERVICE branch)
	[TestMethod]
	public void GetUniqueId_ReturnsRpmrtidValue ()
		{
		Dictionary<string, string> properties = new Dictionary<string, string> { ["rpmrtid"] = "ABCDEF123456" };

		string? uniqueId = CompanionServiceInfo.GetUniqueId (properties);

		Assert.AreEqual ("ABCDEF123456", uniqueId);
		}

	[TestMethod]
	public void GetUniqueId_ReturnsNullWhenMissing ()
		{
		Dictionary<string, string> properties = new Dictionary<string, string> ();

		string? uniqueId = CompanionServiceInfo.GetUniqueId (properties);

		Assert.IsNull (uniqueId);
		}

	// pyatv/protocols/companion/__init__.py:56-79, 648-660 (service_info + masks)
	[TestMethod]
	public void GetPairingRequirement_DisabledMaskWins ()
		{
		Dictionary<string, string> properties = new Dictionary<string, string> { ["rpfl"] = "0x627B6" };

		CompanionPairingRequirement requirement = CompanionServiceInfo.GetPairingRequirement (properties);

		Assert.AreEqual (CompanionPairingRequirement.Disabled, requirement);
		}

	[TestMethod]
	public void GetPairingRequirement_PinSupportedMask ()
		{
		Dictionary<string, string> properties = new Dictionary<string, string> { ["rpfl"] = "0x367A2" };

		CompanionPairingRequirement requirement = CompanionServiceInfo.GetPairingRequirement (properties);

		Assert.AreEqual (CompanionPairingRequirement.Mandatory, requirement);
		}

	[TestMethod]
	public void GetPairingRequirement_NoFlagsIsUnsupported ()
		{
		Dictionary<string, string> properties = new Dictionary<string, string> { ["rpfl"] = "0x20000" };

		CompanionPairingRequirement requirement = CompanionServiceInfo.GetPairingRequirement (properties);

		Assert.AreEqual (CompanionPairingRequirement.Unsupported, requirement);
		}

	[TestMethod]
	public void GetPairingRequirement_MissingPropertyIsUnsupported ()
		{
		Dictionary<string, string> properties = new Dictionary<string, string> ();

		CompanionPairingRequirement requirement = CompanionServiceInfo.GetPairingRequirement (properties);

		Assert.AreEqual (CompanionPairingRequirement.Unsupported, requirement);
		}
	}
