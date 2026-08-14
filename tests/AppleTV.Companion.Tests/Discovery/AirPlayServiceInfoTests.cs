// Copyright (c) 2026 Neil Colvin. Licensed under the MIT License.
// See LICENSE file in the repository root for full license text.

using System.Collections.Generic;

using AppleTvControlLibrary.Discovery.AirPlay;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AppleTV.Companion.Tests.Discovery;

/// <summary>
/// Targeted tests for AirPlay-specific TXT-record parsing, since pyatv doesn't ship a
/// dedicated discovery test file for these rules.
/// </summary>
[TestClass]
public class AirPlayServiceInfoTests
	{
	// pyatv/support/device_info.py (_MODEL_LIST) — line 11-18 as of pyatv 0.18.0
	[TestMethod]
	public void IsAppleTv_TrueForAppleTvModel ()
		{
		Dictionary<string, string> properties = new Dictionary<string, string> { ["model"] = "AppleTV14,1" };

		Assert.IsTrue (AirPlayServiceInfo.IsAppleTv (properties));
		}

	[TestMethod]
	public void IsAppleTv_FalseForNonAppleTvModel ()
		{
		Dictionary<string, string> properties = new Dictionary<string, string> { ["model"] = "DM-NAX-4ZSA-50" };

		Assert.IsFalse (AirPlayServiceInfo.IsAppleTv (properties));
		}

	[TestMethod]
	public void IsAppleTv_FalseWhenModelMissing ()
		{
		Dictionary<string, string> properties = new Dictionary<string, string> ();

		Assert.IsFalse (AirPlayServiceInfo.IsAppleTv (properties));
		}

	[TestMethod]
	public void RemoveNameCollisionSuffix_StripsTrailingParentheticalNumber ()
		{
		Assert.AreEqual ("Office", AirPlayServiceInfo.RemoveNameCollisionSuffix ("Office (2)"));
		}

	[TestMethod]
	public void RemoveNameCollisionSuffix_LeavesPlainNameUnchanged ()
		{
		Assert.AreEqual ("Office", AirPlayServiceInfo.RemoveNameCollisionSuffix ("Office"));
		}

	[TestMethod]
	public void RemoveNameCollisionSuffix_LeavesNonSuffixParenthesesUnchanged ()
		{
		Assert.AreEqual ("Office (Downstairs)", AirPlayServiceInfo.RemoveNameCollisionSuffix ("Office (Downstairs)"));
		}
	}
