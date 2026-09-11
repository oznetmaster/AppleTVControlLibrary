// Copyright (c) 2026 Neil Colvin. Licensed under the MIT License.
// See LICENSE file in the repository root for full license text.

using System;

using AppleTvControlLibrary.Auth;

using NUnit.Framework;

namespace AppleTV.Companion.Tests.AuthTests;

/// <summary>
/// Ported from pyatv/tests/auth/test_hap_pairing.py (pyatv 0.18.0).
/// </summary>
[TestFixture]
[FixtureLifeCycle (LifeCycle.InstancePerTestCase)]
public class HapCredentialsTests
	{
	// pyatv/auth/hap_pairing.py (parse_credentials) — line 139-152 as of pyatv 0.18.0
	[Test]
	public void ParseNullReturnsNoCredentials ()
		{
		HapCredentials creds = HapCredentials.Parse (null);

		Assert.That (creds.Type, Is.EqualTo (AuthenticationType.Null));
		Assert.That (creds, Is.EqualTo (HapCredentials.NoCredentials));
		}

	[Test]
	public void ParseTwoPartIsLegacy ()
		{
		HapCredentials creds = HapCredentials.Parse ("0102:0304");

		Assert.That (creds.Type, Is.EqualTo (AuthenticationType.Legacy));
		Assert.That (creds.Ltsk, Is.EqualTo (new byte[] { 0x03, 0x04 }));
		Assert.That (creds.ClientId, Is.EqualTo (new byte[] { 0x01, 0x02 }));
		}

	[Test]
	public void ParseFourPartIsHap ()
		{
		HapCredentials creds = HapCredentials.Parse ("01:02:03:04");

		Assert.That (creds.Type, Is.EqualTo (AuthenticationType.Hap));
		Assert.That (creds.Ltpk, Is.EqualTo (new byte[] { 0x01 }));
		Assert.That (creds.Ltsk, Is.EqualTo (new byte[] { 0x02 }));
		Assert.That (creds.AtvId, Is.EqualTo (new byte[] { 0x03 }));
		Assert.That (creds.ClientId, Is.EqualTo (new byte[] { 0x04 }));
		}

	[Test]
	public void RoundTripToStringAndParse ()
		{
		var creds = new HapCredentials (
			[0xAA, 0xBB],
			[0xCC],
			[0xDD, 0xDD],
			[0xEE]);

		HapCredentials roundTripped = HapCredentials.Parse (creds.ToString ());

		Assert.That (roundTripped, Is.EqualTo (creds));
		}

	[Test]
	public void TransientCredentialsHaveTransientType ()
		{
		Assert.That (HapCredentials.TransientCredentials.Type, Is.EqualTo (AuthenticationType.Transient));
		}

	[Test]
	public void InvalidCombinationThrows ()
		{
		Assert.Catch<InvalidCredentialsException> (() => new HapCredentials (ltpk: [0x01]));
		}
	}