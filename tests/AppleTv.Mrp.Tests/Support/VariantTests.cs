// Copyright (c) 2026 Neil Colvin. Licensed under the MIT License.
// See LICENSE file in the repository root for full license text.

using AppleTvControlLibrary.Mrp.Support;

using NUnit.Framework;

namespace AppleTvControlLibrary.Mrp.Tests.Support;

/// <summary>
/// Ported from pyatv/tests/support/test_variant.py (pyatv 0.18.0).
/// </summary>
[TestFixture]
[FixtureLifeCycle (LifeCycle.InstancePerTestCase)]
public class VariantTests
	{
	// tests/support/test_variant.py:7-9 (test_read_single_byte)
	[Test]
	public void ReadSingleByte ()
		{
		Assert.That (Variant.ReadVariant ([0x00]).Value, Is.EqualTo (0x00));
		Assert.That (Variant.ReadVariant ([0x35]).Value, Is.EqualTo (0x35));
		}

	// tests/support/test_variant.py:12-14 (test_read_multiple_bytes)
	[Test]
	public void ReadMultipleBytes ()
		{
		Assert.That (Variant.ReadVariant ([0xb5, 0x44]).Value, Is.EqualTo (8757));
		Assert.That (Variant.ReadVariant ([0xc5, 0x92, 0x01]).Value, Is.EqualTo (18757));
		}

	// tests/support/test_variant.py:17-20 (test_read_and_return_remaining_data)
	[Test]
	public void ReadAndReturnRemainingData ()
		{
		(long value, byte[] remaining) = Variant.ReadVariant ([0xb5, 0x44, 0xca, 0xfe]);
		Assert.That (value, Is.EqualTo (8757));
		Assert.That (remaining, Is.EqualTo (new byte[] { 0xca, 0xfe }));
		}

	// tests/support/test_variant.py:23-25 (test_read_invalid_variant)
	[Test]
	public void ReadInvalidVariant ()
		{
		Assert.Catch<System.ArgumentException> (() => Variant.ReadVariant ([0x80]));
		}

	// tests/support/test_variant.py:28-30 (test_write_single_byte)
	[Test]
	public void WriteSingleByte ()
		{
		Assert.That (Variant.WriteVariant (0x00), Is.EqualTo (new byte[] { 0x00 }));
		Assert.That (Variant.WriteVariant (0x35), Is.EqualTo (new byte[] { 0x35 }));
		}

	// tests/support/test_variant.py:33-35 (test_write_multiple_bytes)
	[Test]
	public void WriteMultipleBytes ()
		{
		Assert.That (Variant.WriteVariant (8757), Is.EqualTo (new byte[] { 0xb5, 0x44 }));
		Assert.That (Variant.WriteVariant (18757), Is.EqualTo (new byte[] { 0xc5, 0x92, 0x01 }));
		}
	}