// Copyright (c) 2026 Neil Colvin. Licensed under the MIT License.
// See LICENSE file in the repository root for full license text.

using System.IO;

using AppleTvControlLibrary.Mrp.AirPlay.Http;

using Claunia.PropertyList;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AppleTvControlLibrary.Mrp.Tests.AirPlay.Http;

/// <summary>
/// Tests for <see cref="PlistBody"/> binary property list encode/decode used by the AirPlay 2
/// control connection.
/// </summary>
// pyatv/protocols/airplay/utils.py (encode_plist_body, decode_plist_body) — line 183-198 as of pyatv 0.18.0
[TestClass]
public class PlistBodyTests
	{
	[TestMethod]
	public void EncodeThenDecodeRoundTripsScalarValues ()
		{
		var dict = new NSDictionary ();
		dict.Add ("isRemoteControlOnly", true);
		dict.Add ("osName", "iPhone OS");
		dict.Add ("timingPort", 12345);

		byte[] encoded = PlistBody.Encode (dict);
		NSDictionary decoded = PlistBody.Decode (encoded);

		Assert.IsTrue (((NSNumber)decoded.ObjectForKey ("isRemoteControlOnly")).ToBool ());
		Assert.AreEqual ("iPhone OS", decoded.ObjectForKey ("osName").ToString ());
		Assert.AreEqual (12345, ((NSNumber)decoded.ObjectForKey ("timingPort")).ToInt ());
		}

	[TestMethod]
	public void EncodeThenDecodeRoundTripsNestedDictionaries ()
		{
		var inner = new NSDictionary ();
		inner.Add ("data", new byte[] { 0x01, 0x02, 0x03 });

		var outer = new NSDictionary ();
		outer.Add ("params", inner);

		byte[] encoded = PlistBody.Encode (outer);
		NSDictionary decoded = PlistBody.Decode (encoded);

		var decodedInner = (NSDictionary)decoded.ObjectForKey ("params");
		byte[] decodedData = ((NSData)decodedInner.ObjectForKey ("data")).Bytes;
		CollectionAssert.AreEqual (new byte[] { 0x01, 0x02, 0x03 }, decodedData);
		}

	[TestMethod]
	public void EncodeThenDecodeRoundTripsEmptyDictionary ()
		{
		var dict = new NSDictionary ();

		byte[] encoded = PlistBody.Encode (dict);
		NSDictionary decoded = PlistBody.Decode (encoded);

		Assert.AreEqual (0, decoded.Count);
		}

	[TestMethod]
	public void DecodeThrowsWhenTopLevelIsNotADictionary ()
		{
		// A top-level plist array, rather than a dictionary.
		var array = new NSArray (new NSString ("a"), new NSString ("b"));
		using var stream = new MemoryStream ();
		BinaryPropertyListWriter.Write (stream, array);
		byte[] encoded = stream.ToArray ();

		Assert.Throws<InvalidDataException> (() => PlistBody.Decode (encoded));
		}
	}
