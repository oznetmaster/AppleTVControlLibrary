// Copyright (c) 2026 Neil Colvin. Licensed under the MIT License.
// See LICENSE file in the repository root for full license text.

using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

using AppleTvControlLibrary.Auth;
using AppleTvControlLibrary.Mrp.AirPlay.Http;
using AppleTvControlLibrary.Tlv8;

namespace AppleTvControlLibrary.Mrp.AirPlay.Auth;

/// <summary>
/// Verify a previously-paired device is allowed to establish an AirPlay 2 control connection,
/// and derive the resulting session encryption keys.
/// </summary>
/// <remarks>
/// Only the HAP pair-verify path is ported here (the path used by tvOS Apple TVs); pyatv's
/// legacy and transient AirPlay auth variants are out of scope for MRP-over-AirPlay tunnel
/// support (see auth/__init__.py — line 84-98 as of pyatv 0.18.0: HAP credentials are required
/// for Apple TV once tvOS >= 13).
/// </remarks>
// pyatv/protocols/airplay/auth/hap.py (AirPlayHapPairVerifyProcedure) — line 92-142 as of pyatv 0.18.0
public sealed class AirPlayHapPairVerifyProcedure
	{
	// pyatv/protocols/airplay/auth/hap.py (_AIRPLAY_HEADERS) — line 20-25 as of pyatv 0.18.0
	internal static readonly Dictionary<string, string> AirPlayHeaders = new ()
		{
		["User-Agent"] = "AirPlay/320.20",
		["Connection"] = "keep-alive",
		["X-Apple-HKP"] = "3",
		["Content-Type"] = "application/octet-stream",
		};

	private readonly HttpConnection _http;
	private readonly SrpAuthHandler _srp;
	private readonly HapCredentials _credentials;

	/// <summary>Initializes a new instance of the <see cref="AirPlayHapPairVerifyProcedure"/> class.</summary>
	/// <param name="http">The AirPlay control connection.</param>
	/// <param name="srp">The SRP handler used for key agreement.</param>
	/// <param name="credentials">The previously-paired credentials for the device.</param>
	// pyatv/protocols/airplay/auth/hap.py (__init__) — line 95-101 as of pyatv 0.18.0
	public AirPlayHapPairVerifyProcedure (HttpConnection http, SrpAuthHandler srp, HapCredentials credentials)
		{
		_http = http;
		_srp = srp;
		_credentials = credentials;
		}

	/// <summary>Verify the device is allowed to use AirPlay.</summary>
	/// <returns><see langword="true"/> if verification succeeded.</returns>
	// pyatv/protocols/airplay/auth/hap.py (verify_credentials) — line 104-127 as of pyatv 0.18.0
	public async Task<bool> VerifyCredentialsAsync ()
		{
		(_, byte[] publicKey) = _srp.Initialize ();

		HttpResponse resp = await SendAsync (new Dictionary<int, byte[]>
			{
				{ (int)TlvValue.SeqNo, [0x01] },
				{ (int)TlvValue.PublicKey, publicKey },
			}).ConfigureAwait (false);

		Dictionary<int, byte[]> pairingData = Tlv8.Tlv8.ReadTlv (resp.Body);
		byte[] sessionPubKey = pairingData[(int)TlvValue.PublicKey];
		byte[] encrypted = pairingData[(int)TlvValue.EncryptedData];

		byte[] encryptedData = _srp.Verify1 (_credentials, sessionPubKey, encrypted);
		_ = await SendAsync (new Dictionary<int, byte[]>
			{
				{ (int)TlvValue.SeqNo, [0x03] },
				{ (int)TlvValue.EncryptedData, encryptedData },
			}).ConfigureAwait (false);

		// pyatv/protocols/airplay/auth/hap.py — line 126 as of pyatv 0.18.0: "TODO: check status code"
		return true;
		}

	// pyatv/protocols/airplay/auth/hap.py (_send) — line 129-133 as of pyatv 0.18.0
	private async Task<HttpResponse> SendAsync (Dictionary<int, byte[]> data)
		{
		byte[] body = Tlv8.Tlv8.WriteTlv (data);
		return await _http.PostAsync ("/pair-verify", AirPlayHeaders, body).ConfigureAwait (false);
		}

	/// <summary>Return the derived output/input encryption keys for the given salt/info strings.</summary>
	/// <param name="salt">The HKDF salt string.</param>
	/// <param name="outputInfo">The HKDF info string for the output key.</param>
	/// <param name="inputInfo">The HKDF info string for the input key.</param>
	// pyatv/protocols/airplay/auth/hap.py (encryption_keys) — line 135-141 as of pyatv 0.18.0
	public (byte[] OutputKey, byte[] InputKey) EncryptionKeys (string salt, string outputInfo, string inputInfo) =>
		_srp.Verify2 (salt, outputInfo, inputInfo);
	}

/// <summary>
/// Authenticate (pair-setup) a device for AirPlay playback. This is the procedure that must be
/// run once, before any AirPlay pair-verify or MRP-over-AirPlay tunnel can be used, to obtain
/// AirPlay-specific <see cref="HapCredentials"/> (distinct from, and not interchangeable with,
/// MRP's own pairing/credentials, since they are separate services on separate ports).
/// </summary>
// pyatv/protocols/airplay/auth/hap.py (AirPlayHapPairSetupProcedure) — line 33-84 as of pyatv 0.18.0
public sealed class AirPlayHapPairSetupProcedure
	{
	private readonly HttpConnection _http;
	private readonly SrpAuthHandler _srp;
	private byte[]? _atvSalt;
	private byte[]? _atvPubKey;

	/// <summary>Initializes a new instance of the <see cref="AirPlayHapPairSetupProcedure"/> class.</summary>
	/// <param name="http">The AirPlay control connection.</param>
	/// <param name="srp">The SRP handler used for key agreement.</param>
	// pyatv/protocols/airplay/auth/hap.py (__init__) — line 36-41 as of pyatv 0.18.0
	public AirPlayHapPairSetupProcedure (HttpConnection http, SrpAuthHandler srp)
		{
		_http = http;
		_srp = srp;
		}

	/// <summary>Start the pairing process. Causes the device to display an on-screen PIN.</summary>
	// pyatv/protocols/airplay/auth/hap.py (start_pairing) — line 43-58 as of pyatv 0.18.0
	public async Task StartPairingAsync ()
		{
		_ = _srp.Initialize ();

		_ = await _http.PostAsync ("/pair-pin-start", AirPlayHapPairVerifyProcedure.AirPlayHeaders).ConfigureAwait (false);

		byte[] body = Tlv8.Tlv8.WriteTlv (new Dictionary<int, byte[]>
			{
				{ (int)TlvValue.Method, [0x00] },
				{ (int)TlvValue.SeqNo, [0x01] },
			});

		HttpResponse resp = await _http.PostAsync ("/pair-setup", AirPlayHapPairVerifyProcedure.AirPlayHeaders, body).ConfigureAwait (false);
		Dictionary<int, byte[]> pairingData = Tlv8.Tlv8.ReadTlv (resp.Body);

		_atvSalt = pairingData[(int)TlvValue.Salt];
		_atvPubKey = pairingData[(int)TlvValue.PublicKey];
		}

	/// <summary>Finish the pairing process using the PIN shown on the device.</summary>
	/// <param name="pinCode">The PIN code entered by the user.</param>
	/// <param name="displayName">The client display name reported to the device, or <see langword="null"/>.</param>
	/// <returns>The resulting credentials for the paired device.</returns>
	// pyatv/protocols/airplay/auth/hap.py (finish_pairing) — line 60-84 as of pyatv 0.18.0
	public async Task<HapCredentials> FinishPairingAsync (int pinCode, string? displayName = null)
		{
		if (_atvSalt is null || _atvPubKey is null)
			{
			throw new InvalidOperationException ("StartPairingAsync must be called first");
			}

		_srp.Step1 (pinCode);

		(byte[] pubKey, byte[] proof) = _srp.Step2 (_atvPubKey, _atvSalt);
		byte[] body = Tlv8.Tlv8.WriteTlv (new Dictionary<int, byte[]>
			{
				{ (int)TlvValue.SeqNo, [0x03] },
				{ (int)TlvValue.PublicKey, pubKey },
				{ (int)TlvValue.Proof, proof },
			});

		_ = await _http.PostAsync ("/pair-setup", AirPlayHapPairVerifyProcedure.AirPlayHeaders, body).ConfigureAwait (false);

		body = Tlv8.Tlv8.WriteTlv (new Dictionary<int, byte[]>
			{
				{ (int)TlvValue.SeqNo, [0x05] },
				{ (int)TlvValue.EncryptedData, _srp.Step3 (displayName) },
			});

		HttpResponse resp = await _http.PostAsync ("/pair-setup", AirPlayHapPairVerifyProcedure.AirPlayHeaders, body).ConfigureAwait (false);
		Dictionary<int, byte[]> pairingData = Tlv8.Tlv8.ReadTlv (resp.Body);

		byte[] encryptedData = pairingData[(int)TlvValue.EncryptedData];
		return _srp.Step4 (encryptedData);
		}
	}

/// <summary>
/// Runs Pair-Verify on an AirPlay control connection and enables HAP session encryption on it.
/// </summary>
// pyatv/protocols/airplay/auth/__init__.py (verify_connection) — line 98-111 as of pyatv 0.18.0
public static class AirPlayConnectionAuth
	{
	// pyatv/protocols/airplay/auth/__init__.py (CONTROL_SALT/CONTROL_OUTPUT_INFO/CONTROL_INPUT_INFO) — line 33-35 as of pyatv 0.18.0
	private const string ControlSalt = "Control-Salt";
	private const string ControlOutputInfo = "Control-Write-Encryption-Key";
	private const string ControlInputInfo = "Control-Read-Encryption-Key";

	/// <summary>Perform Pair-Verify on a connection and enable HAP session encryption if credentials are HAP-typed.</summary>
	/// <param name="credentials">The previously-paired credentials for the device.</param>
	/// <param name="connection">The AirPlay control connection to verify and, on success, encrypt.</param>
	/// <returns>The verify procedure, so callers can derive further channel encryption keys from it.</returns>
	// pyatv/protocols/airplay/auth/__init__.py (verify_connection) — line 98-111 as of pyatv 0.18.0
	public static async Task<AirPlayHapPairVerifyProcedure> VerifyConnectionAsync (HapCredentials credentials, HttpConnection connection)
		{
		var srp = new SrpAuthHandler ();
		var verifier = new AirPlayHapPairVerifyProcedure (connection, srp, credentials);
		bool hasEncryptionKeys = await verifier.VerifyCredentialsAsync ().ConfigureAwait (false);

		if (hasEncryptionKeys)
			{
			(byte[] outputKey, byte[] inputKey) = verifier.EncryptionKeys (ControlSalt, ControlOutputInfo, ControlInputInfo);

			var session = new HapSession ();
			session.Enable (outputKey, inputKey);
			connection.ReceiveProcessor = session.Decrypt;
			connection.SendProcessor = session.Encrypt;
			}

		return verifier;
		}
	}
