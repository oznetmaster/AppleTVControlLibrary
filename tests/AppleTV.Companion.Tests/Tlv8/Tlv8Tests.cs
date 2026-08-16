// Copyright (c) 2026 Neil Colvin. Licensed under the MIT License.
// See LICENSE file in the repository root for full license text.

using System;
using System.Collections.Generic;
using System.Linq;

using AppleTvControlLibrary.Tlv8;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AppleTV.Companion.Tests.Tlv8Tests;

/// <summary>
/// Ported from pyatv/tests/auth/test_hap_tlv8.py (pyatv 0.18.0).
/// </summary>
[TestClass]
public class Tlv8Tests
	{
	// tests/auth/test_hap_tlv8.py:15-16 (SINGLE_KEY_IN / SINGLE_KEY_OUT)
	private static readonly Dictionary<int, byte[]> SingleKeyIn = new ()
		{
		[10] = [0x31, 0x32, 0x33],
		};

	private static readonly byte[] SingleKeyOut = [0x0a, 0x03, 0x31, 0x32, 0x33];

	// tests/auth/test_hap_tlv8.py:20-21 (DOUBLE_KEY_IN / DOUBLE_KEY_OUT)
	// Use a list of KeyValuePair (ordered) as an OrderedDict equivalent, since a
	// regular Dictionary might enumerate keys in a different order every run.
	private static readonly List<KeyValuePair<int, byte[]>> DoubleKeyIn = new ()
		{
		new KeyValuePair<int, byte[]> (1, [0x31, 0x31, 0x31]),
		new KeyValuePair<int, byte[]> (4, [0x32, 0x32, 0x32]),
		};

	private static readonly byte[] DoubleKeyOut =
		[
		0x01, 0x03, 0x31, 0x31, 0x31,
		0x04, 0x03, 0x32, 0x32, 0x32,
		];

	// tests/auth/test_hap_tlv8.py:23-24 (LARGE_KEY_IN / LARGE_KEY_OUT)
	private static readonly Dictionary<int, byte[]> LargeKeyIn = new ()
		{
		[2] = Repeat (0x31, 256),
		};

	private static readonly byte[] _largeKeyOut = Concat (
		Concat ([0x02, 0xff], Repeat (0x31, 255)),
		[0x02, 0x01, 0x31]);

	// tests/auth/test_hap_tlv8.py:27-28 (test_write_single_key)
	[TestMethod]
	public void WriteSingleKey ()
		{
		CollectionAssert.AreEqual (SingleKeyOut, AppleTvControlLibrary.Tlv8.Tlv8.WriteTlv (SingleKeyIn));
		}

	// tests/auth/test_hap_tlv8.py:31-32 (test_write_two_keys)
	[TestMethod]
	public void WriteTwoKeys ()
		{
		CollectionAssert.AreEqual (DoubleKeyOut, AppleTvControlLibrary.Tlv8.Tlv8.WriteTlv (DoubleKeyIn));
		}

	// tests/auth/test_hap_tlv8.py:35-38 (test_write_key_larger_than_255_bytes)
	[TestMethod]
	public void WriteKeyLargerThan255Bytes ()
		{
		// This will actually result in two serialized TLVs, one being 255 bytes
		// and the next one will contain the remaining one byte
		CollectionAssert.AreEqual (_largeKeyOut, AppleTvControlLibrary.Tlv8.Tlv8.WriteTlv (LargeKeyIn));
		}

	// tests/auth/test_hap_tlv8.py:41-42 (test_read_single_key)
	[TestMethod]
	public void ReadSingleKey ()
		{
		AssertDictionariesEqual (SingleKeyIn, AppleTvControlLibrary.Tlv8.Tlv8.ReadTlv (SingleKeyOut));
		}

	// tests/auth/test_hap_tlv8.py:45-46 (test_read_two_keys)
	[TestMethod]
	public void ReadTwoKeys ()
		{
		var expected = DoubleKeyIn.ToDictionary (kvp => kvp.Key, kvp => kvp.Value);
		AssertDictionariesEqual (expected, AppleTvControlLibrary.Tlv8.Tlv8.ReadTlv (DoubleKeyOut));
		}

	// tests/auth/test_hap_tlv8.py:49-50 (test_read_key_larger_than_255_bytes)
	[TestMethod]
	public void ReadKeyLargerThan255Bytes ()
		{
		AssertDictionariesEqual (LargeKeyIn, AppleTvControlLibrary.Tlv8.Tlv8.ReadTlv (_largeKeyOut));
		}

	// tests/auth/test_hap_tlv8.py:53-55 (test_stringify_method)
	[TestMethod]
	public void StringifyMethod ()
		{
		Assert.AreEqual ("Method=PairSetup", AppleTvControlLibrary.Tlv8.Tlv8.Stringify (Entry (TlvValue.Method, 0x00)));
		Assert.AreEqual ("Method=PairVerify", AppleTvControlLibrary.Tlv8.Tlv8.Stringify (Entry (TlvValue.Method, 0x02)));
		}

	// tests/auth/test_hap_tlv8.py:58-64 (test_stringify_seqno)
	[TestMethod]
	public void StringifySeqNo ()
		{
		Assert.AreEqual ("SeqNo=M1", AppleTvControlLibrary.Tlv8.Tlv8.Stringify (Entry (TlvValue.SeqNo, 0x01)));
		Assert.AreEqual ("SeqNo=M2", AppleTvControlLibrary.Tlv8.Tlv8.Stringify (Entry (TlvValue.SeqNo, 0x02)));
		Assert.AreEqual ("SeqNo=M3", AppleTvControlLibrary.Tlv8.Tlv8.Stringify (Entry (TlvValue.SeqNo, 0x03)));
		Assert.AreEqual ("SeqNo=M4", AppleTvControlLibrary.Tlv8.Tlv8.Stringify (Entry (TlvValue.SeqNo, 0x04)));
		Assert.AreEqual ("SeqNo=M5", AppleTvControlLibrary.Tlv8.Tlv8.Stringify (Entry (TlvValue.SeqNo, 0x05)));
		Assert.AreEqual ("SeqNo=M6", AppleTvControlLibrary.Tlv8.Tlv8.Stringify (Entry (TlvValue.SeqNo, 0x06)));
		}

	// tests/auth/test_hap_tlv8.py:67-69 (test_stringify_error)
	[TestMethod]
	public void StringifyError ()
		{
		Assert.AreEqual ("Error=Authentication", AppleTvControlLibrary.Tlv8.Tlv8.Stringify (Entry (TlvValue.Error, 0x02)));
		Assert.AreEqual ("Error=MaxTries", AppleTvControlLibrary.Tlv8.Tlv8.Stringify (Entry (TlvValue.Error, 0x05)));
		}

	// tests/auth/test_hap_tlv8.py:72-73 (test_stringify_backoff)
	[TestMethod]
	public void StringifyBackoff ()
		{
		var data = new Dictionary<int, byte[]> { [(int)TlvValue.BackOff] = [0x02, 0x00] };
		Assert.AreEqual ("BackOff=2s", AppleTvControlLibrary.Tlv8.Tlv8.Stringify (data));
		}

	// tests/auth/test_hap_tlv8.py:76-91 (test_stringify_remainging_short)
	[TestMethod]
	public void StringifyRemainingShort ()
		{
		var values = new[]
			{
			TlvValue.Identifier,
			TlvValue.Salt,
			TlvValue.PublicKey,
			TlvValue.Proof,
			TlvValue.EncryptedData,
			TlvValue.Certificate,
			TlvValue.Signature,
			TlvValue.Permissions,
			TlvValue.FragmentData,
			TlvValue.FragmentLast,
			};

		foreach (var value in values)
			{
			var data = new Dictionary<int, byte[]> { [(int)value] = [0x00, 0x01, 0x02, 0x03] };
			Assert.AreEqual ($"{value}=4bytes", AppleTvControlLibrary.Tlv8.Tlv8.Stringify (data));
			}
		}

	// tests/auth/test_hap_tlv8.py:94-105 (test_stringify_multiple)
	[TestMethod]
	public void StringifyMultiple ()
		{
		var data = new Dictionary<int, byte[]>
			{
			[(int)TlvValue.Method] = [0x00],
			[(int)TlvValue.SeqNo] = [0x01],
			[(int)TlvValue.Error] = [0x03],
			[(int)TlvValue.BackOff] = [0x01, 0x00],
			};

		Assert.AreEqual ("Method=PairSetup, SeqNo=M1, Error=BackOff, BackOff=1s", AppleTvControlLibrary.Tlv8.Tlv8.Stringify (data));
		}

	// tests/auth/test_hap_tlv8.py:108-119 (test_stringify_unknown_values)
	[TestMethod]
	public void StringifyUnknownValues ()
		{
		var data = new Dictionary<int, byte[]>
			{
			[(int)TlvValue.Method] = [0xaa],
			[(int)TlvValue.SeqNo] = [0xab],
			[(int)TlvValue.Error] = [0xac],
			[0xad] = [0x01, 0x02, 0x03],
			};

		Assert.AreEqual ("Method=0xaa, SeqNo=0xab, Error=0xac, 0xad=3bytes", AppleTvControlLibrary.Tlv8.Tlv8.Stringify (data));
		}

	// Additional trap coverage beyond the ported vectors: a value chunked across
	// more than two 255-byte segments must still concatenate correctly on read,
	// and round-trip through write_tlv/read_tlv. (pyatv/auth/hap_tlv8.py — line 80-81 as of pyatv 0.18.0, 114-122)
	[TestMethod]
	public void RoundTripValueLargerThan255Bytes ()
		{
		var value = Repeat (0x42, 600);
		var input = new Dictionary<int, byte[]> { [5] = value };

		var written = AppleTvControlLibrary.Tlv8.Tlv8.WriteTlv (input);
		var read = AppleTvControlLibrary.Tlv8.Tlv8.ReadTlv (written);

		Assert.IsTrue (read.ContainsKey (5));
		CollectionAssert.AreEqual (value, read[5]);
		}

	private static Dictionary<int, byte[]> Entry (TlvValue key, byte value)
		{
		return new Dictionary<int, byte[]> { [(int)key] = [value] };
		}

	private static void AssertDictionariesEqual (Dictionary<int, byte[]> expected, Dictionary<int, byte[]> actual)
		{
		Assert.HasCount (expected.Count, actual);
		foreach (var kvp in expected)
			{
			Assert.IsTrue (actual.ContainsKey (kvp.Key));
			CollectionAssert.AreEqual (kvp.Value, actual[kvp.Key]);
			}
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