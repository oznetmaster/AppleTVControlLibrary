// Copyright (c) 2026 Neil Colvin. Licensed under the MIT License.
// See LICENSE file in the repository root for full license text.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace AppleTvControlLibrary.Opack;

/// <summary>
/// Support for the OPACK serialization format used by the Companion Link protocol.
/// </summary>
// pyatv/support/opack.py (ported in full)
// Notes (pyatv/support/opack.py — line 3-7 as of pyatv 0.18.0):
//  * Absolute time (0x06) is not implemented for pack (can unpack as integer only).
//  * Pack implementation does not implement UID referencing.
//  * Likely other cases missing.
public static class Opack
	{
	/// <summary>Packs a data structure using OPACK and returns the encoded bytes.</summary>
	/// <param name="data">The data to encode.</param>
	/// <returns>The OPACK-encoded bytes.</returns>
	// pyatv/support/opack.py (pack) — line 32-34 as of pyatv 0.18.0
	public static byte[] Pack (object? data)
		{
		var objectList = new List<byte[]> ();
		return PackInternal (data, objectList);
		}

	// pyatv/support/opack.py (_pack) — line 37-141 as of pyatv 0.18.0
	private static byte[] PackInternal (object? data, List<byte[]> objectList)
		{
		byte[]? packedBytes = null;

		switch (data)
			{
			case null:
				// pyatv/support/opack.py — line 41-42 as of pyatv 0.18.0
				packedBytes = [0x04];
				break;
			case bool b:
				// pyatv/support/opack.py — line 43-44 as of pyatv 0.18.0
				packedBytes = [(byte)(b ? 1 : 2)];
				break;
			case Guid guid:
				// pyatv/support/opack.py — line 45-46 as of pyatv 0.18.0
				packedBytes = [0x05, .. guid.ToByteArray ()];
				break;
			case DateTime:
				// pyatv/support/opack.py — line 47-48 as of pyatv 0.18.0
				throw new NotImplementedException ("absolute time");
			case SizedInteger:
			case object when IsIntegral (data):
				packedBytes = PackInteger (data);
				break;
			case double or float:
				{
				// pyatv/support/opack.py (pack always emits 0x36 / float64) — line 60-61 as of pyatv 0.18.0
				var d = Convert.ToDouble (data, System.Globalization.CultureInfo.InvariantCulture);
				Span<byte> buffer = stackalloc byte[9];
				buffer[0] = 0x36;
				System.Buffers.Binary.BinaryPrimitives.WriteInt64LittleEndian (buffer[1..], BitConverter.DoubleToInt64Bits (d));
				packedBytes = buffer.ToArray ();
				break;
				}
			case string s:
				packedBytes = PackString (s);
				break;
			case byte[] bytes:
				packedBytes = PackBytes (bytes);
				break;
			case IDictionary dict:
				packedBytes = PackDict (dict, objectList);
				break;
			case IList list:
				packedBytes = PackList (list, objectList);
				break;
			default:
				throw new NotSupportedException (data.GetType ().ToString ());
			}

		// pyatv/support/opack.py (object/pointer table, pack side) — line 126-140 as of pyatv 0.18.0
		var objectIndex = IndexOfBytes (objectList, packedBytes);
		if (objectIndex >= 0)
			{
			if (objectIndex < 0x21)
				{
				// pyatv/support/opack.py — line 130-131 as of pyatv 0.18.0
				packedBytes = [(byte)(0xA0 + objectIndex)];
				}
			else if (objectIndex <= 0xFF)
				{
				// pyatv/support/opack.py — line 132-133 as of pyatv 0.18.0
				packedBytes = [0xC1, .. IntToLittleEndian (objectIndex, 1)];
				}
			else if (objectIndex <= 0xFFFF)
				{
				// pyatv/support/opack.py — line 134-135 as of pyatv 0.18.0
				packedBytes = [0xC2, .. IntToLittleEndian (objectIndex, 2)];
				}
			else if ((uint)objectIndex <= 0xFFFFFFFF)
				{
				// pyatv/support/opack.py — line 136-137 as of pyatv 0.18.0
				packedBytes = [0xC3, .. IntToLittleEndian (objectIndex, 4)];
				}
			else
				{
				// pyatv/support/opack.py — line 138-139 as of pyatv 0.18.0
				packedBytes = [0xC4, .. IntToLittleEndian (objectIndex, 8)];
				}
			}
		else if (packedBytes.Length > 1)
			{
			// pyatv/support/opack.py — line 141 as of pyatv 0.18.0
			objectList.Add (packedBytes);
			}

		return packedBytes;
		}

	private static bool IsIntegral (object data) =>
		 data is sbyte or byte or short or ushort or
		 int or uint or long or ulong;

	// pyatv/support/opack.py — line 49-58 as of pyatv 0.18.0
	private static byte[] PackInteger (object data)
		{
		long value;
		int? sizeHint = null;
		if (data is SizedInteger sized)
			{
			value = sized.Value;
			sizeHint = sized.Size;
			}
		else
			{
			value = Convert.ToInt64 (data, System.Globalization.CultureInfo.InvariantCulture);
			}

		var uValue = unchecked((ulong)value);

		if (value < 0x28 && sizeHint is null)
			{
			// pyatv/support/opack.py — line 51-52 as of pyatv 0.18.0
			return [(byte)(value + 8)];
			}

		if ((uValue <= 0xFF && sizeHint is null) || sizeHint == 1)
			{
			// pyatv/support/opack.py — line 53-54 as of pyatv 0.18.0
			return [0x30, .. IntToLittleEndian (value, 1)];
			}

		if ((uValue <= 0xFFFF && sizeHint is null) || sizeHint == 2)
			{
			// pyatv/support/opack.py — line 55-56 as of pyatv 0.18.0
			return [0x31, .. IntToLittleEndian (value, 2)];
			}

		if ((uValue <= 0xFFFFFFFF && sizeHint is null) || sizeHint == 4)
			{
			// pyatv/support/opack.py — line 57-58 as of pyatv 0.18.0
			return [0x32, .. IntToLittleEndian (value, 4)];
			}

		// pyatv/support/opack.py — line 59 as of pyatv 0.18.0
		return [0x33, .. IntToLittleEndian (value, 8)];
		}

	// pyatv/support/opack.py (strings, little-endian length ladder: 1,2,3,4 bytes) — line 62-80 as of pyatv 0.18.0
	private static byte[] PackString (string s)
		{
		var encoded = Encoding.UTF8.GetBytes (s);
		if (encoded.Length <= 0x20)
			{
			// pyatv/support/opack.py — line 64-65 as of pyatv 0.18.0
			return [(byte)(0x40 + encoded.Length), .. encoded];
			}

		if (encoded.Length <= 0xFF)
			{
			// pyatv/support/opack.py — line 66-69 as of pyatv 0.18.0
			return [0x61, .. IntToLittleEndian (encoded.Length, 1), .. encoded];
			}

		if (encoded.Length <= 0xFFFF)
			{
			// pyatv/support/opack.py — line 70-73 as of pyatv 0.18.0
			return [0x62, .. IntToLittleEndian (encoded.Length, 2), .. encoded];
			}

		if (encoded.Length <= 0xFFFFFF)
			{
			// pyatv/support/opack.py — line 74-77 as of pyatv 0.18.0
			return [0x63, .. IntToLittleEndian (encoded.Length, 3), .. encoded];
			}

		// pyatv/support/opack.py — line 78-81 as of pyatv 0.18.0
		return [0x64, .. IntToLittleEndian (encoded.Length, 4), .. encoded];
		}

	// pyatv/support/opack.py (byte arrays, length ladder: 1,2,4,8 bytes) — line 82-100 as of pyatv 0.18.0
	private static byte[] PackBytes (byte[] data)
		{
		if (data.Length <= 0x20)
			{
			// pyatv/support/opack.py — line 84-85 as of pyatv 0.18.0
			return [(byte)(0x70 + data.Length), .. data];
			}

		if (data.Length <= 0xFF)
			{
			// pyatv/support/opack.py — line 86-89 as of pyatv 0.18.0
			return [0x91, .. IntToLittleEndian (data.Length, 1), .. data];
			}

		if (data.Length <= 0xFFFF)
			{
			// pyatv/support/opack.py — line 90-93 as of pyatv 0.18.0
			return [0x92, .. IntToLittleEndian (data.Length, 2), .. data];
			}

		if ((uint)data.Length <= 0xFFFFFFFF)
			{
			// pyatv/support/opack.py — line 94-97 as of pyatv 0.18.0
			return [0x93, .. IntToLittleEndian (data.Length, 4), .. data];
			}

		// pyatv/support/opack.py — line 98-101 as of pyatv 0.18.0
		return [0x94, .. IntToLittleEndian (data.Length, 8), .. data];
		}

	// pyatv/support/opack.py (list) — line 102-107 as of pyatv 0.18.0
	private static byte[] PackList (IList list, List<byte[]> objectList)
		{
		var result = new List<byte>
			{
				 (byte)(0xD0 + Math.Min(list.Count, 0xF)),
			};
		foreach (var item in list)
			{
			result.AddRange (PackInternal (item, objectList));
			}

		if (list.Count >= 0xF)
			{
			result.Add (0x03);
			}

		return [.. result];
		}

	// pyatv/support/opack.py (dict) — line 108-113 as of pyatv 0.18.0
	private static byte[] PackDict (IDictionary dict, List<byte[]> objectList)
		{
		var result = new List<byte>
			{
				 (byte)(0xE0 + Math.Min(dict.Count, 0xF)),
			};
		foreach (DictionaryEntry entry in dict)
			{
			result.AddRange (PackInternal (entry.Key, objectList));
			result.AddRange (PackInternal (entry.Value, objectList));
			}

		if (dict.Count >= 0xF)
			{
			result.Add (0x03);
			}

		return [.. result];
		}

	/// <summary>Unpacks raw OPACK data into .NET objects.</summary>
	/// <param name="data">The OPACK-encoded bytes.</param>
	/// <param name="consumed">The number of bytes consumed from <paramref name="data"/>.</param>
	/// <returns>The decoded value.</returns>
	// pyatv/support/opack.py (unpack) — line 144-146 as of pyatv 0.18.0
	public static object? Unpack (ReadOnlySpan<byte> data, out int consumed)
		{
		var objectList = new List<object?> ();
		return UnpackInternal (data, objectList, out consumed);
		}

	// pyatv/support/opack.py (_unpack) — line 149-233 as of pyatv 0.18.0
	private static object? UnpackInternal (ReadOnlySpan<byte> data, List<object?> objectList, out int consumed)
		{
		object? value;
		var addToObjectList = true;
		var tag = data[0];

		if (tag == 0x01)
			{
			// pyatv/support/opack.py — line 155-157 as of pyatv 0.18.0
			value = true;
			addToObjectList = false;
			consumed = 1;
			}
		else if (tag == 0x02)
			{
			// pyatv/support/opack.py — line 158-160 as of pyatv 0.18.0
			value = false;
			addToObjectList = false;
			consumed = 1;
			}
		else if (tag == 0x04)
			{
			// pyatv/support/opack.py — line 161-163 as of pyatv 0.18.0
			value = null;
			addToObjectList = false;
			consumed = 1;
			}
		else if (tag == 0x05)
			{
			// pyatv/support/opack.py — line 164-165 as of pyatv 0.18.0
			value = new Guid (data.Slice (1, 16).ToArray ());
			consumed = 17;
			}
		else if (tag == 0x06)
			{
			// pyatv/support/opack.py (dummy implementation: parse as integer only) — line 166-168 as of pyatv 0.18.0
			value = LittleEndianToLong (data.Slice (1, 8));
			consumed = 9;
			}
		else if (tag is >= 0x08 and <= 0x2F)
			{
			// pyatv/support/opack.py — line 169-171 as of pyatv 0.18.0
			value = (long)(tag - 8);
			addToObjectList = false;
			consumed = 1;
			}
		else if (tag == 0x35)
			{
			// pyatv/support/opack.py — line 172-173 as of pyatv 0.18.0
			value = System.Runtime.InteropServices.MemoryMarshal.Read<float> (data.Slice (1, 4));
			consumed = 5;
			}
		else if (tag == 0x36)
			{
			// pyatv/support/opack.py — line 174-175 as of pyatv 0.18.0
			value = System.Runtime.InteropServices.MemoryMarshal.Read<double> (data.Slice (1, 8));
			consumed = 9;
			}
		else if ((tag & 0xF0) == 0x30)
			{
			// pyatv/support/opack.py — line 176-183 as of pyatv 0.18.0
			var noofBytes = 1 << (tag & 0xF);
			var intValue = LittleEndianToLong (data.Slice (1, noofBytes));
			value = new SizedInteger (intValue, noofBytes);
			consumed = 1 + noofBytes;
			}
		else if (tag is >= 0x40 and <= 0x60)
			{
			// pyatv/support/opack.py — line 184-186 as of pyatv 0.18.0
			var length = tag - 0x40;
			value = Encoding.UTF8.GetString (data.Slice (1, length).ToArray ());
			consumed = 1 + length;
			}
		else if (tag is > 0x60 and <= 0x64)
			{
			// pyatv/support/opack.py — line 187-193 as of pyatv 0.18.0
			var noofBytes = tag & 0xF;
			var length = (int)LittleEndianToLong (data.Slice (1, noofBytes));
			value = Encoding.UTF8.GetString (data.Slice (1 + noofBytes, length).ToArray ());
			consumed = 1 + noofBytes + length;
			}
		else if (tag is >= 0x70 and <= 0x90)
			{
			// pyatv/support/opack.py — line 194-196 as of pyatv 0.18.0
			var length = tag - 0x70;
			value = data.Slice (1, length).ToArray ();
			consumed = 1 + length;
			}
		else if (tag is >= 0x91 and <= 0x94)
			{
			// pyatv/support/opack.py — line 197-203 as of pyatv 0.18.0
			var noofBytes = 1 << ((tag & 0xF) - 1);
			var length = (int)LittleEndianToLong (data.Slice (1, noofBytes));
			value = data.Slice (1 + noofBytes, length).ToArray ();
			consumed = 1 + noofBytes + length;
			}
		else if ((tag & 0xF0) == 0xD0)
			{
			// pyatv/support/opack.py (list) — line 204-217 as of pyatv 0.18.0
			var count = tag & 0xF;
			var output = new List<object?> ();
			var offset = 1;
			if (count == 0xF)
				{
				// Endless list
				while (data[offset] != 0x03)
					{
					var item = UnpackInternal (data[offset..], objectList, out var itemConsumed);
					output.Add (item);
					offset += itemConsumed;
					}

				offset += 1;
				}
			else
				{
				for (var i = 0; i < count; i++)
					{
					var item = UnpackInternal (data[offset..], objectList, out var itemConsumed);
					output.Add (item);
					offset += itemConsumed;
					}
				}

			value = output;
			addToObjectList = false;
			consumed = offset;
			}
		else if ((tag & 0xE0) == 0xE0)
			{
			// pyatv/support/opack.py (dict) — line 218-232 as of pyatv 0.18.0
			var count = tag & 0xF;
			var output = new Dictionary<object, object?> ();
			var offset = 1;
			if (count == 0xF)
				{
				// Endless dict
				while (data[offset] != 0x03)
					{
					var key = UnpackInternal (data[offset..], objectList, out var keyConsumed);
					offset += keyConsumed;
					var item = UnpackInternal (data[offset..], objectList, out var itemConsumed);
					offset += itemConsumed;
					output[key!] = item;
					}

				offset += 1;
				}
			else
				{
				for (var i = 0; i < count; i++)
					{
					var key = UnpackInternal (data[offset..], objectList, out var keyConsumed);
					offset += keyConsumed;
					var item = UnpackInternal (data[offset..], objectList, out var itemConsumed);
					offset += itemConsumed;
					output[key!] = item;
					}
				}

			value = output;
			addToObjectList = false;
			consumed = offset;
			}
		else if (tag is >= 0xA0 and <= 0xC0)
			{
			// pyatv/support/opack.py (pointer, single byte) — line 233-234 as of pyatv 0.18.0
			value = objectList[tag - 0xA0];
			consumed = 1;
			}
		else if (tag is >= 0xC1 and <= 0xC4)
			{
			// pyatv/support/opack.py (pointer, multi-byte index) — line 235-241 as of pyatv 0.18.0
			var length = tag - 0xC0;
			var uid = (int)LittleEndianToLong (data.Slice (1, length));
			value = objectList[uid];
			consumed = 1 + length;
			}
		else
			{
			throw new NotSupportedException ($"0x{tag:x2}");
			}

		// pyatv/support/opack.py (object list, unpack side) — line 243-244 as of pyatv 0.18.0
		if (addToObjectList && !objectList.Any (existing => OpackEquals (existing, value)))
			{
			objectList.Add (value);
			}

		return value;
		}

	private static long LittleEndianToLong (ReadOnlySpan<byte> data)
		{
		long result = 0;
		for (var i = data.Length - 1; i >= 0; i--)
			{
			result = (result << 8) | data[i];
			}

		return result;
		}

	private static byte[] IntToLittleEndian (long value, int byteCount)
		{
		var result = new byte[byteCount];
		for (var i = 0; i < byteCount; i++)
			{
			result[i] = (byte)(value & 0xFF);
			value >>= 8;
			}

		return result;
		}

	private static int IndexOfBytes (List<byte[]> objectList, byte[] target)
		{
		for (var i = 0; i < objectList.Count; i++)
			{
			if (objectList[i].AsSpan ().SequenceEqual (target))
				{
				return i;
				}
			}

		return -1;
		}

	private static bool OpackEquals (object? a, object? b)
		{
		if (ReferenceEquals (a, b))
			{
			return true;
			}

		if (a is null || b is null)
			{
			return false;
			}

		if (a is byte[] aBytes && b is byte[] bBytes)
			{
			return aBytes.AsSpan ().SequenceEqual (bBytes);
			}

		if (a is List<object?> aList && b is List<object?> bList)
			{
			if (aList.Count != bList.Count)
				{
				return false;
				}

			for (var i = 0; i < aList.Count; i++)
				{
				if (!OpackEquals (aList[i], bList[i]))
					{
					return false;
					}
				}

			return true;
			}

		if (a is Dictionary<object, object?> aDict && b is Dictionary<object, object?> bDict)
			{
			if (aDict.Count != bDict.Count)
				{
				return false;
				}

			foreach (KeyValuePair<object, object?> kvp in aDict)
				{
				if (!bDict.TryGetValue (kvp.Key, out var bValue) || !OpackEquals (kvp.Value, bValue))
					{
					return false;
					}
				}

			return true;
			}

		return a.Equals (b);
		}
	}