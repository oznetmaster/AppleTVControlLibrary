// Copyright (c) 2026 Neil Colvin. Licensed under the MIT License.
// See LICENSE file in the repository root for full license text.

using System;
using System.Collections.Generic;

using Claunia.PropertyList;

namespace AppleTvControlLibrary.Text;

/// <summary>
/// Support for reading a subset of NSKeyedArchiver serialized data.
/// </summary>
/// <remarks>
/// pyatv does not implement a full NSKeyedArchiver reader; it just loads the plist and walks
/// <c>$top</c> down a key path, dereferencing UID values into <c>$objects</c> as it goes. This
/// is a direct port of that same limited approach -- do not extend it into a general archiver.
/// </remarks>
// pyatv/protocols/companion/keyed_archiver.py (read_archive_properties) — line 7-28 as of pyatv 0.18.0
public static class KeyedArchiver
	{
	/// <summary>
	/// Get properties from an NSKeyedArchiver encoded binary plist.
	/// </summary>
	/// <param name="archive">The raw NSKeyedArchiver encoded binary plist bytes.</param>
	/// <param name="paths">One or more key paths to follow from <c>$top</c>, dereferencing UID values into <c>$objects</c> along the way.</param>
	/// <returns>
	/// One resolved value per requested path, in the same order, or <see langword="null"/> for a
	/// path that could not be fully resolved.
	/// </returns>
	// pyatv/protocols/companion/keyed_archiver.py (read_archive_properties) — line 7-28 as of pyatv 0.18.0
	public static object?[] ReadArchiveProperties (byte[] archive, params string[][] paths)
		{
		// pyatv/protocols/companion/keyed_archiver.py (data = plistlib.loads(archive)) — line 13 as of pyatv 0.18.0
		NSObject parsed = PropertyListParser.Parse (archive);
		if (parsed is not NSDictionary data)
			{
			throw new ArgumentException ("Archive root is not a dictionary.", nameof (archive));
			}

		// pyatv/protocols/companion/keyed_archiver.py (objects = data["$objects"]) — line 16 as of pyatv 0.18.0
		NSObject? objectsObj = data.ObjectForKey ("$objects");
		if (objectsObj is not NSArray objects)
			{
			throw new ArgumentException ("Archive is missing an $objects array.", nameof (archive));
			}

		var results = new List<object?> ();

		// pyatv/protocols/companion/keyed_archiver.py (for path in paths) — line 17-26 as of pyatv 0.18.0
		foreach (string[] path in paths)
			{
			results.Add (ResolvePath (data, objects, path));
			}

		return [.. results];
		}

	// pyatv/protocols/companion/keyed_archiver.py (element = data["$top"] ... for key in path) — line 18-25 as of pyatv 0.18.0
	private static object? ResolvePath (NSDictionary data, NSArray objects, string[] path)
		{
		// pyatv/protocols/companion/keyed_archiver.py (element = data["$top"]) — line 18 as of pyatv 0.18.0
		NSObject? topObj = data.ObjectForKey ("$top");
		if (topObj is not NSDictionary element)
			{
			return null;
			}

		object? current = element;

		foreach (string key in path)
			{
			if (current is not NSDictionary dict)
				{
				// pyatv/protocols/companion/keyed_archiver.py (except (IndexError, KeyError)) — line 25 as of pyatv 0.18.0
				return null;
				}

			current = dict.ObjectForKey (key);
			if (current is null)
				{
				// pyatv/protocols/companion/keyed_archiver.py (except (IndexError, KeyError)) — line 25 as of pyatv 0.18.0
				return null;
				}

			// pyatv/protocols/companion/keyed_archiver.py (if isinstance(element, plistlib.UID)) — line 22-23 as of pyatv 0.18.0
			if (current is UID uid)
				{
				long index = (long)uid.ToUInt64 ();
				if (index < 0 || index >= objects.Count)
					{
					return null;
					}

				current = objects[(int)index];
				}
			}

		return Unwrap (current);
		}

	// Convert leaf Claunia.PropertyList values into plain CLR values (string/byte[]), mirroring
	// what plistlib.loads produces for the leaves pyatv actually reads (strings and NS.uuidbytes
	// byte blobs) — pyatv/protocols/companion/keyed_archiver.py does not itself convert these,
	// since Python's plistlib already yields plain str/bytes for these node types.
	private static object? Unwrap (object? value) => value switch
		{
			null => null,
			NSString s => s.Content,
			NSData d => d.Bytes,
			NSNumber n => n.ToObject (),
			_ => value,
			};
	}
