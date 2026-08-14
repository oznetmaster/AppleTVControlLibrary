// Copyright (c) 2026 Neil Colvin. Licensed under the MIT License.
// See LICENSE file in the repository root for full license text.

namespace AppleTvControlLibrary.Mrp.Auth;

/// <summary>
/// HKDF salt/info strings used to derive MRP transport encryption keys after pair-verify.
/// </summary>
// pyatv/protocols/mrp/protocol.py — line 25-27 as of pyatv 0.18.0
public static class MrpProtocolConstants
	{
	/// <summary>HKDF salt used for both output and input key derivation. pyatv/protocols/mrp/protocol.py — line 25 as of pyatv 0.18.0</summary>
	public const string SrpSalt = "MediaRemote-Salt";

	/// <summary>HKDF info string for the key used to encrypt outgoing data. pyatv/protocols/mrp/protocol.py — line 26 as of pyatv 0.18.0</summary>
	public const string SrpOutputInfo = "MediaRemote-Write-Encryption-Key";

	/// <summary>HKDF info string for the key used to decrypt incoming data. pyatv/protocols/mrp/protocol.py — line 27 as of pyatv 0.18.0</summary>
	public const string SrpInputInfo = "MediaRemote-Read-Encryption-Key";
	}
