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
	private static readonly byte[] EmptyBytes = Array.Empty<byte> ();
	private static readonly byte[] TransientMarker = System.Text.Encoding.UTF8.GetBytes ("transient");

	/// <summary>Initializes a new instance of the <see cref="HapCredentials"/> class.</summary>
	/// <param name="ltpk">Long-term public key of the peer.</param>
	/// <param name="ltsk">Long-term secret key belonging to this client.</param>
	/// <param name="atvId">Identifier of the Apple TV.</param>
	/// <param name="clientId">Identifier of this client.</param>
	// pyatv/auth/hap_pairing.py (__init__) — line 33-44 as of pyatv 0.18.0
	public HapCredentials (byte[]? ltpk = null, byte[]? ltsk = null, byte[]? atvId = null, byte[]? clientId = null)
		{
		this.Ltpk = ltpk ?? EmptyBytes;
		this.Ltsk = ltsk ?? EmptyBytes;
		this.AtvId = atvId ?? EmptyBytes;
		this.ClientId = clientId ?? EmptyBytes;
		this.Type = GetAuthType (this.Ltpk, this.Ltsk, this.AtvId, this.ClientId);
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
		} = new HapCredentials (ltpk: TransientMarker);

	// pyatv/auth/hap_pairing.py (_get_auth_type) — line 46-63 as of pyatv 0.18.0
	private static AuthenticationType GetAuthType (byte[] ltpk, byte[] ltsk, byte[] atvId, byte[] clientId)
		{
		if (ltpk.Length == 0 && ltsk.Length == 0 && atvId.Length == 0 && clientId.Length == 0)
			{
			return AuthenticationType.Null;
			}

		if (BytesEqual (ltpk, TransientMarker))
			{
			return AuthenticationType.Transient;
			}

		if (ltpk.Length == 0 && ltsk.Length != 0 && atvId.Length == 0 && clientId.Length != 0)
			{
			return AuthenticationType.Legacy;
			}

		if (ltpk.Length != 0 && ltsk.Length != 0 && atvId.Length != 0 && clientId.Length != 0)
			{
			return AuthenticationType.Hap;
			}

		throw new InvalidCredentialsException ("invalid credentials type");
		}

	private static bool BytesEqual (byte[] a, byte[] b)
		{
		if (a.Length != b.Length)
			{
			return false;
			}

		for (int i = 0; i < a.Length; i++)
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
	public bool Equals (HapCredentials? other)
		{
		return other is not null && ToString () == other.ToString ();
		}

	/// <inheritdoc/>
	public override bool Equals (object? obj)
		{
		return Equals (obj as HapCredentials);
		}

	/// <inheritdoc/>
	public override int GetHashCode ()
		{
		return StringComparer.Ordinal.GetHashCode (ToString ());
		}

	/// <inheritdoc/>
	// pyatv/auth/hap_pairing.py (__str__) — line 71-79 as of pyatv 0.18.0
	public override string ToString ()
		{
		return string.Join (
			":",
			ToHex (this.Ltpk),
			ToHex (this.Ltsk),
			ToHex (this.AtvId),
			ToHex (this.ClientId));
		}

	private static readonly char[] HexChars = "0123456789abcdef".ToCharArray ();

	// pyatv/auth/hap_pairing.py (binascii.hexlify) — line 73-76 as of pyatv 0.18.0
	private static string ToHex (byte[] data)
		{
		var chars = new char[data.Length * 2];
		for (int i = 0; i < data.Length; i++)
			{
			byte b = data[i];
			chars[i * 2] = HexChars[b >> 4];
			chars[(i * 2) + 1] = HexChars[b & 0xF];
			}

		return new string (chars);
		}

	// pyatv/auth/hap_pairing.py (binascii.unhexlify) — line 145-151 as of pyatv 0.18.0
	private static byte[] FromHex (string hex)
		{
		if (hex.Length % 2 != 0)
			{
			throw new InvalidCredentialsException ("invalid hex string: " + hex);
			}

		var result = new byte[hex.Length / 2];
		for (int i = 0; i < result.Length; i++)
			{
			result[i] = Convert.ToByte (hex.Substring (i * 2, 2), 16);
			}

		return result;
		}

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

		string[] split = detailString.Split (':');

		// Compatibility with "legacy credentials" used by AirPlay where seed is stored
		// as LTSK and identifier as client_id (others are empty).
		if (split.Length == 2)
			{
			byte[] clientId = FromHex (split[0]);
			byte[] ltsk = FromHex (split[1]);
			return new HapCredentials (ltsk: ltsk, clientId: clientId);
			}

		if (split.Length == 4)
			{
			byte[] ltpk = FromHex (split[0]);
			byte[] ltsk = FromHex (split[1]);
			byte[] atvId = FromHex (split[2]);
			byte[] clientId = FromHex (split[3]);
			return new HapCredentials (ltpk, ltsk, atvId, clientId);
			}

		throw new InvalidCredentialsException ("invalid credentials: " + detailString);
		}
	}
