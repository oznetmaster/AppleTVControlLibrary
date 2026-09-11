// Copyright (c) 2026 Neil Colvin. Licensed under the MIT License.
// See LICENSE file in the repository root for full license text.

using System;
using System.Collections.Generic;
using System.Linq;

using AppleTvControlLibrary.Tlv8;

using NUnit.Framework;

namespace AppleTv.Hap.Tests.Tlv8Tests;

/// <summary>
/// Ported from pyatv/tests/auth/test_hap_tlv8.py (pyatv 0.18.0).
/// </summary>
[TestFixture]
[FixtureLifeCycle (LifeCycle.InstancePerTestCase)]
public class Tlv8Tests
	{
	// tests/auth/test_hap_tlv8.py:15-16 (SINGLE_KEY_IN / SINGLE_KEY_OUT)
	private static readonly Dictionary<int, byte[]> SingleKeyIn = new ()
		{
		[10] = [0x31, 0x32, 0x33],
		};

	private static readonly byte[] _singleKeyOut = [0x0a, 0x03, 0x31, 0x32, 0x33];

	// tests/auth/test_hap_tlv8.py:20-21 (DOUBLE_KEY_IN / DOUBLE_KEY_OUT)
	// Use a list of KeyValuePair (ordered) as an OrderedDict equivalent, since a
	// regular Dictionary might enumerate keys in a different order every run.
	private static readonly List<KeyValuePair<int, byte[]>> DoubleKeyIn =
		[
		new KeyValuePair<int, byte[]> (1, [0x31, 0x31, 0x31]),
		new KeyValuePair<int, byte[]> (4, [0x32, 0x32, 0x32]),
		];

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

	private static readonly byte[] LargeKeyOut = Concat (
		Concat ([0x02, 0xff], Repeat (0x31, 255)),
		[0x02, 0x01, 0x31]);

	// tests/auth/test_hap_tlv8.py:27-28 (test_write_single_key)
	[Test]
	public void WriteSingleKey () => Assert.That (AppleTvControlLibrary.Tlv8.Tlv8.WriteTlv (SingleKeyIn), Is.EqualTo (_singleKeyOut));

	// tests/auth/test_hap_tlv8.py:31-32 (test_write_two_keys)
	[Test]
	public void WriteTwoKeys () => Assert.That (AppleTvControlLibrary.Tlv8.Tlv8.WriteTlv (DoubleKeyIn), Is.EqualTo (DoubleKeyOut));

	// tests/auth/test_hap_tlv8.py:35-38 (test_write_key_larger_than_255_bytes)
	[Test]
	public void WriteKeyLargerThan255Bytes () =>
		// This will actually result in two serialized TLVs, one being 255 bytes
		// and the next one will contain the remaining one byte
		Assert.That (AppleTvControlLibrary.Tlv8.Tlv8.WriteTlv (LargeKeyIn), Is.EqualTo (LargeKeyOut));

	// tests/auth/test_hap_tlv8.py:41-42 (test_read_single_key)
	[Test]
	public void ReadSingleKey () => AssertDictionariesEqual (SingleKeyIn, AppleTvControlLibrary.Tlv8.Tlv8.ReadTlv (_singleKeyOut));

	// tests/auth/test_hap_tlv8.py:45-46 (test_read_two_keys)
	[Test]
	public void ReadTwoKeys ()
		{
		var expected = DoubleKeyIn.ToDictionary (kvp => kvp.Key, kvp => kvp.Value);
		AssertDictionariesEqual (expected, AppleTvControlLibrary.Tlv8.Tlv8.ReadTlv (DoubleKeyOut));
		}

	// tests/auth/test_hap_tlv8.py:49-50 (test_read_key_larger_than_255_bytes)
	[Test]
	public void ReadKeyLargerThan255Bytes () => AssertDictionariesEqual (LargeKeyIn, AppleTvControlLibrary.Tlv8.Tlv8.ReadTlv (LargeKeyOut));

	// tests/auth/test_hap_tlv8.py:53-55 (test_stringify_method)
	[Test]
	public void StringifyMethod ()
		{
		Assert.That (AppleTvControlLibrary.Tlv8.Tlv8.Stringify (Entry (TlvValue.Method, 0x00)), Is.EqualTo ("Method=PairSetup"));
		Assert.That (AppleTvControlLibrary.Tlv8.Tlv8.Stringify (Entry (TlvValue.Method, 0x02)), Is.EqualTo ("Method=PairVerify"));
		}

	// tests/auth/test_hap_tlv8.py:58-64 (test_stringify_seqno)
	[Test]
	public void StringifySeqNo ()
		{
		Assert.That (AppleTvControlLibrary.Tlv8.Tlv8.Stringify (Entry (TlvValue.SeqNo, 0x01)), Is.EqualTo ("SeqNo=M1"));
		Assert.That (AppleTvControlLibrary.Tlv8.Tlv8.Stringify (Entry (TlvValue.SeqNo, 0x02)), Is.EqualTo ("SeqNo=M2"));
		Assert.That (AppleTvControlLibrary.Tlv8.Tlv8.Stringify (Entry (TlvValue.SeqNo, 0x03)), Is.EqualTo ("SeqNo=M3"));
		Assert.That (AppleTvControlLibrary.Tlv8.Tlv8.Stringify (Entry (TlvValue.SeqNo, 0x04)), Is.EqualTo ("SeqNo=M4"));
		Assert.That (AppleTvControlLibrary.Tlv8.Tlv8.Stringify (Entry (TlvValue.SeqNo, 0x05)), Is.EqualTo ("SeqNo=M5"));
		Assert.That (AppleTvControlLibrary.Tlv8.Tlv8.Stringify (Entry (TlvValue.SeqNo, 0x06)), Is.EqualTo ("SeqNo=M6"));
		}

	// tests/auth/test_hap_tlv8.py:67-69 (test_stringify_error)
	[Test]
	public void StringifyError ()
		{
		Assert.That (AppleTvControlLibrary.Tlv8.Tlv8.Stringify (Entry (TlvValue.Error, 0x02)), Is.EqualTo ("Error=Authentication"));
		Assert.That (AppleTvControlLibrary.Tlv8.Tlv8.Stringify (Entry (TlvValue.Error, 0x05)), Is.EqualTo ("Error=MaxTries"));
		}

	// tests/auth/test_hap_tlv8.py:72-73 (test_stringify_backoff)
	[Test]
	public void StringifyBackoff ()
		{
		var data = new Dictionary<int, byte[]> { [(int)TlvValue.BackOff] = [0x02, 0x00] };
		Assert.That (AppleTvControlLibrary.Tlv8.Tlv8.Stringify (data), Is.EqualTo ("BackOff=2s"));
		}

	// tests/auth/test_hap_tlv8.py:76-91 (test_stringify_remainging_short)
	[Test]
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
			Assert.That (AppleTvControlLibrary.Tlv8.Tlv8.Stringify (data), Is.EqualTo ($"{value}=4bytes"));
			}
		}

	// tests/auth/test_hap_tlv8.py:94-105 (test_stringify_multiple)
	[Test]
	public void StringifyMultiple ()
		{
		var data = new Dictionary<int, byte[]>
			{
			[(int)TlvValue.Method] = [0x00],
			[(int)TlvValue.SeqNo] = [0x01],
			[(int)TlvValue.Error] = [0x03],
			[(int)TlvValue.BackOff] = [0x01, 0x00],
			};

		Assert.That (AppleTvControlLibrary.Tlv8.Tlv8.Stringify (data), Is.EqualTo ("Method=PairSetup, SeqNo=M1, Error=BackOff, BackOff=1s"));
		}

	// tests/auth/test_hap_tlv8.py:108-119 (test_stringify_unknown_values)
	[Test]
	public void StringifyUnknownValues ()
		{
		var data = new Dictionary<int, byte[]>
			{
			[(int)TlvValue.Method] = [0xaa],
			[(int)TlvValue.SeqNo] = [0xab],
			[(int)TlvValue.Error] = [0xac],
			[0xad] = [0x01, 0x02, 0x03],
			};

		Assert.That (AppleTvControlLibrary.Tlv8.Tlv8.Stringify (data), Is.EqualTo ("Method=0xaa, SeqNo=0xab, Error=0xac, 0xad=3bytes"));
		}

	// Additional trap coverage beyond the ported vectors: a value chunked across
	// more than two 255-byte segments must still concatenate correctly on read,
	// and round-trip through write_tlv/read_tlv. (pyatv/auth/hap_tlv8.py — line 80-81 as of pyatv 0.18.0, 114-122)
	[Test]
	public void RoundTripValueLargerThan255Bytes ()
		{
		var value = Repeat (0x42, 600);
		var input = new Dictionary<int, byte[]> { [5] = value };

		byte[] written = AppleTvControlLibrary.Tlv8.Tlv8.WriteTlv (input);
		var read = AppleTvControlLibrary.Tlv8.Tlv8.ReadTlv (written);

		Assert.That (read.ContainsKey (5), Is.True);
		Assert.That (read[5], Is.EqualTo (value));
		}

	private static Dictionary<int, byte[]> Entry (TlvValue key, byte value) => new () { [(int)key] = [value] };

	private static void AssertDictionariesEqual (Dictionary<int, byte[]> expected, Dictionary<int, byte[]> actual)
		{
		Assert.That (actual.Count, Is.EqualTo (expected.Count));
		foreach (var kvp in expected)
			{
			Assert.That (actual.ContainsKey (kvp.Key), Is.True);
			Assert.That (actual[kvp.Key], Is.EqualTo (kvp.Value));
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
		for (int i = 0; i < count; i++)
			{
			result[i] = value;
			}

		return result;
		}
	}