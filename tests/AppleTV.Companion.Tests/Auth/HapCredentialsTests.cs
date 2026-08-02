using System;

using AppleTvControlLibrary.Auth;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AppleTV.Companion.Tests.AuthTests;

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
		CollectionAssert.AreEqual (new byte[] { 0x03, 0x04 }, creds.Ltsk);
		CollectionAssert.AreEqual (new byte[] { 0x01, 0x02 }, creds.ClientId);
		}

	[TestMethod]
	public void ParseFourPartIsHap ()
		{
		HapCredentials creds = HapCredentials.Parse ("01:02:03:04");

		Assert.AreEqual (AuthenticationType.Hap, creds.Type);
		CollectionAssert.AreEqual (new byte[] { 0x01 }, creds.Ltpk);
		CollectionAssert.AreEqual (new byte[] { 0x02 }, creds.Ltsk);
		CollectionAssert.AreEqual (new byte[] { 0x03 }, creds.AtvId);
		CollectionAssert.AreEqual (new byte[] { 0x04 }, creds.ClientId);
		}

	[TestMethod]
	public void RoundTripToStringAndParse ()
		{
		var creds = new HapCredentials (
			new byte[] { 0xAA, 0xBB },
			new byte[] { 0xCC },
			new byte[] { 0xDD, 0xDD },
			new byte[] { 0xEE });

		HapCredentials roundTripped = HapCredentials.Parse (creds.ToString ());

		Assert.AreEqual (creds, roundTripped);
		}

	[TestMethod]
	public void TransientCredentialsHaveTransientType ()
		{
		Assert.AreEqual (AuthenticationType.Transient, HapCredentials.TransientCredentials.Type);
		}

	[TestMethod]
	public void InvalidCombinationThrows ()
		{
		Assert.ThrowsException<InvalidCredentialsException> (() => new HapCredentials (ltpk: new byte[] { 0x01 }));
		}
	}
