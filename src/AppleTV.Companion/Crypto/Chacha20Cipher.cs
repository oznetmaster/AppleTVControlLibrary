using System;

using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Modes;
using Org.BouncyCastle.Crypto.Parameters;

namespace AppleTvControlLibrary.Crypto;

/// <summary>
/// CHACHA20 encryption/decryption layer.
/// </summary>
// pyatv/support/chacha20.py:9-73 (NONCE_LENGTH, Chacha20Cipher)
public class Chacha20Cipher
	{
	// pyatv/support/chacha20.py:9
	private const int NONCE_LENGTH = 12;

	// The Poly1305 authentication tag is always 16 bytes (128 bits).
	private const int MAC_SIZE_BITS = 128;

	private readonly KeyParameter _outKey;
	private readonly KeyParameter _inKey;
	private readonly int _nonceLength;

	private long _outCounter;
	private long _inCounter;

	/// <summary>Initializes a new instance of the <see cref="Chacha20Cipher"/> class.</summary>
	/// <param name="outKey">The key used to encrypt outgoing data.</param>
	/// <param name="inKey">The key used to decrypt incoming data.</param>
	/// <param name="nonceLength">The length, in bytes, of the counter portion of the nonce.</param>
	// pyatv/support/chacha20.py:15-21 (__init__)
	public Chacha20Cipher (byte[] outKey, byte[] inKey, int nonceLength = 8)
		{
		_outKey = new KeyParameter (outKey);
		_inKey = new KeyParameter (inKey);
		_nonceLength = nonceLength;
		_outCounter = 0;
		_inCounter = 0;
		}

	/// <summary>Gets the next encrypt nonce.</summary>
	/// <remarks>
	/// This is the nonce that will be used by <see cref="Encrypt"/> in the _next_ call if no
	/// custom nonce is specified.
	/// </remarks>
	// pyatv/support/chacha20.py:23-34 (out_nonce)
	public virtual byte[] OutNonce
		{
		get
			{
			byte[] nonce = CounterToBytes (_outCounter, _nonceLength);
			return _nonceLength != NONCE_LENGTH ? PadNonce (nonce) : nonce;
			}
		}

	/// <summary>Gets the next decrypt nonce.</summary>
	/// <remarks>
	/// This is the nonce that will be used by <see cref="Decrypt"/> in the _next_ call if no
	/// custom nonce is specified.
	/// </remarks>
	// pyatv/support/chacha20.py:36-47 (in_nonce)
	public virtual byte[] InNonce
		{
		get
			{
			byte[] nonce = CounterToBytes (_inCounter, _nonceLength);
			return _nonceLength != NONCE_LENGTH ? PadNonce (nonce) : nonce;
			}
		}

	/// <summary>Encrypt data with counter or specified nonce.</summary>
	/// <param name="data">The plaintext to encrypt.</param>
	/// <param name="nonce">An optional explicit nonce. If not specified, the counter-derived nonce is used.</param>
	/// <param name="aad">Optional additional authenticated data.</param>
	/// <returns>The ciphertext with the 16-byte Poly1305 tag appended.</returns>
	// pyatv/support/chacha20.py:53-62 (encrypt)
	public byte[] Encrypt (byte[] data, byte[]? nonce = null, byte[]? aad = null)
		{
		if (nonce is null)
			{
			nonce = OutNonce;
			_outCounter += 1;
			}
		else if (nonce.Length < NONCE_LENGTH)
			{
			nonce = PadNonce (nonce);
			}

		var cipher = new ChaCha20Poly1305 ();
		cipher.Init (true, new AeadParameters (_outKey, MAC_SIZE_BITS, nonce, aad));

		var output = new byte[cipher.GetOutputSize (data.Length)];
		int len = cipher.ProcessBytes (data, 0, data.Length, output, 0);
		len += cipher.DoFinal (output, len);

		if (len != output.Length)
			{
			Array.Resize (ref output, len);
			}

		return output;
		}

	/// <summary>Decrypt data with counter or specified nonce.</summary>
	/// <param name="data">The ciphertext, including the 16-byte Poly1305 tag, to decrypt.</param>
	/// <param name="nonce">An optional explicit nonce. If not specified, the counter-derived nonce is used.</param>
	/// <param name="aad">Optional additional authenticated data.</param>
	/// <returns>The decrypted plaintext.</returns>
	// pyatv/support/chacha20.py:64-73 (decrypt)
	public byte[] Decrypt (byte[] data, byte[]? nonce = null, byte[]? aad = null)
		{
		if (nonce is null)
			{
			nonce = InNonce;
			_inCounter += 1;
			}
		else if (nonce.Length < NONCE_LENGTH)
			{
			nonce = PadNonce (nonce);
			}

		var cipher = new ChaCha20Poly1305 ();
		cipher.Init (false, new AeadParameters (_inKey, MAC_SIZE_BITS, nonce, aad));

		var output = new byte[cipher.GetOutputSize (data.Length)];
		int len = cipher.ProcessBytes (data, 0, data.Length, output, 0);
		len += cipher.DoFinal (output, len);

		if (len != output.Length)
			{
			Array.Resize (ref output, len);
			}

		return output;
		}

	/// <summary>Pad nonce to 12 bytes.</summary>
	// pyatv/support/chacha20.py:49-51 (_pad_nonce)
	private static byte[] PadNonce (byte[] nonce)
		{
		var padded = new byte[NONCE_LENGTH];
		Array.Copy (nonce, 0, padded, NONCE_LENGTH - nonce.Length, nonce.Length);
		return padded;
		}

	private static byte[] CounterToBytes (long counter, int length)
		{
		var result = new byte[length];
		for (int i = 0; i < length; i++)
			{
			result[i] = (byte)(counter >> (8 * i));
			}

		return result;
		}
	}

/// <summary>
/// CHACHA20 encryption/decryption layer with an 8 byte counter.
/// </summary>
/// <remarks>
/// The first 4 bytes are always 0, followed by 8 bytes of counter for a total of 12 bytes.
/// </remarks>
// pyatv/support/chacha20.py:79-106 (Chacha20Cipher8byteNonce)
public sealed class Chacha20Cipher8ByteNonce : Chacha20Cipher
	{
	/// <summary>Initializes a new instance of the <see cref="Chacha20Cipher8ByteNonce"/> class.</summary>
	/// <param name="outKey">The key used to encrypt outgoing data.</param>
	/// <param name="inKey">The key used to decrypt incoming data.</param>
	// pyatv/support/chacha20.py:86-88 (__init__)
	public Chacha20Cipher8ByteNonce (byte[] outKey, byte[] inKey)
		: base (outKey, inKey, nonceLength: 8)
		{
		}

	// pyatv packs this nonce as 4 zero bytes followed by an 8-byte little-endian
	// counter (Struct("<LQ").pack(0, counter)), for a total of 12 bytes
	// (pyatv/support/chacha20.py:76, 90-106). The base class's OutNonce/InNonce
	// already produce identical bytes for an 8-byte nonce_length: a little-endian
	// counter left-padded with zeros to 12 bytes. No override is required.
	}