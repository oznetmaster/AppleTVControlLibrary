// Copyright (c) 2026 Neil Colvin. Licensed under the MIT License.
// See LICENSE file in the repository root for full license text.

using AppleTvControlLibrary.Mrp.Support;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AppleTvControlLibrary.Mrp.Tests.Support;

/// <summary>
/// Ported from pyatv/tests/support/test_variant.py (pyatv 0.18.0).
/// </summary>
[TestClass]
public class VariantTests
	{
	// tests/support/test_variant.py:7-9 (test_read_single_byte)
	[TestMethod]
	public void ReadSingleByte ()
		{
		Assert.AreEqual (0x00, Variant.ReadVariant ([0x00]).Value);
		Assert.AreEqual (0x35, Variant.ReadVariant ([0x35]).Value);
		}

	// tests/support/test_variant.py:12-14 (test_read_multiple_bytes)
	[TestMethod]
	public void ReadMultipleBytes ()
		{
		Assert.AreEqual (8757, Variant.ReadVariant ([0xb5, 0x44]).Value);
		Assert.AreEqual (18757, Variant.ReadVariant ([0xc5, 0x92, 0x01]).Value);
		}

	// tests/support/test_variant.py:17-20 (test_read_and_return_remaining_data)
	[TestMethod]
	public void ReadAndReturnRemainingData ()
		{
		(long value, byte[] remaining) = Variant.ReadVariant ([0xb5, 0x44, 0xca, 0xfe]);
		Assert.AreEqual (8757, value);
		CollectionAssert.AreEqual (new byte[] { 0xca, 0xfe }, remaining);
		}

	// tests/support/test_variant.py:23-25 (test_read_invalid_variant)
	[TestMethod]
	public void ReadInvalidVariant ()
		{
		Assert.Throws<System.ArgumentException> (() => Variant.ReadVariant ([0x80]));
		}

	// tests/support/test_variant.py:28-30 (test_write_single_byte)
	[TestMethod]
	public void WriteSingleByte ()
		{
		CollectionAssert.AreEqual (new byte[] { 0x00 }, Variant.WriteVariant (0x00));
		CollectionAssert.AreEqual (new byte[] { 0x35 }, Variant.WriteVariant (0x35));
		}

	// tests/support/test_variant.py:33-35 (test_write_multiple_bytes)
	[TestMethod]
	public void WriteMultipleBytes ()
		{
		CollectionAssert.AreEqual (new byte[] { 0xb5, 0x44 }, Variant.WriteVariant (8757));
		CollectionAssert.AreEqual (new byte[] { 0xc5, 0x92, 0x01 }, Variant.WriteVariant (18757));
		}
	}
