// Copyright (c) 2026 Neil Colvin. Licensed under the MIT License.
// See LICENSE file in the repository root for full license text.

using System;

using AppleTvControlLibrary.Auth;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AppleTv.Hap.Tests.AuthTests;

/// <summary>
/// Ported from pyatv/tests/auth/test_hap_pairing.py (pyatv 0.18.0).
/// </summary>
[TestClass]
public class HapCredentialsTests
	{
	// pyatv/auth/hap_pairing.py (parse_credentials) — line 139-152 as of pyatv 0.18.0
	[TestMethod]
	public void ParseNullReturnsNoCredentials ()
		{
		HapCredentials creds = HapCredentials.Parse (null);

		Assert.AreEqual (AuthenticationType.Null, creds.Type);
		Assert.AreEqual (HapCredentials.NoCredentials, creds);
		}

	[TestMethod]
	public void ParseTwoPartIsLegacy ()
		{
		HapCredentials creds = HapCredentials.Parse ("0102:0304");

		Assert.AreEqual (AuthenticationType.Legacy, creds.Type);
		Assert.AreSequenceEqual (new byte[] { 0x03, 0x04 }, creds.Ltsk);
		Assert.AreSequenceEqual (new byte[] { 0x01, 0x02 }, creds.ClientId);
		}

	[TestMethod]
	public void ParseFourPartIsHap ()
		{
		HapCredentials creds = HapCredentials.Parse ("01:02:03:04");

		Assert.AreEqual (AuthenticationType.Hap, creds.Type);
		Assert.AreSequenceEqual (new byte[] { 0x01 }, creds.Ltpk);
		Assert.AreSequenceEqual (new byte[] { 0x02 }, creds.Ltsk);
		Assert.AreSequenceEqual (new byte[] { 0x03 }, creds.AtvId);
		Assert.AreSequenceEqual (new byte[] { 0x04 }, creds.ClientId);
		}

	[TestMethod]
	public void RoundTripToStringAndParse ()
		{
		var creds = new HapCredentials (
			[0xAA, 0xBB],
			[0xCC],
			[0xDD, 0xDD],
			[0xEE]);

		HapCredentials roundTripped = HapCredentials.Parse (creds.ToString ());

		Assert.AreEqual (creds, roundTripped);
		}

	[TestMethod]
	public void TransientCredentialsHaveTransientType () => Assert.AreEqual (AuthenticationType.Transient, HapCredentials.TransientCredentials.Type);

	[TestMethod]
	public void InvalidCombinationThrows () => _ = Assert.Throws<InvalidCredentialsException> (() => new HapCredentials (ltpk: [0x01]));
	}
