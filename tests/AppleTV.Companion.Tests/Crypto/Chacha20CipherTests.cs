// Copyright (c) 2026 Neil Colvin. Licensed under the MIT License.
// See LICENSE file in the repository root for full license text.

using System.Text;

using AppleTvControlLibrary.Crypto;

using NUnit.Framework;

namespace AppleTV.Companion.Tests.Crypto;

/// <summary>
/// Ported from pyatv/tests/support/test_chacha20.py (pyatv 0.18.0).
/// </summary>
[TestFixture]
[FixtureLifeCycle (LifeCycle.InstancePerTestCase)]
public class Chacha20CipherTests
	{
	// tests/support/test_chacha20.py:7 (fake_key)
	private static readonly byte[] FakeKey = Encoding.ASCII.GetBytes (new string ('k', 32));

	// tests/support/test_chacha20.py:10-15 (test_12_bytes_nonce)
	[Test]
	public void TwelveByteNonce ()
		{
		var cipher = new Chacha20Cipher (FakeKey, FakeKey, 12);
		Assert.That (cipher.OutNonce, Has.Length.EqualTo (12));
		Assert.That (cipher.InNonce, Has.Length.EqualTo (12));

		var result = cipher.Encrypt (Encoding.ASCII.GetBytes ("test"));
		Assert.That (cipher.Decrypt (result), Is.EqualTo (Encoding.ASCII.GetBytes ("test")));
		}

	// tests/support/test_chacha20.py:18-23 (test_8_bytes_nonce)
	[Test]
	public void EightByteNonce ()
		{
		var cipher = new Chacha20Cipher8ByteNonce (FakeKey, FakeKey);
		Assert.That (cipher.OutNonce, Has.Length.EqualTo (12));
		Assert.That (cipher.InNonce, Has.Length.EqualTo (12));

		var result = cipher.Encrypt (Encoding.ASCII.GetBytes ("test"));
		Assert.That (cipher.Decrypt (result), Is.EqualTo (Encoding.ASCII.GetBytes ("test")));
		}
	}