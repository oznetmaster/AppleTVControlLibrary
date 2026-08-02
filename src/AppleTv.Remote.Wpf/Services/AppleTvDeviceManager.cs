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

	/// <summary>
	/// Raised whenever the connected device's advertised media-control capabilities (including
	/// <see cref="IsVolumeControlSupported"/>) may have changed.
	/// </summary>
	public event EventHandler? MediaControlCapabilitiesChanged;

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
	/// Begins pair-setup by connecting to the device and sending M1 (<c>PS_Start</c>). This is
	/// what causes the Apple TV to display the on-screen PIN, so the PIN can only be known -
	/// and therefore only be requested from the user - after this method returns. Follow up with
	/// <see cref="CompletePairAsync"/> once the user has supplied the PIN shown on the TV.
	/// </summary>
	/// <param name="device">The device to pair with.</param>
	/// <returns>An in-progress pairing session to pass to <see cref="CompletePairAsync"/>.</returns>
	// pyatv/protocols/companion/auth.py:37-60 (CompanionPairSetupProcedure, M1/M2)
	public async Task<PairingSession> BeginPairAsync (CompanionDiscoveryResult device)
		{
		if (device.Address is null)
			{
			throw new InvalidOperationException ("Device has no resolved address");
			}

		return await Task.Run (() =>
			{
			CompanionConnection connection = new CompanionConnection ();
			CompanionProtocol protocol = new CompanionProtocol (connection, new SrpAuthHandler ());
			TcpCompanionTransport transport = TcpCompanionTransport.Connect (
				device.Address.ToString (), device.Port, connection, protocol);

			SrpAuthHandler srp = new SrpAuthHandler ();
			srp.Initialize ();

			// M1 - sending this is what makes the Apple TV display the on-screen PIN.
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

			return new PairingSession (device, transport, protocol, srp, atvSalt, atvPubKey);
			}).ConfigureAwait (false);
		}

	/// <summary>
	/// Completes pair-setup (SRP M3-M6, per the porting brief's WP4 message shapes) using the
	/// PIN shown on the TV after <see cref="BeginPairAsync"/>, then persists the resulting
	/// credentials and stable identifier.
	/// </summary>
	/// <param name="session">The in-progress session returned by <see cref="BeginPairAsync"/>.</param>
	/// <param name="pin">The PIN code displayed on the TV.</param>
	/// <returns>The stored device record, ready to be used for <see cref="ConnectAsync"/>.</returns>
	// pyatv/protocols/companion/auth.py:60-100 (CompanionPairSetupProcedure, M3-M6)
	public async Task<StoredDevice> CompletePairAsync (PairingSession session, int pin)
		{
		return await Task.Run (() =>
			{
			using TcpCompanionTransport transport = session.Transport;
			CompanionProtocol protocol = session.Protocol;
			SrpAuthHandler srp = session.Srp;
			CompanionDiscoveryResult device = session.Device;
			byte[] atvSalt = session.AtvSalt;
			byte[] atvPubKey = session.AtvPubKey;

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
	/// <param name="stored">The stored device record from a previous <see cref="CompletePairAsync"/> call.</param>
	// pyatv/protocols/companion/auth.py:120-158 (CompanionPairVerifyProcedure)
	public async Task ConnectAsync (StoredDevice stored)
		{
		await Task.Run (() =>
			{
			System.Diagnostics.Debug.WriteLine ($"[AppleTvDeviceManager] Connecting to {stored.Name} at {stored.Address}:{stored.Port}");
			CompanionConnection connection = new CompanionConnection ();
			CompanionProtocol protocol = new CompanionProtocol (connection, new SrpAuthHandler ());
			this._transport = TcpCompanionTransport.Connect (stored.Address, stored.Port, connection, protocol);
			System.Diagnostics.Debug.WriteLine ("[AppleTvDeviceManager] TCP connected, starting pair-verify");

			HapCredentials credentials = stored.ToCredentials ();

			SrpAuthHandler srp = new SrpAuthHandler ();
			(byte[] _, byte[] verifyPubKey) = srp.Initialize ();

			byte[] pv1 = Tlv8.Tlv8.WriteTlv (new Dictionary<int, byte[]>
				{
				{ (int)TlvValue.SeqNo, new byte[] { 1 } },
				{ (int)TlvValue.PublicKey, verifyPubKey },
				});
			System.Diagnostics.Debug.WriteLine ("[AppleTvDeviceManager] Sending pair-verify M1 (PV_Start)");
			Dictionary<object, object?> pv2Response = protocol.ExchangeAuth (FrameType.PV_Start, new Dictionary<string, object?> { ["_pd"] = pv1, ["_auTy"] = 4 });
			System.Diagnostics.Debug.WriteLine ("[AppleTvDeviceManager] Received pair-verify M2");
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
			System.Diagnostics.Debug.WriteLine ("[AppleTvDeviceManager] Sending pair-verify M3 (PV_Next)");
			Dictionary<object, object?> pv4Response = protocol.ExchangeAuth (FrameType.PV_Next, new Dictionary<string, object?> { ["_pd"] = pv3 });
			System.Diagnostics.Debug.WriteLine ("[AppleTvDeviceManager] Received pair-verify M4");
			byte[] pv4 = (byte[])pv4Response["_pd"]!;
			Dictionary<int, byte[]> pv4Tlv = Tlv8.Tlv8.ReadTlv (pv4);
			if (pv4Tlv.ContainsKey ((int)TlvValue.Error))
				{
				throw new AuthenticationException ("Pair-verify failed");
				}

			(byte[] outputKey, byte[] inputKey) = srp.Verify2 (
				CompanionProtocol.SRP_SALT, CompanionProtocol.SRP_OUTPUT_INFO, CompanionProtocol.SRP_INPUT_INFO);
			connection.EnableEncryption (outputKey, inputKey);
			System.Diagnostics.Debug.WriteLine ("[AppleTvDeviceManager] Pair-verify complete, encryption enabled; starting session bring-up");

			CompanionApi api = new CompanionApi (
				protocol,
				credentials,
				stableIdentifier: stored.StableIdentifier,
				deviceId: Convert.ToHexString (credentials.AtvId).ToLowerInvariant (),
				model: "AppleTV",
				name: stored.Name);
			api.Connect ();
			System.Diagnostics.Debug.WriteLine ("[AppleTvDeviceManager] Connect complete");

			api.MediaControlCapabilitiesChanged += this.OnMediaControlCapabilitiesChanged;
			this._api = api;
			}).ConfigureAwait (false);
		}

	private void OnMediaControlCapabilitiesChanged (object? sender, EventArgs e)
		{
		this.MediaControlCapabilitiesChanged?.Invoke (this, EventArgs.Empty);
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

	/// <summary>
	/// Toggles mute using the Companion media-control channel (<c>_mcc</c>: <c>GetVolume</c>/
	/// <c>SetVolume</c>), saving and restoring the actual volume level rather than approximating
	/// with repeated volume-step presses. Requires the device to advertise volume control support
	/// via the <c>_iMC</c> event's <c>_mcF</c> bitmask.
	/// </summary>
	/// <returns>The resulting muted state.</returns>
	public bool ToggleMute ()
		{
		if (this._api is null)
			{
			throw new InvalidOperationException ("Not connected");
			}

		return this._api.ToggleMute ();
		}

	/// <summary>Gets a value indicating whether the connected device supports volume control.</summary>
	public bool IsVolumeControlSupported => this._api?.IsVolumeControlSupported ?? false;

	/// <summary>Disconnects the current device, if any.</summary>
	public void Disconnect ()
		{
		if (this._api is not null)
			{
			this._api.MediaControlCapabilitiesChanged -= this.OnMediaControlCapabilitiesChanged;
			}

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

/// <summary>
/// Represents an in-progress pair-setup exchange, captured after M1/M2 (which is what causes
/// the Apple TV to display its on-screen PIN) and before M3-M6. Pass this to
/// <see cref="AppleTvDeviceManager.CompletePairAsync"/> once the user has supplied the PIN.
/// </summary>
public sealed class PairingSession
	{
	internal PairingSession (CompanionDiscoveryResult device, TcpCompanionTransport transport, CompanionProtocol protocol, SrpAuthHandler srp, byte[] atvSalt, byte[] atvPubKey)
		{
		this.Device = device;
		this.Transport = transport;
		this.Protocol = protocol;
		this.Srp = srp;
		this.AtvSalt = atvSalt;
		this.AtvPubKey = atvPubKey;
		}

	internal CompanionDiscoveryResult Device { get; }

	internal TcpCompanionTransport Transport { get; }

	internal CompanionProtocol Protocol { get; }

	internal SrpAuthHandler Srp { get; }

	internal byte[] AtvSalt { get; }

	internal byte[] AtvPubKey { get; }
	}
