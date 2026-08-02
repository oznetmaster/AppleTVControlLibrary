// Copyright (c) 2026 Neil Colvin. Licensed under the MIT License.
// See LICENSE file in the repository root for full license text.

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

	/// <summary>
	/// Gets the connected device's most recently known system status (power state), tracked via
	/// the initial <see cref="CompanionApi.Connect"/> snapshot and pushed
	/// <c>SystemStatus</c>/<c>TVSystemStatus</c> events. <see cref="SystemStatus.Unknown"/> if
	/// not connected or if no status has been observed yet.
	/// </summary>
	public SystemStatus CurrentSystemStatus => this._api?.CurrentSystemStatus ?? SystemStatus.Unknown;

	/// <summary>
	/// Raised whenever a pushed <c>SystemStatus</c>/<c>TVSystemStatus</c> event updates
	/// <see cref="CurrentSystemStatus"/>.
	/// </summary>
	public event EventHandler? SystemStatusChanged;

	/// <summary>
	/// Gets the connected device's current keyboard (RTI text input) focus state, tracked via
	/// <c>_tiStarted</c>/<c>_tiStopped</c> events (and the <c>_tiStart</c> response).
	/// <see cref="KeyboardFocusState.Unknown"/> if not connected or if no state has been
	/// observed yet.
	/// </summary>
	public KeyboardFocusState TextFocusState => this._api?.TextFocusState ?? KeyboardFocusState.Unknown;

	/// <summary>
	/// Raised whenever <see cref="TextFocusState"/> changes: the Apple TV wants (or no longer
	/// wants) on-screen text input, e.g. when the user selects a text field in a tvOS app.
	/// </summary>
	public event EventHandler? TextFocusStateChanged;

	/// <summary>Gets the current virtual keyboard text from the connected device.</summary>
	// pyatv/protocols/companion/__init__.py (CompanionKeyboard.text_get) — line 517-519 as of pyatv 0.18.0
	public string? TextGet ()
		{
		if (this._api is null)
			{
			throw new InvalidOperationException ("Not connected");
			}

		return this._api.TextGet ();
		}

	/// <summary>Replaces the virtual keyboard text on the connected device.</summary>
	/// <param name="text">The new text.</param>
	// pyatv/protocols/companion/__init__.py (CompanionKeyboard.text_set) — line 529-531 as of pyatv 0.18.0
	public void SetText (string text)
		{
		if (this._api is null)
			{
			throw new InvalidOperationException ("Not connected");
			}

		this._api.TextSet (text);
		}

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
	/// Loads the stored device currently marked for automatic connection at startup, if any,
	/// for ease-of-testing so the same paired device doesn't need to be reselected every run.
	/// </summary>
	public StoredDevice? LoadAutoConnectDevice () => this._credentialStore.LoadAutoConnectDevice ();

	/// <summary>
	/// Marks <paramref name="uniqueId"/> as the device to automatically connect to on the next
	/// application startup, clearing the flag on every other stored device.
	/// </summary>
	/// <param name="uniqueId">
	/// The device to enable auto-connect for, or <see langword="null"/> to disable auto-connect.
	/// </param>
	public void SetAutoConnect (string? uniqueId) => this._credentialStore.SetAutoConnect (uniqueId);

	/// <summary>
	/// Begins pair-setup by connecting to the device and sending M1 (<c>PS_Start</c>). This is
	/// what causes the Apple TV to display the on-screen PIN, so the PIN can only be known -
	/// and therefore only be requested from the user - after this method returns. Follow up with
	/// <see cref="CompletePairAsync"/> once the user has supplied the PIN shown on the TV.
	/// </summary>
	/// <param name="device">The device to pair with.</param>
	/// <returns>An in-progress pairing session to pass to <see cref="CompletePairAsync"/>.</returns>
	// pyatv/protocols/companion/auth.py (CompanionPairSetupProcedure, M1/M2) — line 37-60 as of pyatv 0.18.0
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
	// pyatv/protocols/companion/auth.py (CompanionPairSetupProcedure, M3-M6) — line 60-100 as of pyatv 0.18.0
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
				UniqueId = device.UniqueId ?? ToHexString (credentials.AtvId).ToLowerInvariant (),
				Name = device.Name,
				Address = device.Address?.ToString () ?? string.Empty,
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
	// pyatv/protocols/companion/auth.py (CompanionPairVerifyProcedure) — line 120-158 as of pyatv 0.18.0
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
			// pyatv/protocols/companion/auth.py — line 145-158 as of pyatv 0.18.0: M3 carries no "_auTy", unlike M1.
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
				deviceId: ToHexString (credentials.AtvId).ToLowerInvariant (),
				model: "AppleTV",
				name: stored.Name);
			api.Connect ();
			System.Diagnostics.Debug.WriteLine ("[AppleTvDeviceManager] Connect complete");

			api.MediaControlCapabilitiesChanged += this.OnMediaControlCapabilitiesChanged;
			api.SystemStatusChanged += this.OnSystemStatusChanged;
			api.TextFocusStateChanged += this.OnTextFocusStateChanged;
			this._api = api;

			// pyatv/protocols/companion/api.py (app_list/account_list) — best-effort, mirroring the
			// FetchAttentionState pattern: some devices/tvOS builds do not populate these, which must
			// not prevent the rest of connect from completing.
			try
				{
				this.Apps = api.AppList ();
				}
			catch (Exception ex)
				{
				System.Diagnostics.Debug.WriteLine ($"[AppleTvDeviceManager] AppList failed (ignored): {ex}");
				this.Apps = new Dictionary<string, string> ();
				}

			try
				{
				this.Accounts = api.AccountList ();
				}
			catch (Exception ex)
				{
				System.Diagnostics.Debug.WriteLine ($"[AppleTvDeviceManager] AccountList failed (ignored): {ex}");
				this.Accounts = new Dictionary<string, string> ();
				}
			}).ConfigureAwait (false);
		}

	private void OnMediaControlCapabilitiesChanged (object? sender, EventArgs e)
		{
		this.MediaControlCapabilitiesChanged?.Invoke (this, EventArgs.Empty);
		}

	private void OnSystemStatusChanged (object? sender, EventArgs e)
		{
		this.SystemStatusChanged?.Invoke (this, EventArgs.Empty);
		}

	private void OnTextFocusStateChanged (object? sender, EventArgs e)
		{
		this.TextFocusStateChanged?.Invoke (this, EventArgs.Empty);
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

	/// <summary>Sends a raw touchpad (touch surface) event to the connected device.</summary>
	/// <param name="x">The x coordinate, in the range [0, 1000].</param>
	/// <param name="y">The y coordinate, in the range [0, 1000].</param>
	/// <param name="action">The touch phase.</param>
	public void SendTouchEvent (int x, int y, TouchAction action)
		{
		if (this._api is null)
			{
			throw new InvalidOperationException ("Not connected");
			}

		this._api.SendHidEvent (x, y, action);
		}

	/// <summary>Sends a touchpad click (tap), as opposed to a swipe/drag.</summary>
	/// <param name="action">The click gesture: single tap, double tap, or press-and-hold.</param>
	public void SendTouchClick (InputAction action)
		{
		if (this._api is null)
			{
			throw new InvalidOperationException ("Not connected");
			}

		this._api.SendClick (action);
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

	/// <summary>
	/// Gets the launchable apps fetched during connect, as a bundle-identifier-to-display-name
	/// mapping. Empty (never <see langword="null"/>) if not connected or if the device did not
	/// return any apps.
	/// </summary>
	public IReadOnlyDictionary<string, string> Apps
		{
		get;
		private set;
		} = new Dictionary<string, string> ();

	/// <summary>
	/// Gets the switchable user accounts fetched during connect, as an account-identifier-to-
	/// display-name mapping. Empty (never <see langword="null"/>) if not connected or if the
	/// device did not return any accounts.
	/// </summary>
	public IReadOnlyDictionary<string, string> Accounts
		{
		get;
		private set;
		} = new Dictionary<string, string> ();

	/// <summary>Launches an app on the connected device.</summary>
	/// <param name="bundleIdOrUrl">A bundle identifier or a URL/URL scheme to open.</param>
	public void LaunchApp (string bundleIdOrUrl)
		{
		if (this._api is null)
			{
			throw new InvalidOperationException ("Not connected");
			}

		this._api.LaunchApp (bundleIdOrUrl);
		}

	/// <summary>Switches the active user account on the connected device.</summary>
	/// <param name="accountId">The account identifier to switch to, from <see cref="Accounts"/>.</param>
	public void SwitchAccount (string accountId)
		{
		if (this._api is null)
			{
			throw new InvalidOperationException ("Not connected");
			}

		this._api.SwitchAccount (accountId);
		}

	/// <summary>
	/// Toggles power using the cached <see cref="CurrentSystemStatus"/> (tracked via the initial
	/// connect snapshot and pushed <c>SystemStatus</c>/<c>TVSystemStatus</c> events, per
	/// pyatv/protocols/companion/__init__.py — line 219-246 as of pyatv 0.18.0) and sending the corresponding
	/// <see cref="HidCommand.Sleep"/> or <see cref="HidCommand.Wake"/> command: an asleep device
	/// is woken, anything else (awake, screensaver, idle, unknown) is put to sleep.
	/// </summary>
	/// <returns><see langword="true"/> if a wake command was sent, <see langword="false"/> if a sleep command was sent.</returns>
	// pyatv/protocols/companion/api.py (HidCommand.Sleep/Wake) — line 38/44-45 as of pyatv 0.18.0. turn_on/turn_off both
	// call hid_command(False, ...) - a single up-only event, not a down/up pair.
	public bool TogglePower ()
		{
		if (this._api is null)
			{
			throw new InvalidOperationException ("Not connected");
			}

		bool shouldWake = this._api.CurrentSystemStatus == SystemStatus.Asleep;
		HidCommand command = shouldWake ? HidCommand.Wake : HidCommand.Sleep;
		this._api.SendHidCommand (down: false, command: command);
		return shouldWake;
		}

	/// <summary>Disconnects the current device, if any.</summary>
	public void Disconnect ()
		{
		if (this._api is not null)
			{
			this._api.MediaControlCapabilitiesChanged -= this.OnMediaControlCapabilitiesChanged;
			this._api.SystemStatusChanged -= this.OnSystemStatusChanged;
			this._api.TextFocusStateChanged -= this.OnTextFocusStateChanged;
			}

		this._api = null;
		this._transport?.Dispose ();
		this._transport = null;
		this.Apps = new Dictionary<string, string> ();
		this.Accounts = new Dictionary<string, string> ();
		}

	// Six random bytes, hex-encoded - generated once at pair time and persisted, per the
	// porting brief's WP6 notes on the _systemInfo "_i" field.
	private static string GenerateStableIdentifier ()
		{
		byte[] bytes = new byte[6];
#if NET472
		Compat.FillRandom (bytes);
#else
		RandomNumberGenerator.Fill (bytes);
#endif
		return ToHexString (bytes).ToLowerInvariant ();
		}

	// Convert.ToHexString is not available on net472; Compat.ToHexString polyfills it there.
	private static string ToHexString (byte[] bytes)
		{
#if NET472
		return Compat.ToHexString (bytes);
#else
		return Convert.ToHexString (bytes);
#endif
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
