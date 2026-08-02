using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

using AppleTvControlLibrary.Auth;
using AppleTvControlLibrary.Connection;
using AppleTvControlLibrary.Discovery.Companion;
using AppleTvControlLibrary.Protocol;
using AppleTvControlLibrary.Remote.Wpf.Storage;
using AppleTvControlLibrary.Remote.Wpf.Transport;
using AppleTvControlLibrary.Tlv8;

namespace AppleTvControlLibrary.Remote.Wpf.Services;

/// <summary>
/// Orchestrates discovery, pairing, connection and HID command dispatch for a single
/// Companion Link device, gluing together <see cref="MulticastCompanionDiscovery"/>,
/// <see cref="SrpAuthHandler"/>, <see cref="CompanionProtocol"/>/<see cref="CompanionApi"/>,
/// <see cref="TcpCompanionTransport"/> and <see cref="CredentialStore"/>.
/// </summary>
public sealed class AppleTvDeviceManager : IDisposable
	{
	private readonly ICompanionDiscovery _discovery;
	private readonly CredentialStore _credentialStore;

	private TcpCompanionTransport? _transport;
	private CompanionApi? _api;

	/// <summary>Initializes a new instance of the <see cref="AppleTvDeviceManager"/> class.</summary>
	/// <param name="discovery">The discovery implementation to use for scanning.</param>
	/// <param name="credentialStore">The credential store used to persist/load pairings.</param>
	public AppleTvDeviceManager (ICompanionDiscovery? discovery = null, CredentialStore? credentialStore = null)
		{
		this._discovery = discovery ?? new MulticastCompanionDiscovery ();
		this._credentialStore = credentialStore ?? new CredentialStore ();
		}

	/// <summary>Gets the connected <see cref="CompanionApi"/> instance, if connected.</summary>
	public CompanionApi? Api => this._api;

	/// <summary>Gets a value indicating whether a device is currently connected.</summary>
	public bool IsConnected => this._api is not null;

	/// <summary>Scans the network for Companion Link devices.</summary>
	/// <param name="timeout">How long to scan for.</param>
	/// <param name="cancellationToken">A token to cancel the scan.</param>
	public Task<IReadOnlyList<CompanionDiscoveryResult>> ScanAsync (TimeSpan timeout, CancellationToken cancellationToken = default)
		{
		return this._discovery.ScanAsync (timeout, cancellationToken);
		}

	/// <summary>Loads previously saved credentials for the given device, if any.</summary>
	/// <param name="uniqueId">The device's stable unique id.</param>
	public StoredDevice? LoadStoredDevice (string uniqueId) => this._credentialStore.Load (uniqueId);

	/// <summary>
	/// Pairs with a device using pair-setup (SRP M1-M6, per the porting brief's WP4 message
	/// shapes), then persists the resulting credentials and stable identifier.
	/// </summary>
	/// <param name="device">The device to pair with.</param>
	/// <param name="pin">The PIN code displayed on the TV.</param>
	/// <returns>The stored device record, ready to be used for <see cref="ConnectAsync"/>.</returns>
	// pyatv/protocols/companion/auth.py:37-100 (CompanionPairSetupProcedure)
	public async Task<StoredDevice> PairAsync (CompanionDiscoveryResult device, int pin)
		{
		if (device.Address is null)
			{
			throw new InvalidOperationException ("Device has no resolved address");
			}

		return await Task.Run (() =>
			{
			CompanionConnection connection = new CompanionConnection ();
			CompanionProtocol protocol = new CompanionProtocol (connection, new SrpAuthHandler ());
			using TcpCompanionTransport transport = TcpCompanionTransport.Connect (
				device.Address.ToString (), device.Port, connection, protocol);

			SrpAuthHandler srp = new SrpAuthHandler ();
			srp.Initialize ();

			// M1
			byte[] m1 = Tlv8.Tlv8.WriteTlv (new Dictionary<int, byte[]>
				{
				{ (int)TlvValue.Method, new byte[] { 0 } },
				{ (int)TlvValue.SeqNo, new byte[] { 1 } },
				});
			Dictionary<object, object?> m2Response = protocol.ExchangeAuth (FrameType.PS_Start, new Dictionary<string, object?> { ["_pd"] = m1, ["_pwTy"] = 1 });
			byte[] m2 = (byte[])m2Response["_pd"]!;
			Dictionary<int, byte[]> m2Tlv = Tlv8.Tlv8.ReadTlv (m2);
			byte[] atvSalt = m2Tlv[(int)TlvValue.Salt];
			byte[] atvPubKey = m2Tlv[(int)TlvValue.PublicKey];

			// M3
			srp.Step1 (pin);
			(byte[] clientPubKey, byte[] clientProof) = srp.Step2 (atvPubKey, atvSalt);
			byte[] m3 = Tlv8.Tlv8.WriteTlv (new Dictionary<int, byte[]>
				{
				{ (int)TlvValue.SeqNo, new byte[] { 3 } },
				{ (int)TlvValue.PublicKey, clientPubKey },
				{ (int)TlvValue.Proof, clientProof },
				});
			Dictionary<object, object?> m4Response = protocol.ExchangeAuth (FrameType.PS_Next, new Dictionary<string, object?> { ["_pd"] = m3, ["_pwTy"] = 1 });
			byte[] m4 = (byte[])m4Response["_pd"]!;
			Dictionary<int, byte[]> m4Tlv = Tlv8.Tlv8.ReadTlv (m4);
			if (m4Tlv.ContainsKey ((int)TlvValue.Error))
				{
				throw new AuthenticationException ("Pairing failed at M3/M4 - incorrect PIN?");
				}

			// M5
			byte[] m5EncryptedData = srp.Step3 (name: Environment.MachineName);
			byte[] m5 = Tlv8.Tlv8.WriteTlv (new Dictionary<int, byte[]>
				{
				{ (int)TlvValue.SeqNo, new byte[] { 5 } },
				{ (int)TlvValue.EncryptedData, m5EncryptedData },
				});
			Dictionary<object, object?> m6Response = protocol.ExchangeAuth (FrameType.PS_Next, new Dictionary<string, object?> { ["_pd"] = m5, ["_pwTy"] = 1 });
			byte[] m6 = (byte[])m6Response["_pd"]!;
			Dictionary<int, byte[]> m6Tlv = Tlv8.Tlv8.ReadTlv (m6);
			byte[] m6EncryptedData = m6Tlv[(int)TlvValue.EncryptedData];

			HapCredentials credentials = srp.Step4 (m6EncryptedData);

			StoredDevice stored = new StoredDevice
				{
				UniqueId = device.UniqueId ?? Convert.ToHexString (credentials.AtvId).ToLowerInvariant (),
				Name = device.Name,
				Address = device.Address.ToString (),
				Port = device.Port,
				StableIdentifier = GenerateStableIdentifier (),
				};
			stored.SetCredentials (credentials);

			this._credentialStore.Save (stored);
			return stored;
			}).ConfigureAwait (false);
		}

	/// <summary>
	/// Connects to a previously paired device: performs pair-verify, enables encryption, and
	/// runs the <see cref="CompanionApi.Connect"/> session bring-up sequence.
	/// </summary>
	/// <param name="stored">The stored device record from a previous <see cref="PairAsync"/> call.</param>
	// pyatv/protocols/companion/auth.py:120-158 (CompanionPairVerifyProcedure)
	public async Task ConnectAsync (StoredDevice stored)
		{
		await Task.Run (() =>
			{
			CompanionConnection connection = new CompanionConnection ();
			CompanionProtocol protocol = new CompanionProtocol (connection, new SrpAuthHandler ());
			this._transport = TcpCompanionTransport.Connect (stored.Address, stored.Port, connection, protocol);

			HapCredentials credentials = stored.ToCredentials ();

			SrpAuthHandler srp = new SrpAuthHandler ();
			(byte[] _, byte[] verifyPubKey) = srp.Initialize ();

			byte[] pv1 = Tlv8.Tlv8.WriteTlv (new Dictionary<int, byte[]>
				{
				{ (int)TlvValue.SeqNo, new byte[] { 1 } },
				{ (int)TlvValue.PublicKey, verifyPubKey },
				});
			Dictionary<object, object?> pv2Response = protocol.ExchangeAuth (FrameType.PV_Start, new Dictionary<string, object?> { ["_pd"] = pv1, ["_auTy"] = 4 });
			byte[] pv2 = (byte[])pv2Response["_pd"]!;
			Dictionary<int, byte[]> pv2Tlv = Tlv8.Tlv8.ReadTlv (pv2);
			byte[] serverVerifyPubKey = pv2Tlv[(int)TlvValue.PublicKey];
			byte[] serverEncryptedData = pv2Tlv[(int)TlvValue.EncryptedData];

			byte[] pv3EncryptedData = srp.Verify1 (credentials, serverVerifyPubKey, serverEncryptedData);
			byte[] pv3 = Tlv8.Tlv8.WriteTlv (new Dictionary<int, byte[]>
				{
				{ (int)TlvValue.SeqNo, new byte[] { 3 } },
				{ (int)TlvValue.EncryptedData, pv3EncryptedData },
				});
			// pyatv/protocols/companion/auth.py:145-158: M3 carries no "_auTy", unlike M1.
			Dictionary<object, object?> pv4Response = protocol.ExchangeAuth (FrameType.PV_Next, new Dictionary<string, object?> { ["_pd"] = pv3 });
			byte[] pv4 = (byte[])pv4Response["_pd"]!;
			Dictionary<int, byte[]> pv4Tlv = Tlv8.Tlv8.ReadTlv (pv4);
			if (pv4Tlv.ContainsKey ((int)TlvValue.Error))
				{
				throw new AuthenticationException ("Pair-verify failed");
				}

			(byte[] outputKey, byte[] inputKey) = srp.Verify2 (
				CompanionProtocol.SRP_SALT, CompanionProtocol.SRP_OUTPUT_INFO, CompanionProtocol.SRP_INPUT_INFO);
			connection.EnableEncryption (outputKey, inputKey);

			CompanionApi api = new CompanionApi (
				protocol,
				credentials,
				stableIdentifier: stored.StableIdentifier,
				deviceId: Convert.ToHexString (credentials.AtvId).ToLowerInvariant (),
				model: "AppleTV",
				name: stored.Name);
			api.Connect ();

			this._api = api;
			}).ConfigureAwait (false);
		}

	/// <summary>Sends a HID button command to the connected device.</summary>
	/// <param name="command">The button to send.</param>
	public void SendHidCommand (HidCommand command)
		{
		if (this._api is null)
			{
			throw new InvalidOperationException ("Not connected");
			}

		this._api.SendHidCommand (down: true, command: command);
		this._api.SendHidCommand (down: false, command: command);
		}

	/// <summary>Disconnects the current device, if any.</summary>
	public void Disconnect ()
		{
		this._api = null;
		this._transport?.Dispose ();
		this._transport = null;
		}

	// Six random bytes, hex-encoded - generated once at pair time and persisted, per the
	// porting brief's WP6 notes on the _systemInfo "_i" field.
	private static string GenerateStableIdentifier ()
		{
		byte[] bytes = new byte[6];
		RandomNumberGenerator.Fill (bytes);
		return Convert.ToHexString (bytes).ToLowerInvariant ();
		}

	/// <inheritdoc/>
	public void Dispose ()
		{
		this.Disconnect ();
		}
	}
