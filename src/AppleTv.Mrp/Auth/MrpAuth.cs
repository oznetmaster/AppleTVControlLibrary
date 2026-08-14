// Copyright (c) 2026 Neil Colvin. Licensed under the MIT License.
// See LICENSE file in the repository root for full license text.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using AppleTvControlLibrary.Auth;
using AppleTvControlLibrary.Mrp.Protobuf;
using AppleTvControlLibrary.Tlv8;

namespace AppleTvControlLibrary.Mrp.Auth;

/// <summary>
/// Sends a CRYPTO_PAIRING_MESSAGE and returns the corresponding response message.
/// </summary>
/// <param name="message">The message to send.</param>
/// <returns>The response message received from the device.</returns>
/// <remarks>
/// pyatv correlates pairing exchanges through <c>protocol.send_and_receive(msg, generate_identifier=False)</c>
/// (pyatv/protocols/mrp/auth.py — lines 41, 58, 73, 82, 103, 116 as of pyatv 0.18.0); the caller of
/// <see cref="MrpPairSetupProcedure"/>/<see cref="MrpPairVerifyProcedure"/> is expected to provide an
/// equivalent request/response correlation over the underlying <c>MrpConnection</c>/<c>MrpProtocol</c>.
/// </remarks>
public delegate Task<ProtocolMessage> MrpSendAndReceive (ProtocolMessage message);

/// <summary>
/// Perform MRP pairing and return new credentials.
/// </summary>
/// <remarks>
/// MRP pair-setup is a thin wrapper around the shared HAP SRP routines in <see cref="SrpAuthHandler"/>;
/// only the message shapes (CRYPTO_PAIRING_MESSAGE with a TLV8 <c>pairingData</c> field) differ from
/// Companion Link.
/// </remarks>
// pyatv/protocols/mrp/auth.py (MrpPairSetupProcedure) — line 26-77 as of pyatv 0.18.0
public sealed class MrpPairSetupProcedure
	{
	private readonly MrpSendAndReceive _sendAndReceive;
	private readonly SrpAuthHandler _srp;

	private byte[]? _atvSalt;
	private byte[]? _atvPubKey;

	/// <summary>Initializes a new instance of the <see cref="MrpPairSetupProcedure"/> class.</summary>
	/// <param name="sendAndReceive">Callback used to send a message and await its response.</param>
	/// <param name="srp">The SRP handler used to perform the pairing crypto.</param>
	// pyatv/protocols/mrp/auth.py (MrpPairSetupProcedure.__init__) — line 29-33 as of pyatv 0.18.0
	public MrpPairSetupProcedure (MrpSendAndReceive sendAndReceive, SrpAuthHandler srp)
		{
		_sendAndReceive = sendAndReceive;
		_srp = srp;
		}

	/// <summary>Start the pairing procedure (M1/M2). Causes the device to display an on-screen PIN.</summary>
	// pyatv/protocols/mrp/auth.py (start_pairing) — line 35-46 as of pyatv 0.18.0
	public async Task StartPairingAsync ()
		{
		_srp.Initialize ();

		var m1 = MrpMessages.CryptoPairing (
			new Dictionary<int, byte[]>
				{
				{ (int)TlvValue.Method, new byte[] { 0 } },
				{ (int)TlvValue.SeqNo, new byte[] { 1 } },
				},
			isPairing: true);

		ProtocolMessage resp = await _sendAndReceive (m1).ConfigureAwait (false);

		Dictionary<int, byte[]> pairingData = MrpMessages.GetPairingData (resp);
		_atvSalt = pairingData[(int)TlvValue.Salt];
		_atvPubKey = pairingData[(int)TlvValue.PublicKey];
		}

	/// <summary>Finish the pairing process (M3-M6) using the PIN shown on the device.</summary>
	/// <param name="pin">The PIN code entered by the user.</param>
	/// <returns>The resulting credentials for the paired device.</returns>
	// pyatv/protocols/mrp/auth.py (finish_pairing) — line 48-77 as of pyatv 0.18.0
	public async Task<HapCredentials> FinishPairingAsync (int pin)
		{
		if (_atvSalt is null || _atvPubKey is null)
			{
			throw new InvalidOperationException ("StartPairingAsync must be called first");
			}

		_srp.Step1 (pin);

		(byte[] pubKey, byte[] proof) = _srp.Step2 (_atvPubKey, _atvSalt);

		var m3 = MrpMessages.CryptoPairing (new Dictionary<int, byte[]>
			{
			{ (int)TlvValue.SeqNo, new byte[] { 3 } },
			{ (int)TlvValue.PublicKey, pubKey },
			{ (int)TlvValue.Proof, proof },
			});

		ProtocolMessage resp = await _sendAndReceive (m3).ConfigureAwait (false);
		Dictionary<int, byte[]> pairingData = MrpMessages.GetPairingData (resp);
		byte[] atvProof = pairingData[(int)TlvValue.Proof];
		_ = atvProof;

		byte[] encryptedData = _srp.Step3 ();
		var m5 = MrpMessages.CryptoPairing (new Dictionary<int, byte[]>
			{
			{ (int)TlvValue.SeqNo, new byte[] { 5 } },
			{ (int)TlvValue.EncryptedData, encryptedData },
			});

		resp = await _sendAndReceive (m5).ConfigureAwait (false);
		pairingData = MrpMessages.GetPairingData (resp);
		encryptedData = pairingData[(int)TlvValue.EncryptedData];

		return _srp.Step4 (encryptedData);
		}
	}

/// <summary>
/// Verify credentials and derive new encryption keys for an MRP session.
/// </summary>
// pyatv/protocols/mrp/auth.py (MrpPairVerifyProcedure) — line 80-121 as of pyatv 0.18.0
public sealed class MrpPairVerifyProcedure
	{
	private readonly MrpSendAndReceive _sendAndReceive;
	private readonly SrpAuthHandler _srp;
	private readonly HapCredentials _credentials;

	/// <summary>Initializes a new instance of the <see cref="MrpPairVerifyProcedure"/> class.</summary>
	/// <param name="sendAndReceive">Callback used to send a message and await its response.</param>
	/// <param name="srp">The SRP handler used to perform the verify crypto.</param>
	/// <param name="credentials">The previously paired credentials to verify.</param>
	// pyatv/protocols/mrp/auth.py (MrpPairVerifyProcedure.__init__) — line 83-87 as of pyatv 0.18.0
	public MrpPairVerifyProcedure (MrpSendAndReceive sendAndReceive, SrpAuthHandler srp, HapCredentials credentials)
		{
		_sendAndReceive = sendAndReceive;
		_srp = srp;
		_credentials = credentials;
		}

	/// <summary>Verify credentials with the device.</summary>
	/// <returns><see langword="true"/> if verification succeeded.</returns>
	// pyatv/protocols/mrp/auth.py (verify_credentials) — line 89-108 as of pyatv 0.18.0
	public async Task<bool> VerifyCredentialsAsync ()
		{
		(_, byte[] publicKey) = _srp.Initialize ();

		var m1 = MrpMessages.CryptoPairing (new Dictionary<int, byte[]>
			{
			{ (int)TlvValue.SeqNo, new byte[] { 1 } },
			{ (int)TlvValue.PublicKey, publicKey },
			});

		ProtocolMessage resp = await _sendAndReceive (m1).ConfigureAwait (false);
		Dictionary<int, byte[]> pairingData = MrpMessages.GetPairingData (resp);
		byte[] sessionPubKey = pairingData[(int)TlvValue.PublicKey];
		byte[] encrypted = pairingData[(int)TlvValue.EncryptedData];

		byte[] encryptedData = _srp.Verify1 (_credentials, sessionPubKey, encrypted);

		var m3 = MrpMessages.CryptoPairing (new Dictionary<int, byte[]>
			{
			{ (int)TlvValue.SeqNo, new byte[] { 3 } },
			{ (int)TlvValue.EncryptedData, encryptedData },
			});

		await _sendAndReceive (m3).ConfigureAwait (false);

		// TODO: check status code — pyatv/protocols/mrp/auth.py line 118 as of pyatv 0.18.0
		return true;
		}

	/// <summary>Return derived encryption keys (output, input) for the encrypted session.</summary>
	/// <param name="salt">The HKDF salt string.</param>
	/// <param name="outputInfo">The HKDF info string used to derive the output key.</param>
	/// <param name="inputInfo">The HKDF info string used to derive the input key.</param>
	/// <returns>A tuple of (output key, input key).</returns>
	// pyatv/protocols/mrp/auth.py (encryption_keys) — line 110-114 as of pyatv 0.18.0
	public (byte[] OutputKey, byte[] InputKey) EncryptionKeys (string salt, string outputInfo, string inputInfo)
		{
		return _srp.Verify2 (salt, outputInfo, inputInfo);
		}
	}
