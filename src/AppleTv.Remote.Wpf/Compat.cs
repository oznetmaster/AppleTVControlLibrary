// Copyright (c) 2026 Neil Colvin. Licensed under the MIT License.
// See LICENSE file in the repository root for full license text.

#if NET472
using System;
using System.Security.Cryptography;

namespace AppleTvControlLibrary.Remote.Wpf;

/// <summary>
/// Small polyfills for BCL members that only exist on <c>net10.0</c>, scoped to this WPF
/// application (not the protocol layer, which must have a single code path per
/// <c>.github/copilot-instructions.md</c> rule 4). Used only by <c>net472</c>.
/// </summary>
internal static class Compat
	{
	/// <summary>Polyfill for <c>Convert.ToHexString</c>, not available on <c>net472</c>.</summary>
	/// <param name="bytes">The bytes to convert.</param>
	/// <returns>An upper-case hex string, matching <c>Convert.ToHexString</c>'s casing.</returns>
	public static string ToHexString (byte[] bytes)
		{
		char[] chars = new char[bytes.Length * 2];
		for (int i = 0; i < bytes.Length; i++)
			{
			string hex = bytes[i].ToString ("X2", System.Globalization.CultureInfo.InvariantCulture);
			chars[i * 2] = hex[0];
			chars[(i * 2) + 1] = hex[1];
			}

		return new string (chars);
		}

	/// <summary>Polyfill for <c>Math.Clamp(int, int, int)</c>, not available on <c>net472</c>.</summary>
	/// <param name="value">The value to clamp.</param>
	/// <param name="min">The inclusive lower bound.</param>
	/// <param name="max">The inclusive upper bound.</param>
	/// <returns><paramref name="value"/> clamped to <c>[min, max]</c>.</returns>
	public static int Clamp (int value, int min, int max)
		{
		if (value < min)
			{
			return min;
			}

		if (value > max)
			{
			return max;
			}

		return value;
		}

	/// <summary>Polyfill for <c>RandomNumberGenerator.Fill</c>, not available on <c>net472</c>.</summary>
	/// <param name="data">The buffer to fill with random bytes.</param>
	public static void FillRandom (byte[] data)
		{
		using RandomNumberGenerator rng = RandomNumberGenerator.Create ();
		rng.GetBytes (data);
		}
	}
#endif
