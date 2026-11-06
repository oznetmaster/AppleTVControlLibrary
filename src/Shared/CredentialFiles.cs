// Copyright (c) 2026 Neil Colvin. Licensed under the MIT License.
// See LICENSE file in the repository root for full license text.

using System;
using System.IO;
using System.Text;
using System.Text.Json;

namespace AppleTvControlLibrary.Remote.Storage;

internal static class CredentialFiles
	{
	private const string PreferenceFile = ".auto-connect";

	internal static void WriteAtomically (string path, string content)
		{
		string temporary = Path.Combine (Path.GetDirectoryName (path)!, $".{Guid.NewGuid ():N}.tmp");
		bool created = false;
		try
			{
			byte[] bytes = Encoding.UTF8.GetBytes (content);
			using (var stream = new FileStream (temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
				{
				created = true;
				stream.Write (bytes, 0, bytes.Length);
				stream.Flush (flushToDisk: true);
				}

			if (File.Exists (path))
				File.Replace (temporary, path, destinationBackupFileName: null);
			else
				File.Move (temporary, path);
			}
		finally
			{
			// A failed save must preserve the original error as well as the old record.
			if (created)
				{
				try { File.Delete (temporary); }
				catch (IOException) { }
				catch (UnauthorizedAccessException) { }
				}
			}
		}

	internal static bool TryGetAutoConnect (string directory, out string? uniqueId)
		{
		uniqueId = null;
		try
			{
			uniqueId = JsonSerializer.Deserialize<string> (File.ReadAllText (Path.Combine (directory, PreferenceFile)));
			return true;
			}
		catch (FileNotFoundException)
			{
			return false; // No override: respect the legacy flag in each credential record.
			}
		catch (JsonException)
			{
			return true; // A damaged preference should neither block credential reads nor auto-connect.
			}
		}

	internal static void SetAutoConnect (string directory, string? uniqueId) =>
		WriteAtomically (Path.Combine (directory, PreferenceFile), JsonSerializer.Serialize (uniqueId));
	}
