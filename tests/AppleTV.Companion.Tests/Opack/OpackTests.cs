using System;
using System.Collections.Generic;

using AppleTvControlLibrary.Opack;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AppleTV.Companion.Tests.Opack;

/// <summary>
/// Ported from pyatv/tests/support/test_opack.py (pyatv 0.18.0).
/// </summary>
[TestClass]
public class OpackTests
	{
	// tests/support/test_opack.py: test_pack_unsupported_type
	[TestMethod]
	public void PackUnsupportedTypeThrows ()
		{
		Assert.ThrowsException<NotSupportedException> (() => AppleTvControlLibrary.Opack.Opack.Pack (new object ()));
		}

	// tests/support/test_opack.py: test_pack_boolean
	[TestMethod]
	public void PackBoolean ()
		{
		CollectionAssert.AreEqual (new byte[] { 0x01 }, AppleTvControlLibrary.Opack.Opack.Pack (true));
		CollectionAssert.AreEqual (new byte[] { 0x02 }, AppleTvControlLibrary.Opack.Opack.Pack (false));
		}

	// tests/support/test_opack.py: test_pack_none
	[TestMethod]
	public void PackNone ()
		{
		CollectionAssert.AreEqual (new byte[] { 0x04 }, AppleTvControlLibrary.Opack.Opack.Pack (null));
		}

	// tests/support/test_opack.py: test_pack_uuid
	[TestMethod]
	public void PackUuid ()
		{
		var guid = new Guid ("12345678-1234-5678-1234-567812345678");
		byte[] expected = Concat (new byte[] { 0x05 }, guid.ToByteArray ());
		CollectionAssert.AreEqual (expected, AppleTvControlLibrary.Opack.Opack.Pack (guid));
		}

	// tests/support/test_opack.py: test_pack_absolute_time
	[TestMethod]
	public void PackAbsoluteTimeThrows ()
		{
		Assert.ThrowsException<NotImplementedException> (() => AppleTvControlLibrary.Opack.Opack.Pack (DateTime.Now));
		}

	// tests/support/test_opack.py: test_pack_small_integers
	[DataTestMethod]
	[DataRow (0L, new byte[] { 0x08 })]
	[DataRow (0xFL, new byte[] { 0x17 })]
	[DataRow (0x27L, new byte[] { 0x2f })]
	public void PackSmallIntegers (long value, byte[] expected)
		{
		CollectionAssert.AreEqual (expected, AppleTvControlLibrary.Opack.Opack.Pack (value));
		}

	// tests/support/test_opack.py: test_pack_larger_integers
	[DataTestMethod]
	[DataRow (0x28L, new byte[] { 0x30, 0x28 })]
	[DataRow (0x1FFL, new byte[] { 0x31, 0xff, 0x01 })]
	[DataRow (0x1FFFFFFL, new byte[] { 0x32, 0xff, 0xff, 0xff, 0x01 })]
	[DataRow (0x1FFFFFFFFFFFFFFL, new byte[] { 0x33, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0x01 })]
	public void PackLargerIntegers (long value, byte[] expected)
		{
		CollectionAssert.AreEqual (expected, AppleTvControlLibrary.Opack.Opack.Pack (value));
		}

	// tests/support/test_opack.py: test_pack_sized_integers
	[DataTestMethod]
	[DataRow (1, new byte[] { 0x30, 0x01 })]
	[DataRow (2, new byte[] { 0x31, 0x01, 0x00 })]
	[DataRow (4, new byte[] { 0x32, 0x01, 0x00, 0x00, 0x00 })]
	[DataRow (8, new byte[] { 0x33, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 })]
	public void PackSizedIntegers (int size, byte[] expected)
		{
		var value = new SizedInteger (0x1, size);
		CollectionAssert.AreEqual (expected, AppleTvControlLibrary.Opack.Opack.Pack (value));
		}

	// tests/support/test_opack.py: test_pack_float64
	[TestMethod]
	public void PackFloat64 ()
		{
		byte[] expected = { 0x36, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xf0, 0x3f };
		CollectionAssert.AreEqual (expected, AppleTvControlLibrary.Opack.Opack.Pack (1.0));
		}

	// tests/support/test_opack.py: test_pack_short_strings
	[TestMethod]
	public void PackShortStrings ()
		{
		CollectionAssert.AreEqual (new byte[] { 0x41, 0x61 }, AppleTvControlLibrary.Opack.Opack.Pack ("a"));
		CollectionAssert.AreEqual (new byte[] { 0x43, 0x61, 0x62, 0x63 }, AppleTvControlLibrary.Opack.Opack.Pack ("abc"));

		byte[] expected = Concat (new byte[] { 0x60 }, Repeat ((byte)0x61, 0x20));
		CollectionAssert.AreEqual (expected, AppleTvControlLibrary.Opack.Opack.Pack (new string ('a', 0x20)));
		}

	// tests/support/test_opack.py: test_pack_longer_strings
	[TestMethod]
	public void PackLongerStrings ()
		{
		byte[] expected33 = Concat (new byte[] { 0x61, 0x21 }, Repeat ((byte)0x61, 33));
		CollectionAssert.AreEqual (expected33, AppleTvControlLibrary.Opack.Opack.Pack (new string ('a', 33)));

		byte[] expected256 = Concat (new byte[] { 0x62, 0x00, 0x01 }, Repeat ((byte)0x61, 256));
		CollectionAssert.AreEqual (expected256, AppleTvControlLibrary.Opack.Opack.Pack (new string ('a', 256)));
		}

	// tests/support/test_opack.py: test_pack_short_raw_bytes
	[TestMethod]
	public void PackShortRawBytes ()
		{
		CollectionAssert.AreEqual (new byte[] { 0x71, 0xac }, AppleTvControlLibrary.Opack.Opack.Pack (new byte[] { 0xac }));
		CollectionAssert.AreEqual (new byte[] { 0x73, 0x12, 0x34, 0x56 }, AppleTvControlLibrary.Opack.Opack.Pack (new byte[] { 0x12, 0x34, 0x56 }));

		byte[] expected = Concat (new byte[] { 0x90 }, Repeat ((byte)0xad, 0x20));
		CollectionAssert.AreEqual (expected, AppleTvControlLibrary.Opack.Opack.Pack (Repeat ((byte)0xad, 0x20)));
		}

	// tests/support/test_opack.py: test_pack_longer_raw_bytes
	[TestMethod]
	public void PackLongerRawBytes ()
		{
		byte[] expected33 = Concat (new byte[] { 0x91, 0x21 }, Repeat ((byte)0x61, 33));
		CollectionAssert.AreEqual (expected33, AppleTvControlLibrary.Opack.Opack.Pack (Repeat ((byte)0x61, 33)));

		byte[] expected256 = Concat (new byte[] { 0x92, 0x00, 0x01 }, Repeat ((byte)0x61, 256));
		CollectionAssert.AreEqual (expected256, AppleTvControlLibrary.Opack.Opack.Pack (Repeat ((byte)0x61, 256)));

		byte[] expected65536 = Concat (new byte[] { 0x93, 0x00, 0x00, 0x01, 0x00 }, Repeat ((byte)0x61, 65536));
		CollectionAssert.AreEqual (expected65536, AppleTvControlLibrary.Opack.Opack.Pack (Repeat ((byte)0x61, 65536)));
		}

	// tests/support/test_opack.py: test_pack_array
	[TestMethod]
	public void PackArray ()
		{
		CollectionAssert.AreEqual (new byte[] { 0xd0 }, AppleTvControlLibrary.Opack.Opack.Pack (new List<object?> ()));

		var list = new List<object?> { 1L, "test", false };
		byte[] expected = { 0xd3, 0x09, 0x44, 0x74, 0x65, 0x73, 0x74, 0x02 };
		CollectionAssert.AreEqual (expected, AppleTvControlLibrary.Opack.Opack.Pack (list));

		var nested = new List<object?> { new List<object?> { true } };
		CollectionAssert.AreEqual (new byte[] { 0xd1, 0xd1, 0x01 }, AppleTvControlLibrary.Opack.Opack.Pack (nested));
		}

	// tests/support/test_opack.py: test_pack_endless_array
	[TestMethod]
	public void PackEndlessArray ()
		{
		var list = new List<object?> ();
		for (int i = 0; i < 15; i++)
			{
			list.Add ("a");
			}

		byte[] expected = Concat (new byte[] { 0xdf, 0x41, 0x61 }, Concat (Repeat ((byte)0xa0, 14), new byte[] { 0x03 }));
		CollectionAssert.AreEqual (expected, AppleTvControlLibrary.Opack.Opack.Pack (list));
		}

	// tests/support/test_opack.py: test_pack_dict
	[TestMethod]
	public void PackDict ()
		{
		CollectionAssert.AreEqual (new byte[] { 0xe0 }, AppleTvControlLibrary.Opack.Opack.Pack (new Dictionary<object, object?> ()));

		var dict = new Dictionary<object, object?> { ["a"] = 12L, [false] = null };
		CollectionAssert.AreEqual (new byte[] { 0xe2, 0x41, 0x61, 0x14, 0x02, 0x04 }, AppleTvControlLibrary.Opack.Opack.Pack (dict));

		var nested = new Dictionary<object, object?> { [true] = new Dictionary<object, object?> { ["a"] = 2L } };
		CollectionAssert.AreEqual (new byte[] { 0xe1, 0x01, 0xe1, 0x41, 0x61, 0x0a }, AppleTvControlLibrary.Opack.Opack.Pack (nested));
		}

	// tests/support/test_opack.py: test_pack_ptr
	[TestMethod]
	public void PackPtr ()
		{
		CollectionAssert.AreEqual (
			new byte[] { 0xd2, 0x41, 0x61, 0xa0 },
			AppleTvControlLibrary.Opack.Opack.Pack (new List<object?> { "a", "a" }));

		CollectionAssert.AreEqual (
			new byte[] { 0xd4, 0x43, 0x66, 0x6f, 0x6f, 0x43, 0x62, 0x61, 0x72, 0xa0, 0xa1 },
			AppleTvControlLibrary.Opack.Opack.Pack (new List<object?> { "foo", "bar", "foo", "bar" }));

		var dict = new Dictionary<object, object?>
			{
			["a"] = "b",
			["c"] = new Dictionary<object, object?> { ["d"] = "a" },
			["d"] = true,
			};
		CollectionAssert.AreEqual (
			new byte[] { 0xe3, 0x41, 0x61, 0x41, 0x62, 0x41, 0x63, 0xe1, 0x41, 0x64, 0xa0, 0xa3, 0x01 },
			AppleTvControlLibrary.Opack.Opack.Pack (dict));
		}

	// tests/support/test_opack.py: test_unpack_unsupported_type
	[TestMethod]
	public void UnpackUnsupportedTypeThrows ()
		{
		Assert.ThrowsException<NotSupportedException> (() =>
			AppleTvControlLibrary.Opack.Opack.Unpack (new byte[] { 0x00 }, out _));
		}

	// tests/support/test_opack.py: test_unpack_boolean
	[TestMethod]
	public void UnpackBoolean ()
		{
		Assert.AreEqual (true, AppleTvControlLibrary.Opack.Opack.Unpack (new byte[] { 0x01 }, out int consumed));
		Assert.AreEqual (1, consumed);
		Assert.AreEqual (false, AppleTvControlLibrary.Opack.Opack.Unpack (new byte[] { 0x02 }, out consumed));
		Assert.AreEqual (1, consumed);
		}

	// tests/support/test_opack.py: test_unpack_none
	[TestMethod]
	public void UnpackNone ()
		{
		Assert.IsNull (AppleTvControlLibrary.Opack.Opack.Unpack (new byte[] { 0x04 }, out int consumed));
		Assert.AreEqual (1, consumed);
		}

	// tests/support/test_opack.py: test_unpack_uid
	[DataTestMethod]
	[DataRow (new byte[] { 0xdf, 0x30, 0x01, 0x30, 0x02, 0xc1, 0x01, 0x03 })]
	[DataRow (new byte[] { 0xdf, 0x30, 0x01, 0x30, 0x02, 0xc2, 0x01, 0x00, 0x03 })]
	[DataRow (new byte[] { 0xdf, 0x30, 0x01, 0x30, 0x02, 0xc3, 0x01, 0x00, 0x00, 0x03 })]
	[DataRow (new byte[] { 0xdf, 0x30, 0x01, 0x30, 0x02, 0xc4, 0x01, 0x00, 0x00, 0x00, 0x03 })]
	public void UnpackUid (byte[] data)
		{
		var value = AppleTvControlLibrary.Opack.Opack.Unpack (data, out int consumed) as List<object?>;
		Assert.IsNotNull (value);
		Assert.AreEqual (3, value.Count);
		Assert.AreEqual (new SizedInteger (1, 1).Value, ((SizedInteger)value[0]!).Value);
		Assert.AreEqual (new SizedInteger (2, 1).Value, ((SizedInteger)value[1]!).Value);
		Assert.AreEqual (new SizedInteger (2, 1).Value, ((SizedInteger)value[2]!).Value);
		Assert.AreEqual (data.Length, consumed);
		}

	// tests/support/test_opack.py: test_golden (round-trip; the pack/unpack pair is exercised
	// rather than DeepDiff, since the test's intent is structural equivalence.)
	[TestMethod]
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

		byte[] packed = AppleTvControlLibrary.Opack.Opack.Pack (data);
		var unpacked = AppleTvControlLibrary.Opack.Opack.Unpack (packed, out int consumed) as Dictionary<object, object?>;

		Assert.AreEqual (packed.Length, consumed);
		Assert.IsNotNull (unpacked);
		Assert.AreEqual ("_systemInfo", unpacked["_i"]);
		Assert.AreEqual (false, unpacked["_btHP"]);
		Assert.AreEqual (2L, unpacked["_t"]);

		var unpackedContent = (Dictionary<object, object?>)unpacked["_c"]!;
		Assert.AreEqual ("AA:BB:CC:DD:EE:FF", unpackedContent["_pubID"]);
		Assert.AreEqual ("iPhone", unpackedContent["name"]);

		var unpackedStA = (List<object?>)unpackedContent["_stA"]!;
		Assert.AreEqual (6, unpackedStA.Count);
		Assert.AreEqual ("com.apple.LiveAudio", unpackedStA[0]);
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
		for (int i = 0; i < count; i++)
			{
			result[i] = value;
			}

		return result;
		}
	}