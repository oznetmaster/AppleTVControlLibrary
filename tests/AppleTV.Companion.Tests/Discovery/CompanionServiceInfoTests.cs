// Copyright (c) 2026 Neil Colvin. Licensed under the MIT License.
// See LICENSE file in the repository root for full license text.

using System.Collections.Generic;

using AppleTvControlLibrary.Discovery.Companion;

using NUnit.Framework;

namespace AppleTV.Companion.Tests.Discovery;

/// <summary>
/// Targeted tests for Companion-specific TXT-record parsing, since pyatv doesn't ship a
/// dedicated discovery test file for these rules.
/// </summary>
[TestFixture]
[FixtureLifeCycle (LifeCycle.InstancePerTestCase)]
public class CompanionServiceInfoTests
	{
	// pyatv/helpers.py (get_unique_id, COMPANION_SERVICE branch) — line 73-76 as of pyatv 0.18.0
	[Test]
	public void GetUniqueId_ReturnsRpmrtidValue ()
		{
		Dictionary<string, string> properties = new Dictionary<string, string> { ["rpmrtid"] = "ABCDEF123456" };

		var uniqueId = CompanionServiceInfo.GetUniqueId (properties);

		Assert.That (uniqueId, Is.EqualTo ("ABCDEF123456"));
		}

	[Test]
	public void GetUniqueId_ReturnsNullWhenMissing ()
		{
		Dictionary<string, string> properties = new Dictionary<string, string> ();

		var uniqueId = CompanionServiceInfo.GetUniqueId (properties);

		Assert.That (uniqueId, Is.Null);
		}

	// pyatv/protocols/companion/__init__.py — line 56-79 as of pyatv 0.18.0, 648-660 (service_info + masks)
	[Test]
	public void GetPairingRequirement_DisabledMaskWins ()
		{
		Dictionary<string, string> properties = new Dictionary<string, string> { ["rpfl"] = "0x627B6" };

		CompanionPairingRequirement requirement = CompanionServiceInfo.GetPairingRequirement (properties);

		Assert.That (requirement, Is.EqualTo (CompanionPairingRequirement.Disabled));
		}

	[Test]
	public void GetPairingRequirement_PinSupportedMask ()
		{
		Dictionary<string, string> properties = new Dictionary<string, string> { ["rpfl"] = "0x367A2" };

		CompanionPairingRequirement requirement = CompanionServiceInfo.GetPairingRequirement (properties);

		Assert.That (requirement, Is.EqualTo (CompanionPairingRequirement.Mandatory));
		}

	[Test]
	public void GetPairingRequirement_NoFlagsIsUnsupported ()
		{
		Dictionary<string, string> properties = new Dictionary<string, string> { ["rpfl"] = "0x20000" };

		CompanionPairingRequirement requirement = CompanionServiceInfo.GetPairingRequirement (properties);

		Assert.That (requirement, Is.EqualTo (CompanionPairingRequirement.Unsupported));
		}

	[Test]
	public void GetPairingRequirement_MissingPropertyIsUnsupported ()
		{
		Dictionary<string, string> properties = new Dictionary<string, string> ();

		CompanionPairingRequirement requirement = CompanionServiceInfo.GetPairingRequirement (properties);

		Assert.That (requirement, Is.EqualTo (CompanionPairingRequirement.Unsupported));
		}
	}