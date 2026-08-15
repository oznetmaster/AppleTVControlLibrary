// Copyright (c) 2026 Neil Colvin. Licensed under the MIT License.
// See LICENSE file in the repository root for full license text.

using System;

namespace AppleTvControlLibrary.Mrp.Support;

/// <summary>
/// Reads and writes Google protobuf variant (varint) values, used to length-prefix
/// MRP protobuf messages on the wire.
/// </summary>
// pyatv/support/variant.py — line 1-20 as of pyatv 0.18.0
public static class Variant
	{
	/// <summary>Read and parse a binary protobuf variant value.</summary>
	/// <param name="variant">The buffer to read the variant from. May contain trailing data.</param>
	/// <returns>The decoded value and the remaining, unconsumed bytes.</returns>
	// pyatv/support/variant.py (read_variant) — line 4-12 as of pyatv 0.18.0
	public static (long Value, byte[] Remaining) ReadVariant (byte[] variant)
		{
		long result = 0;
		int cnt = 0;

		foreach (byte data in variant)
			{
			result |= (long)(data & 0x7F) << (7 * cnt);
			cnt += 1;
			if ((data & 0x80) == 0)
				{
				return (result, variant.AsSpan (cnt).ToArray ());
				}
			}

		throw new ArgumentException ("invalid variant", nameof (variant));
		}

	/// <summary>Convert an integer to a protobuf variant binary buffer.</summary>
	/// <param name="number">The value to encode. Must be non-negative.</param>
	/// <returns>The encoded variant bytes.</returns>
	// pyatv/support/variant.py (write_variant) — line 15-19 as of pyatv 0.18.0
	public static byte[] WriteVariant (long number)
		{
		Span<byte> buffer = stackalloc byte[10];
		int count = 0;
		ulong value = (ulong)number;

		do
			{
			byte b = (byte)(value & 0x7F);
			value >>= 7;
			buffer[count] = (byte)(value != 0 ? b | 0x80 : b);
			count += 1;
			}
		while (value != 0);

		return buffer[..count].ToArray ();
		}
	}
