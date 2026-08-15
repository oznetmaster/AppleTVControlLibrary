// Copyright (c) 2026 Neil Colvin. Licensed under the MIT License.
// See LICENSE file in the repository root for full license text.

using Claunia.PropertyList;

namespace AppleTvControlLibrary.Text;

/// <summary>
/// Helper functions to generate NSKeyedArchiver-encoded payloads for the RTI (Remote Text Input)
/// service used by Companion Link text input (<c>_tiC</c>).
/// </summary>
/// <remarks>
/// pyatv does not implement a real NSKeyedArchiver writer for these; instead it constructs the
/// <c>$objects</c> array literally, with hand-wired UID indices. This is a byte-for-byte port of
/// that same approach, including the object ordering (which matters for the resulting UID
/// indices to match).
/// </remarks>
// pyatv/protocols/companion/plist_payloads/rti_text_operations.py — line 1-147 as of pyatv 0.18.0
public static class RtiTextOperations
	{
	/// <summary>
	/// Prepare an NSKeyedArchiver encoded payload for clearing the RTI text.
	/// </summary>
	/// <param name="sessionUuid">The 16 raw bytes of the RTI session UUID.</param>
	/// <returns>The binary plist encoded payload bytes, for use as the <c>_tiD</c> field.</returns>
	// pyatv/protocols/companion/plist_payloads/rti_text_operations.py (get_rti_clear_text_payload) — line 12-78 as of pyatv 0.18.0
	public static byte[] GetRtiClearTextPayload (byte[] sessionUuid)
		{
		// $objects[0] — pyatv/protocols/companion/plist_payloads/rti_text_operations.py — line 38 as of pyatv 0.18.0
		var objects = new NSArray (0)
			{
			new NSString ("$null")
			};

		// $objects[1] — pyatv/protocols/companion/plist_payloads/rti_text_operations.py — line 39-44 as of pyatv 0.18.0
		var textOperations = new NSDictionary
			{
				{ "$class", new UID ((byte)7) },
				{ "targetSessionUUID", new UID ((byte)5) },
				{ "keyboardOutput", new UID ((byte)2) },
				{ "textToAssert", new UID ((byte)4) }
			};
		objects.Add (textOperations);

		// $objects[2] — pyatv/protocols/companion/plist_payloads/rti_text_operations.py — line 45-47 as of pyatv 0.18.0
		var keyboardOutput = new NSDictionary
			{
				{ "$class", new UID ((byte)3) }
			};
		objects.Add (keyboardOutput);

		// $objects[3] — pyatv/protocols/companion/plist_payloads/rti_text_operations.py — line 48-54 as of pyatv 0.18.0
		objects.Add (KeyboardOutputClass ());

		// $objects[4] — pyatv/protocols/companion/plist_payloads/rti_text_operations.py — line 55 as of pyatv 0.18.0
		objects.Add (new NSString (string.Empty));

		// $objects[5] — pyatv/protocols/companion/plist_payloads/rti_text_operations.py — line 56-59 as of pyatv 0.18.0
		var sessionUuidObject = new NSDictionary
			{
				{ "NS.uuidbytes", new NSData (sessionUuid) },
				{ "$class", new UID ((byte)6) }
			};
		objects.Add (sessionUuidObject);

		// $objects[6] — pyatv/protocols/companion/plist_payloads/rti_text_operations.py — line 60-66 as of pyatv 0.18.0
		objects.Add (NsuuidClass ());

		// $objects[7] — pyatv/protocols/companion/plist_payloads/rti_text_operations.py — line 67-73 as of pyatv 0.18.0
		objects.Add (RtiTextOperationsClass ());

		return Encode (objects);
		}

	/// <summary>
	/// Prepare an NSKeyedArchiver encoded payload for RTI text input.
	/// </summary>
	/// <param name="sessionUuid">The 16 raw bytes of the RTI session UUID.</param>
	/// <param name="text">The text to insert.</param>
	/// <returns>The binary plist encoded payload bytes, for use as the <c>_tiD</c> field.</returns>
	// pyatv/protocols/companion/plist_payloads/rti_text_operations.py (get_rti_input_text_payload) — line 81-147 as of pyatv 0.18.0
	public static byte[] GetRtiInputTextPayload (byte[] sessionUuid, string text)
		{
		// $objects[0] — pyatv/protocols/companion/plist_payloads/rti_text_operations.py — line 107 as of pyatv 0.18.0
		var objects = new NSArray (0)
			{
			new NSString ("$null")
			};

		// $objects[1] — pyatv/protocols/companion/plist_payloads/rti_text_operations.py — line 108-112 as of pyatv 0.18.0
		var textOperations = new NSDictionary
			{
				{ "keyboardOutput", new UID ((byte)2) },
				{ "$class", new UID ((byte)7) },
				{ "targetSessionUUID", new UID ((byte)5) }
			};
		objects.Add (textOperations);

		// $objects[2] — pyatv/protocols/companion/plist_payloads/rti_text_operations.py — line 113-116 as of pyatv 0.18.0
		var keyboardOutput = new NSDictionary
			{
				{ "insertionText", new UID ((byte)3) },
				{ "$class", new UID ((byte)4) }
			};
		objects.Add (keyboardOutput);

		// $objects[3] — pyatv/protocols/companion/plist_payloads/rti_text_operations.py — line 117 as of pyatv 0.18.0
		objects.Add (new NSString (text));

		// $objects[4] — pyatv/protocols/companion/plist_payloads/rti_text_operations.py — line 118-124 as of pyatv 0.18.0
		objects.Add (KeyboardOutputClass ());

		// $objects[5] — pyatv/protocols/companion/plist_payloads/rti_text_operations.py — line 125-128 as of pyatv 0.18.0
		var sessionUuidObject = new NSDictionary
			{
				{ "NS.uuidbytes", new NSData (sessionUuid) },
				{ "$class", new UID ((byte)6) }
			};
		objects.Add (sessionUuidObject);

		// $objects[6] — pyatv/protocols/companion/plist_payloads/rti_text_operations.py — line 129-135 as of pyatv 0.18.0
		objects.Add (NsuuidClass ());

		// $objects[7] — pyatv/protocols/companion/plist_payloads/rti_text_operations.py — line 136-142 as of pyatv 0.18.0
		objects.Add (RtiTextOperationsClass ());

		return Encode (objects);
		}

	// pyatv/protocols/companion/plist_payloads/rti_text_operations.py — line 48-54, 118-124 as of pyatv 0.18.0
	private static NSDictionary KeyboardOutputClass ()
		{
		var classes = new NSArray (0)
			{
			new NSString ("TIKeyboardOutput"),
			new NSString ("NSObject")
			};

		var dict = new NSDictionary
			{
				{ "$classname", "TIKeyboardOutput" },
				{ "$classes", classes }
			};
		return dict;
		}

	// pyatv/protocols/companion/plist_payloads/rti_text_operations.py — line 60-66, 129-135 as of pyatv 0.18.0
	private static NSDictionary NsuuidClass ()
		{
		var classes = new NSArray (0)
			{
			new NSString ("NSUUID"),
			new NSString ("NSObject")
			};

		var dict = new NSDictionary
			{
				{ "$classname", "NSUUID" },
				{ "$classes", classes }
			};
		return dict;
		}

	// pyatv/protocols/companion/plist_payloads/rti_text_operations.py — line 67-73, 136-142 as of pyatv 0.18.0
	private static NSDictionary RtiTextOperationsClass ()
		{
		var classes = new NSArray (0)
			{
			new NSString ("RTITextOperations"),
			new NSString ("NSObject")
			};

		var dict = new NSDictionary
			{
				{ "$classname", "RTITextOperations" },
				{ "$classes", classes }
			};
		return dict;
		}

	// pyatv/protocols/companion/plist_payloads/rti_text_operations.py (plistlib.dumps(..., fmt=plistlib.PlistFormat.FMT_BINARY, sort_keys=False)) — line 30-78, 99-147 as of pyatv 0.18.0
	private static byte[] Encode (NSArray objects)
		{
		var top = new NSDictionary
			{
				{ "textOperations", new UID ((byte)1) }
			};

		var root = new NSDictionary
			{
				{ "$version", 100000L },
				{ "$archiver", "RTIKeyedArchiver" },
				{ "$top", top },
				{ "$objects", objects }
			};

		return BinaryPropertyListWriter.WriteToArray (root);
		}
	}
