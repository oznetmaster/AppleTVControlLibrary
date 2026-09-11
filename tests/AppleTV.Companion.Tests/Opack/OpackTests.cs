// Copyright (c) 2026 Neil Colvin. Licensed under the MIT License.
// See LICENSE file in the repository root for full license text.

using System;
using System.Collections.Generic;

using AppleTvControlLibrary.Opack;

using NUnit.Framework;

namespace AppleTV.Companion.Tests.Opack;

/// <summary>
/// Ported from pyatv/tests/support/test_opack.py (pyatv 0.18.0).
/// </summary>
[TestFixture]
[FixtureLifeCycle (LifeCycle.InstancePerTestCase)]
public class OpackTests
	{
	// tests/support/test_opack.py: test_pack_unsupported_type
	[Test]
	public void PackUnsupportedTypeThrows ()
		{
		Assert.Catch<NotSupportedException> (() => AppleTvControlLibrary.Opack.Opack.Pack (new object ()));
		}

	// tests/support/test_opack.py: test_pack_boolean
	[Test]
	public void PackBoolean ()
		{
		Assert.That (AppleTvControlLibrary.Opack.Opack.Pack (true), Is.EqualTo (new byte[] { 0x01 }));
		Assert.That (AppleTvControlLibrary.Opack.Opack.Pack (false), Is.EqualTo (new byte[] { 0x02 }));
		}

	// tests/support/test_opack.py: test_pack_none
	[Test]
	public void PackNone ()
		{
		Assert.That (AppleTvControlLibrary.Opack.Opack.Pack (null), Is.EqualTo (new byte[] { 0x04 }));
		}

	// tests/support/test_opack.py: test_pack_uuid
	[Test]
	public void PackUuid ()
		{
		var guid = new Guid ("12345678-1234-5678-1234-567812345678");
		var expected = Concat ([0x05], guid.ToByteArray ());
		Assert.That (AppleTvControlLibrary.Opack.Opack.Pack (guid), Is.EqualTo (expected));
		}

	// tests/support/test_opack.py: test_pack_absolute_time
	[Test]
	public void PackAbsoluteTimeThrows ()
		{
		Assert.Catch<NotImplementedException> (() => AppleTvControlLibrary.Opack.Opack.Pack (DateTime.Now));
		}

	// tests/support/test_opack.py: test_pack_small_integers
	[Test]
	[TestCase (0L, new byte[] { 0x08 })]
	[TestCase (0xFL, new byte[] { 0x17 })]
	[TestCase (0x27L, new byte[] { 0x2f })]
	public void PackSmallIntegers (long value, byte[] expected)
		{
		Assert.That (AppleTvControlLibrary.Opack.Opack.Pack (value), Is.EqualTo (expected));
		}

	// tests/support/test_opack.py: test_pack_larger_integers
	[Test]
	[TestCase (0x28L, new byte[] { 0x30, 0x28 })]
	[TestCase (0x1FFL, new byte[] { 0x31, 0xff, 0x01 })]
	[TestCase (0x1FFFFFFL, new byte[] { 0x32, 0xff, 0xff, 0xff, 0x01 })]
	[TestCase (0x1FFFFFFFFFFFFFFL, new byte[] { 0x33, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0x01 })]
	public void PackLargerIntegers (long value, byte[] expected)
		{
		Assert.That (AppleTvControlLibrary.Opack.Opack.Pack (value), Is.EqualTo (expected));
		}

	// tests/support/test_opack.py: test_pack_sized_integers
	[Test]
	[TestCase (1, new byte[] { 0x30, 0x01 })]
	[TestCase (2, new byte[] { 0x31, 0x01, 0x00 })]
	[TestCase (4, new byte[] { 0x32, 0x01, 0x00, 0x00, 0x00 })]
	[TestCase (8, new byte[] { 0x33, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 })]
	public void PackSizedIntegers (int size, byte[] expected)
		{
		var value = new SizedInteger (0x1, size);
		Assert.That (AppleTvControlLibrary.Opack.Opack.Pack (value), Is.EqualTo (expected));
		}

	// tests/support/test_opack.py: test_pack_float64
	[Test]
	public void PackFloat64 ()
		{
		byte[] expected = [0x36, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xf0, 0x3f];
		Assert.That (AppleTvControlLibrary.Opack.Opack.Pack (1.0), Is.EqualTo (expected));
		}

	// tests/support/test_opack.py: test_pack_short_strings
	[Test]
	public void PackShortStrings ()
		{
		Assert.That (AppleTvControlLibrary.Opack.Opack.Pack ("a"), Is.EqualTo (new byte[] { 0x41, 0x61 }));
		Assert.That (AppleTvControlLibrary.Opack.Opack.Pack ("abc"), Is.EqualTo (new byte[] { 0x43, 0x61, 0x62, 0x63 }));

		var expected = Concat ([0x60], Repeat ((byte)0x61, 0x20));
		Assert.That (AppleTvControlLibrary.Opack.Opack.Pack (new string ('a', 0x20)), Is.EqualTo (expected));
		}

	// tests/support/test_opack.py: test_pack_longer_strings
	[Test]
	public void PackLongerStrings ()
		{
		var expected33 = Concat ([0x61, 0x21], Repeat ((byte)0x61, 33));
		Assert.That (AppleTvControlLibrary.Opack.Opack.Pack (new string ('a', 33)), Is.EqualTo (expected33));

		var expected256 = Concat ([0x62, 0x00, 0x01], Repeat ((byte)0x61, 256));
		Assert.That (AppleTvControlLibrary.Opack.Opack.Pack (new string ('a', 256)), Is.EqualTo (expected256));
		}

	// tests/support/test_opack.py: test_pack_short_raw_bytes
	[Test]
	public void PackShortRawBytes ()
		{
		Assert.That (AppleTvControlLibrary.Opack.Opack.Pack (new byte[] { 0xac }), Is.EqualTo (new byte[] { 0x71, 0xac }));
		Assert.That (AppleTvControlLibrary.Opack.Opack.Pack (new byte[] { 0x12, 0x34, 0x56 }), Is.EqualTo (new byte[] { 0x73, 0x12, 0x34, 0x56 }));

		var expected = Concat ([0x90], Repeat ((byte)0xad, 0x20));
		Assert.That (AppleTvControlLibrary.Opack.Opack.Pack (Repeat ((byte)0xad, 0x20)), Is.EqualTo (expected));
		}

	// tests/support/test_opack.py: test_pack_longer_raw_bytes
	[Test]
	public void PackLongerRawBytes ()
		{
		var expected33 = Concat ([0x91, 0x21], Repeat ((byte)0x61, 33));
		Assert.That (AppleTvControlLibrary.Opack.Opack.Pack (Repeat ((byte)0x61, 33)), Is.EqualTo (expected33));

		var expected256 = Concat ([0x92, 0x00, 0x01], Repeat ((byte)0x61, 256));
		Assert.That (AppleTvControlLibrary.Opack.Opack.Pack (Repeat ((byte)0x61, 256)), Is.EqualTo (expected256));

		var expected65536 = Concat ([0x93, 0x00, 0x00, 0x01, 0x00], Repeat ((byte)0x61, 65536));
		Assert.That (AppleTvControlLibrary.Opack.Opack.Pack (Repeat ((byte)0x61, 65536)), Is.EqualTo (expected65536));
		}

	// tests/support/test_opack.py: test_pack_array
	[Test]
	public void PackArray ()
		{
		Assert.That (AppleTvControlLibrary.Opack.Opack.Pack (new List<object?> ()), Is.EqualTo (new byte[] { 0xd0 }));

		var list = new List<object?> { 1L, "test", false };
		byte[] expected = [0xd3, 0x09, 0x44, 0x74, 0x65, 0x73, 0x74, 0x02];
		Assert.That (AppleTvControlLibrary.Opack.Opack.Pack (list), Is.EqualTo (expected));

		var nested = new List<object?> { new List<object?> { true } };
		Assert.That (AppleTvControlLibrary.Opack.Opack.Pack (nested), Is.EqualTo (new byte[] { 0xd1, 0xd1, 0x01 }));
		}

	// tests/support/test_opack.py: test_pack_endless_array
	[Test]
	public void PackEndlessArray ()
		{
		var list = new List<object?> ();
		for (var i = 0; i < 15; i++)
			{
			list.Add ("a");
			}

		var expected = Concat ([0xdf, 0x41, 0x61], Concat (Repeat ((byte)0xa0, 14), [0x03]));
		Assert.That (AppleTvControlLibrary.Opack.Opack.Pack (list), Is.EqualTo (expected));
		}

	// tests/support/test_opack.py: test_pack_dict
	[Test]
	public void PackDict ()
		{
		Assert.That (AppleTvControlLibrary.Opack.Opack.Pack (new Dictionary<object, object?> ()), Is.EqualTo (new byte[] { 0xe0 }));

		var dict = new Dictionary<object, object?> { ["a"] = 12L, [false] = null };
		Assert.That (AppleTvControlLibrary.Opack.Opack.Pack (dict), Is.EqualTo (new byte[] { 0xe2, 0x41, 0x61, 0x14, 0x02, 0x04 }));

		var nested = new Dictionary<object, object?> { [true] = new Dictionary<object, object?> { ["a"] = 2L } };
		Assert.That (AppleTvControlLibrary.Opack.Opack.Pack (nested), Is.EqualTo (new byte[] { 0xe1, 0x01, 0xe1, 0x41, 0x61, 0x0a }));
		}

	// tests/support/test_opack.py: test_pack_ptr
	[Test]
	public void PackPtr ()
		{
		Assert.That (AppleTvControlLibrary.Opack.Opack.Pack (new List<object?> { "a", "a" }), Is.EqualTo (new byte[] { 0xd2, 0x41, 0x61, 0xa0 }));

		Assert.That (AppleTvControlLibrary.Opack.Opack.Pack (new List<object?> { "foo", "bar", "foo", "bar" }), Is.EqualTo (new byte[] { 0xd4, 0x43, 0x66, 0x6f, 0x6f, 0x43, 0x62, 0x61, 0x72, 0xa0, 0xa1 }));

		var dict = new Dictionary<object, object?>
			{
			["a"] = "b",
			["c"] = new Dictionary<object, object?> { ["d"] = "a" },
			["d"] = true,
			};
		Assert.That (AppleTvControlLibrary.Opack.Opack.Pack (dict), Is.EqualTo (new byte[] { 0xe3, 0x41, 0x61, 0x41, 0x62, 0x41, 0x63, 0xe1, 0x41, 0x64, 0xa0, 0xa3, 0x01 }));
		}

	// tests/support/test_opack.py: test_unpack_unsupported_type
	[Test]
	public void UnpackUnsupportedTypeThrows ()
		{
		Assert.Catch<NotSupportedException> (() =>
			AppleTvControlLibrary.Opack.Opack.Unpack ([0x00], out _));
		}

	// tests/support/test_opack.py: test_unpack_boolean
	[Test]
	public void UnpackBoolean ()
		{
		Assert.That ((bool?)AppleTvControlLibrary.Opack.Opack.Unpack ([0x01], out var consumed), Is.True);
		Assert.That (consumed, Is.EqualTo (1));
		Assert.That ((bool?)AppleTvControlLibrary.Opack.Opack.Unpack ([0x02], out consumed), Is.False);
		Assert.That (consumed, Is.EqualTo (1));
		}

	// tests/support/test_opack.py: test_unpack_none
	[Test]
	public void UnpackNone ()
		{
		Assert.That (AppleTvControlLibrary.Opack.Opack.Unpack ([0x04], out var consumed), Is.Null);
		Assert.That (consumed, Is.EqualTo (1));
		}

	// tests/support/test_opack.py: test_unpack_uid
	[Test]
	[TestCase (new byte[] { 0xdf, 0x30, 0x01, 0x30, 0x02, 0xc1, 0x01, 0x03 })]
	[TestCase (new byte[] { 0xdf, 0x30, 0x01, 0x30, 0x02, 0xc2, 0x01, 0x00, 0x03 })]
	[TestCase (new byte[] { 0xdf, 0x30, 0x01, 0x30, 0x02, 0xc3, 0x01, 0x00, 0x00, 0x03 })]
	[TestCase (new byte[] { 0xdf, 0x30, 0x01, 0x30, 0x02, 0xc4, 0x01, 0x00, 0x00, 0x00, 0x03 })]
	public void UnpackUid (byte[] data)
		{
		var value = AppleTvControlLibrary.Opack.Opack.Unpack (data, out var consumed) as List<object?>;
		Assert.That (value, Is.Not.Null);
		Assert.That (value, Has.Count.EqualTo (3));
		Assert.That (((SizedInteger)value[0]!).Value, Is.EqualTo (new SizedInteger (1, 1).Value));
		Assert.That (((SizedInteger)value[1]!).Value, Is.EqualTo (new SizedInteger (2, 1).Value));
		Assert.That (((SizedInteger)value[2]!).Value, Is.EqualTo (new SizedInteger (2, 1).Value));
		Assert.That (consumed, Is.EqualTo (data.Length));
		}

	// tests/support/test_opack.py: test_golden (round-trip; the pack/unpack pair is exercised
	// rather than DeepDiff, since the test's intent is structural equivalence.)
	[Test]
	public void GoldenRoundTrip ()
		{
		var siriDeviceCapabilities = new Dictionary<object, object?>
			{
			["seymourEnabled"] = 1L,
			["voiceTriggerEnabled"] = 2L,
			};

		var siriInfo = new Dictionary<object, object?>
			{
			["collectorElectionVersion"] = 1.0,
			["deviceCapabilities"] = siriDeviceCapabilities,
			["sharedDataProtoBuf"] = Repeat ((byte)0x08, 512),
			};

		var content = new Dictionary<object, object?>
			{
			["_pubID"] = "AA:BB:CC:DD:EE:FF",
			["_sv"] = "230.1",
			["_bf"] = 0L,
			["_siriInfo"] = siriInfo,
			["_stA"] = new List<object?>
			{
				"com.apple.LiveAudio",
				"com.apple.siri.wakeup",
				"com.apple.Seymour",
				"com.apple.announce",
				"com.apple.coreduet.sync",
				"com.apple.SeymourSession",
			},
			["_i"] = "6c62fca18b11",
			["_clFl"] = 128L,
			["_idsID"] = "44E14ABC-DDDD-4188-B661-11BAAAF6ECDE",
			["_hkUID"] = new List<object?> { new Guid ("17ed160a-81f8-4488-962c-6b1a83eb0081") },
			["_dC"] = "1",
			["_sf"] = 256L,
			["model"] = "iPhone10,6",
			["name"] = "iPhone",
			};

		var data = new Dictionary<object, object?>
			{
			["_i"] = "_systemInfo",
			["_x"] = 1254122577L,
			["_btHP"] = false,
			["_c"] = content,
			["_t"] = 2L,
			};

		var packed = AppleTvControlLibrary.Opack.Opack.Pack (data);
		var unpacked = AppleTvControlLibrary.Opack.Opack.Unpack (packed, out var consumed) as Dictionary<object, object?>;

		Assert.That (consumed, Is.EqualTo (packed.Length));
		Assert.That (unpacked, Is.Not.Null);
		Assert.That (unpacked["_i"], Is.EqualTo ("_systemInfo"));
		Assert.That ((bool?)unpacked["_btHP"], Is.False);
		Assert.That (unpacked["_t"], Is.EqualTo (2L));

		var unpackedContent = (Dictionary<object, object?>)unpacked["_c"]!;
		Assert.That (unpackedContent["_pubID"], Is.EqualTo ("AA:BB:CC:DD:EE:FF"));
		Assert.That (unpackedContent["name"], Is.EqualTo ("iPhone"));

		var unpackedStA = (List<object?>)unpackedContent["_stA"]!;
		Assert.That (unpackedStA, Has.Count.EqualTo (6));
		Assert.That (unpackedStA[0], Is.EqualTo ("com.apple.LiveAudio"));
		}

	private static byte[] Concat (byte[] first, byte[] second)
		{
		var result = new byte[first.Length + second.Length];
		Buffer.BlockCopy (first, 0, result, 0, first.Length);
		Buffer.BlockCopy (second, 0, result, first.Length, second.Length);
		return result;
		}

	private static byte[] Repeat (byte value, int count)
		{
		var result = new byte[count];
		for (var i = 0; i < count; i++)
			{
			result[i] = value;
			}

		return result;
		}
	}