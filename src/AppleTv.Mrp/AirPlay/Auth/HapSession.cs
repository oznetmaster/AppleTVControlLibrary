// Copyright (c) 2026 Neil Colvin. Licensed under the MIT License.
// See LICENSE file in the repository root for full license text.

using System;

using AppleTvControlLibrary.Crypto;

namespace AppleTvControlLibrary.Mrp.AirPlay.Auth;

/// <summary>
/// Manages cryptography for a HAP session according to IP in the HomeKit Accessory Protocol
/// specification: data is encrypted/decrypted in blocks of 1024 bytes. Transparent (passthrough)
/// until encryption is enabled.
/// </summary>
// pyatv/auth/hap_session.py (HAPSession) — line 7-66 as of pyatv 0.18.0
public sealed class HapSession
	{
	// pyatv/auth/hap_session.py (FRAME_LENGTH) — line 16 as of pyatv 0.18.0
	private const int FrameLength = 1024;

	// pyatv/auth/hap_session.py (AUTH_TAG_LENGTH) — line 17 as of pyatv 0.18.0
	private const int AuthTagLength = 16;

	private readonly System.Collections.Generic.List<byte> _encryptedData = [];
	private Chacha20Cipher? _chacha20;

	/// <summary>Gets a value indicating whether encryption has been enabled.</summary>
	public bool IsEnabled => _chacha20 is not null;

	/// <summary>Enable encryption with the specified keys.</summary>
	/// <param name="outputKey">The key used to encrypt outgoing data.</param>
	/// <param name="inputKey">The key used to decrypt incoming data.</param>
	// pyatv/auth/hap_session.py (enable) — line 26-28 as of pyatv 0.18.0
	public void Enable (byte[] outputKey, byte[] inputKey)
		{
		_chacha20 = new Chacha20Cipher (outputKey, inputKey);
		}

	/// <summary>Decrypt incoming data, accumulating partial blocks across calls.</summary>
	/// <param name="data">The bytes just received from the transport.</param>
	/// <returns>Any decrypted plaintext produced by fully-received blocks so far; may be empty.</returns>
	// pyatv/auth/hap_session.py (decrypt) — line 30-46 as of pyatv 0.18.0
	public byte[] Decrypt (byte[] data)
		{
		if (_chacha20 is null)
			{
			return data;
			}

		_encryptedData.AddRange (data);

		var output = new System.Collections.Generic.List<byte> ();
		while (_encryptedData.Count > 0)
			{
			if (_encryptedData.Count < 2)
				{
				break;
				}

			byte[] length = [_encryptedData[0], _encryptedData[1]];
			int blockLength = (length[0] | (length[1] << 8)) + AuthTagLength;

			if (_encryptedData.Count < blockLength + 2)
				{
				break;
				}

			byte[] block = _encryptedData.GetRange (2, blockLength).ToArray ();
			output.AddRange (_chacha20.Decrypt (block, aad: length));

			_encryptedData.RemoveRange (0, 2 + blockLength);
			}

		return [.. output];
		}

	/// <summary>Encrypt outgoing data, splitting it into 1024-byte frames.</summary>
	/// <param name="data">The plaintext to encrypt.</param>
	/// <returns>The framed, encrypted bytes ready to write to the transport.</returns>
	// pyatv/auth/hap_session.py (encrypt) — line 48-58 as of pyatv 0.18.0
	public byte[] Encrypt (byte[] data)
		{
		if (_chacha20 is null)
			{
			return data;
			}

		var output = new System.Collections.Generic.List<byte> ();
		int offset = 0;
		while (offset < data.Length)
			{
			int frameLength = Math.Min (FrameLength, data.Length - offset);
			var frame = new byte[frameLength];
			Array.Copy (data, offset, frame, 0, frameLength);
			offset += frameLength;

			byte[] length = [(byte)(frameLength & 0xFF), (byte)((frameLength >> 8) & 0xFF)];
			byte[] encryptedFrame = _chacha20.Encrypt (frame, aad: length);

			output.AddRange (length);
			output.AddRange (encryptedFrame);
			}

		return [.. output];
		}
	}
