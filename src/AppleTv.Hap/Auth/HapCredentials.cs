// Copyright (c) 2026 Neil Colvin. Licensed under the MIT License.
// See LICENSE file in the repository root for full license text.

using System;

namespace AppleTvControlLibrary.Auth;

/// <summary>
/// Supported authentication type.
/// </summary>
// pyatv/auth/hap_pairing.py (AuthenticationType) — line 12-24 as of pyatv 0.18.0
public enum AuthenticationType
	{
	/// <summary>No authentication (just pass through). pyatv/auth/hap_pairing.py — line 15 as of pyatv 0.18.0</summary>
	Null,
	/// <summary>Legacy SRP based authentication. pyatv/auth/hap_pairing.py — line 18 as of pyatv 0.18.0</summary>
	Legacy,
	/// <summary>Authentication based on HAP (Home-Kit). pyatv/auth/hap_pairing.py — line 21 as of pyatv 0.18.0</summary>
	Hap,
	/// <summary>Authentication based on transient HAP (Home-Kit). pyatv/auth/hap_pairing.py — line 24 as of pyatv 0.18.0</summary>
	Transient,
	}

/// <summary>
/// Raised when authentication with a device fails.
/// </summary>
// pyatv/exceptions.py (AuthenticationError) — line 27-29 as of pyatv 0.18.0
public class AuthenticationException : Exception
	{
	/// <summary>Initializes a new instance of the <see cref="AuthenticationException"/> class.</summary>
	public AuthenticationException ()
		{
		}

	/// <summary>Initializes a new instance of the <see cref="AuthenticationException"/> class.</summary>
	/// <param name="message">The exception message.</param>
	public AuthenticationException (string message) : base (message)
		{
		}

	/// <summary>Initializes a new instance of the <see cref="AuthenticationException"/> class.</summary>
	/// <param name="message">The exception message.</param>
	/// <param name="innerException">The inner exception.</param>
	public AuthenticationException (string message, Exception innerException) : base (message, innerException)
		{
		}
	}

/// <summary>
/// Raised when credentials are invalid or malformed.
/// </summary>
// pyatv/exceptions.py (InvalidCredentialsError) — line 55-57 as of pyatv 0.18.0
public class InvalidCredentialsException : Exception
	{
	/// <summary>Initializes a new instance of the <see cref="InvalidCredentialsException"/> class.</summary>
	public InvalidCredentialsException ()
		{
		}

	/// <summary>Initializes a new instance of the <see cref="InvalidCredentialsException"/> class.</summary>
	/// <param name="message">The exception message.</param>
	public InvalidCredentialsException (string message) : base (message)
		{
		}

	/// <summary>Initializes a new instance of the <see cref="InvalidCredentialsException"/> class.</summary>
	/// <param name="message">The exception message.</param>
	/// <param name="innerException">The inner exception.</param>
	public InvalidCredentialsException (string message, Exception innerException) : base (message, innerException)
		{
		}
	}

/// <summary>
/// Identifiers and encryption keys used by HAP.
/// </summary>
// pyatv/auth/hap_pairing.py (HapCredentials) — line 30-83 as of pyatv 0.18.0
public sealed class HapCredentials : IEquatable<HapCredentials>
	{
	private static readonly byte[] _emptyBytes = [];
	private static readonly byte[] _transientMarker = System.Text.Encoding.UTF8.GetBytes ("transient");

	/// <summary>Initializes a new instance of the <see cref="HapCredentials"/> class.</summary>
	/// <param name="ltpk">Long-term public key of the peer.</param>
	/// <param name="ltsk">Long-term secret key belonging to this client.</param>
	/// <param name="atvId">Identifier of the Apple TV.</param>
	/// <param name="clientId">Identifier of this client.</param>
	// pyatv/auth/hap_pairing.py (__init__) — line 33-44 as of pyatv 0.18.0
	public HapCredentials (byte[]? ltpk = null, byte[]? ltsk = null, byte[]? atvId = null, byte[]? clientId = null)
		{
		Ltpk = ltpk ?? _emptyBytes;
		Ltsk = ltsk ?? _emptyBytes;
		AtvId = atvId ?? _emptyBytes;
		ClientId = clientId ?? _emptyBytes;
		Type = GetAuthType (Ltpk, Ltsk, AtvId, ClientId);
		}

	/// <summary>Gets the long-term public key of the peer.</summary>
	public byte[] Ltpk
		{
		get;
		}

	/// <summary>Gets the long-term secret key belonging to this client.</summary>
	public byte[] Ltsk
		{
		get;
		}

	/// <summary>Gets the identifier of the Apple TV.</summary>
	public byte[] AtvId
		{
		get;
		}

	/// <summary>Gets the identifier of this client.</summary>
	public byte[] ClientId
		{
		get;
		}

	/// <summary>Gets the authentication type represented by these credentials.</summary>
	public AuthenticationType Type
		{
		get;
		}

	/// <summary>Gets the shared "no credentials" instance. pyatv/auth/hap_pairing.py — line 135 as of pyatv 0.18.0</summary>
	public static HapCredentials NoCredentials
		{
		get;
		} = new HapCredentials ();

	/// <summary>Gets the shared "transient" credentials instance. pyatv/auth/hap_pairing.py — line 136 as of pyatv 0.18.0</summary>
	public static HapCredentials TransientCredentials
		{
		get;
		} = new HapCredentials (ltpk: _transientMarker);

	// pyatv/auth/hap_pairing.py (_get_auth_type) — line 46-63 as of pyatv 0.18.0
	private static AuthenticationType GetAuthType (byte[] ltpk, byte[] ltsk, byte[] atvId, byte[] clientId) =>
		ltpk.Length == 0 && ltsk.Length == 0 && atvId.Length == 0 && clientId.Length == 0
			? AuthenticationType.Null
			: BytesEqual (ltpk, _transientMarker)
			? AuthenticationType.Transient
			: ltpk.Length == 0 && ltsk.Length != 0 && atvId.Length == 0 && clientId.Length != 0
			? AuthenticationType.Legacy
			: ltpk.Length != 0 && ltsk.Length != 0 && atvId.Length != 0 && clientId.Length != 0
			? AuthenticationType.Hap
			: throw new InvalidCredentialsException ("invalid credentials type");

	private static bool BytesEqual (byte[] a, byte[] b)
		{
		if (a.Length != b.Length)
			{
			return false;
			}

		for (var i = 0; i < a.Length; i++)
			{
			if (a[i] != b[i])
				{
				return false;
				}
			}

		return true;
		}

	/// <inheritdoc/>
	// pyatv/auth/hap_pairing.py (__eq__) — line 65-69 as of pyatv 0.18.0
	public bool Equals (HapCredentials? other) => other is not null && ToString () == other.ToString ();

	/// <inheritdoc/>
	public override bool Equals (object? obj) => Equals (obj as HapCredentials);

	/// <inheritdoc/>
	public override int GetHashCode () => StringComparer.Ordinal.GetHashCode (ToString ());

	/// <inheritdoc/>
	// pyatv/auth/hap_pairing.py (__str__) — line 71-79 as of pyatv 0.18.0
	public override string ToString () => string.Join (
			":",
			ToHex (Ltpk),
			ToHex (Ltsk),
			ToHex (AtvId),
			ToHex (ClientId));

	private static readonly char[] _hexChars = "0123456789abcdef".ToCharArray ();

	// pyatv/auth/hap_pairing.py (binascii.hexlify) — line 73-76 as of pyatv 0.18.0
	private static string ToHex (byte[] data)
		{
		ReadOnlySpan<byte> source = data;
		Span<char> chars = source.Length <= 128 ? stackalloc char[source.Length * 2] : new char[source.Length * 2];
		for (var i = 0; i < source.Length; i++)
			{
			var b = source[i];
			chars[i * 2] = _hexChars[b >> 4];
			chars[(i * 2) + 1] = _hexChars[b & 0xF];
			}

		return chars.ToString ();
		}

	// pyatv/auth/hap_pairing.py (binascii.unhexlify) — line 145-151 as of pyatv 0.18.0
	private static byte[] FromHex (string hex)
		{
		ReadOnlySpan<char> source = hex.AsSpan ();
		if (source.Length % 2 != 0)
			{
			throw new InvalidCredentialsException ("invalid hex string: " + hex);
			}

		var result = new byte[source.Length / 2];
		for (var i = 0; i < result.Length; i++)
			{
			var hi = HexNibble (source[i * 2], hex);
			var lo = HexNibble (source[(i * 2) + 1], hex);
			result[i] = (byte)((hi << 4) | lo);
			}

		return result;
		}

	private static int HexNibble (char c, string hex) =>
		c is >= '0' and <= '9'
			? c - '0'
			: c is >= 'a' and <= 'f'
			? c - 'a' + 10
			: c is >= 'A' and <= 'F' ? c - 'A' + 10 : throw new InvalidCredentialsException ("invalid hex string: " + hex);

	/// <summary>Parse a string representation of <see cref="HapCredentials"/>.</summary>
	/// <param name="detailString">The string to parse, or <see langword="null"/>.</param>
	/// <returns>The parsed credentials.</returns>
	// pyatv/auth/hap_pairing.py (parse_credentials) — line 139-152 as of pyatv 0.18.0
	public static HapCredentials Parse (string? detailString)
		{
		if (detailString is null)
			{
			return NoCredentials;
			}

		var split = detailString.Split (':');

		// Compatibility with "legacy credentials" used by AirPlay where seed is stored
		// as LTSK and identifier as client_id (others are empty).
		if (split.Length == 2)
			{
			var clientId = FromHex (split[0]);
			var ltsk = FromHex (split[1]);
			return new HapCredentials (ltsk: ltsk, clientId: clientId);
			}

		if (split.Length == 4)
			{
			var ltpk = FromHex (split[0]);
			var ltsk = FromHex (split[1]);
			var atvId = FromHex (split[2]);
			var clientId = FromHex (split[3]);
			return new HapCredentials (ltpk, ltsk, atvId, clientId);
			}

		throw new InvalidCredentialsException ("invalid credentials: " + detailString);
		}
	}
