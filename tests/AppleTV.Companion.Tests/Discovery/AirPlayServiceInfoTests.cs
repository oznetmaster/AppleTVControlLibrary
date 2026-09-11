// Copyright (c) 2026 Neil Colvin. Licensed under the MIT License.
// See LICENSE file in the repository root for full license text.

using System.Collections.Generic;

using AppleTvControlLibrary.Discovery.AirPlay;

using NUnit.Framework;

namespace AppleTV.Companion.Tests.Discovery;

/// <summary>
/// Targeted tests for AirPlay-specific TXT-record parsing, since pyatv doesn't ship a
/// dedicated discovery test file for these rules.
/// </summary>
[TestFixture]
[FixtureLifeCycle (LifeCycle.InstancePerTestCase)]
public class AirPlayServiceInfoTests
	{
	// pyatv/support/device_info.py (_MODEL_LIST) — line 11-18 as of pyatv 0.18.0
	[Test]
	public void IsAppleTv_TrueForAppleTvModel ()
		{
		Dictionary<string, string> properties = new Dictionary<string, string> { ["model"] = "AppleTV14,1" };

		Assert.That (AirPlayServiceInfo.IsAppleTv (properties), Is.True);
		}

	[Test]
	public void IsAppleTv_FalseForNonAppleTvModel ()
		{
		Dictionary<string, string> properties = new Dictionary<string, string> { ["model"] = "DM-NAX-4ZSA-50" };

		Assert.That (AirPlayServiceInfo.IsAppleTv (properties), Is.False);
		}

	[Test]
	public void IsAppleTv_FalseWhenModelMissing ()
		{
		Dictionary<string, string> properties = new Dictionary<string, string> ();

		Assert.That (AirPlayServiceInfo.IsAppleTv (properties), Is.False);
		}

	[Test]
	public void RemoveNameCollisionSuffix_StripsTrailingParentheticalNumber ()
		{
		Assert.That (AirPlayServiceInfo.RemoveNameCollisionSuffix ("Office (2)"), Is.EqualTo ("Office"));
		}

	[Test]
	public void RemoveNameCollisionSuffix_LeavesPlainNameUnchanged ()
		{
		Assert.That (AirPlayServiceInfo.RemoveNameCollisionSuffix ("Office"), Is.EqualTo ("Office"));
		}

	[Test]
	public void RemoveNameCollisionSuffix_LeavesNonSuffixParenthesesUnchanged ()
		{
		Assert.That (AirPlayServiceInfo.RemoveNameCollisionSuffix ("Office (Downstairs)"), Is.EqualTo ("Office (Downstairs)"));
		}
	}