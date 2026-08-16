// Copyright (c) 2026 Neil Colvin. Licensed under the MIT License.
// See LICENSE file in the repository root for full license text.

using System;
using System.IO;

using Claunia.PropertyList;

namespace AppleTvControlLibrary.Mrp.AirPlay.Http;

/// <summary>
/// Encode/decode binary property list request/response bodies used by the AirPlay 2 control
/// connection (RTSP <c>SETUP</c> payloads etc.).
/// </summary>
// pyatv/protocols/airplay/utils.py (encode_plist_body, decode_plist_body) — line 183-198 as of pyatv 0.18.0
public static class PlistBody
	{
	/// <summary>Encode an <see cref="NSDictionary"/> as a binary property list.</summary>
	/// <param name="data">The dictionary to encode.</param>
	/// <returns>The encoded binary plist bytes.</returns>
	// pyatv/protocols/airplay/utils.py (encode_plist_body) — line 183-189 as of pyatv 0.18.0
	public static byte[] Encode (NSDictionary data)
		{
		using var stream = new MemoryStream ();
		BinaryPropertyListWriter.Write (stream, data);
		return stream.ToArray ();
		}

	/// <summary>Decode a binary property list body into an <see cref="NSDictionary"/>.</summary>
	/// <param name="body">The raw binary plist bytes.</param>
	/// <returns>The decoded dictionary.</returns>
	// pyatv/protocols/airplay/utils.py (decode_plist_body) — line 191-198 as of pyatv 0.18.0
	public static NSDictionary Decode (byte[] body) => PropertyListParser.Parse (body) is not NSDictionary dict
			? throw new InvalidDataException ("expected a plist dictionary at the top level")
			: dict;
	}
