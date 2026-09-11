// Copyright (c) 2026 Neil Colvin. Licensed under the MIT License.
// See LICENSE file in the repository root for full license text.

using System.IO;

using AppleTvControlLibrary.Mrp.AirPlay.Http;

using Claunia.PropertyList;

using NUnit.Framework;

namespace AppleTvControlLibrary.Mrp.Tests.AirPlay.Http;

/// <summary>
/// Tests for <see cref="PlistBody"/> binary property list encode/decode used by the AirPlay 2
/// control connection.
/// </summary>
// pyatv/protocols/airplay/utils.py (encode_plist_body, decode_plist_body) — line 183-198 as of pyatv 0.18.0
[TestFixture]
[FixtureLifeCycle (LifeCycle.InstancePerTestCase)]
public class PlistBodyTests
	{
	[Test]
	public void EncodeThenDecodeRoundTripsScalarValues ()
		{
		var dict = new NSDictionary ();
		dict.Add ("isRemoteControlOnly", true);
		dict.Add ("osName", "iPhone OS");
		dict.Add ("timingPort", 12345);

		byte[] encoded = PlistBody.Encode (dict);
		NSDictionary decoded = PlistBody.Decode (encoded);

		Assert.That (((NSNumber)decoded.ObjectForKey ("isRemoteControlOnly")).ToBool (), Is.True);
		Assert.That (decoded.ObjectForKey ("osName").ToString (), Is.EqualTo ("iPhone OS"));
		Assert.That (((NSNumber)decoded.ObjectForKey ("timingPort")).ToInt (), Is.EqualTo (12345));
		}

	[Test]
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
		Assert.That (decodedData, Is.EqualTo (new byte[] { 0x01, 0x02, 0x03 }));
		}

	[Test]
	public void EncodeThenDecodeRoundTripsEmptyDictionary ()
		{
		var dict = new NSDictionary ();

		byte[] encoded = PlistBody.Encode (dict);
		NSDictionary decoded = PlistBody.Decode (encoded);

		Assert.That (decoded.Count, Is.EqualTo (0));
		}

	[Test]
	public void DecodeThrowsWhenTopLevelIsNotADictionary ()
		{
		// A top-level plist array, rather than a dictionary.
		var array = new NSArray (new NSString ("a"), new NSString ("b"));
		using var stream = new MemoryStream ();
		BinaryPropertyListWriter.Write (stream, array);
		byte[] encoded = stream.ToArray ();

		Assert.Catch<InvalidDataException> (() => PlistBody.Decode (encoded));
		}
	}