// Copyright (c) 2026 Neil Colvin. Licensed under the MIT License.
// See LICENSE file in the repository root for full license text.

using System.Text;

using AppleTvControlLibrary.Crypto;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AppleTV.Companion.Tests.Crypto;

/// <summary>
/// Ported from pyatv/tests/support/test_chacha20.py (pyatv 0.18.0).
/// </summary>
[TestClass]
public class Chacha20CipherTests
	{
	// tests/support/test_chacha20.py:7 (fake_key)
	private static readonly byte[] FakeKey = Encoding.ASCII.GetBytes (new string ('k', 32));

	// tests/support/test_chacha20.py:10-15 (test_12_bytes_nonce)
	[TestMethod]
	public void TwelveByteNonce ()
		{
		var cipher = new Chacha20Cipher (FakeKey, FakeKey, 12);
		Assert.AreEqual (12, cipher.OutNonce.Length);
		Assert.AreEqual (12, cipher.InNonce.Length);

		byte[] result = cipher.Encrypt (Encoding.ASCII.GetBytes ("test"));
		CollectionAssert.AreEqual (Encoding.ASCII.GetBytes ("test"), cipher.Decrypt (result));
		}

	// tests/support/test_chacha20.py:18-23 (test_8_bytes_nonce)
	[TestMethod]
	public void EightByteNonce ()
		{
		var cipher = new Chacha20Cipher8ByteNonce (FakeKey, FakeKey);
		Assert.AreEqual (12, cipher.OutNonce.Length);
		Assert.AreEqual (12, cipher.InNonce.Length);

		byte[] result = cipher.Encrypt (Encoding.ASCII.GetBytes ("test"));
		CollectionAssert.AreEqual (Encoding.ASCII.GetBytes ("test"), cipher.Decrypt (result));
		}
	}