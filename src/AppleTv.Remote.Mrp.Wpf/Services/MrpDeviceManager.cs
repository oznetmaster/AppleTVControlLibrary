// Copyright (c) 2026 Neil Colvin. Licensed under the MIT License.
// See LICENSE file in the repository root for full license text.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using AppleTvControlLibrary.Auth;
using AppleTvControlLibrary.Discovery.AirPlay;
using AppleTvControlLibrary.Mrp.AirPlay;
using AppleTvControlLibrary.Mrp.AirPlay.Auth;
using AppleTvControlLibrary.Mrp.AirPlay.Http;
using AppleTvControlLibrary.Mrp.PlayerState;
using AppleTvControlLibrary.Mrp.Protocol;
using AppleTvControlLibrary.Mrp.RemoteControl;

using AppleTvControlLibrary.Remote.Mrp.Wpf.Storage;

namespace AppleTvControlLibrary.Remote.Mrp.Wpf.Services;

/// <summary>
/// Orchestrates discovery, pairing, connection and remote-control command dispatch for a single
/// MRP-over-AirPlay device, gluing together <see cref="MulticastAirPlayDiscovery"/>,
/// <see cref="AirPlayHapPairSetupProcedure"/>, <see cref="Ap2Session"/>,
/// <see cref="AirPlayMrpConnection"/>, <see cref="MrpProtocol"/>/<see cref="MrpRemoteControl"/>
/// and <see cref="CredentialStore"/>. Only MRP tunneled over an AirPlay 2 data-stream channel is
/// supported; the legacy direct-TCP MRP transport has been retired.
/// </summary>
public sealed class MrpDeviceManager : IDisposable
	{
	private readonly IAirPlayDiscovery _airPlayDiscovery;
	private readonly CredentialStore _credentialStore;

	private MrpProtocol? _protocol;
	private MrpPlayerStateManager? _playerStateManager;
	private MrpRemoteControl? _remoteControl;

	private Ap2Session? _airPlaySession;
	private AirPlayMrpConnection? _airPlayConnection;

	// AirPlay pair-setup state.
	private HttpConnection? _airPlayPairingConnection;
	private SrpAuthHandler? _airPlayPairingSrp;
	private AirPlayHapPairSetupProcedure? _airPlayPairingProcedure;
	private string? _airPlayPairingUniqueId;
	private string? _airPlayPairingName;
	private string? _airPlayPairingAddress;
	private int _airPlayPairingPort;

	/// <summary>Initializes a new instance of the <see cref="MrpDeviceManager"/> class.</summary>
	/// <param name="credentialStore">The credential store used to persist/load pairings.</param>
	/// <param name="airPlayDiscovery">The AirPlay discovery implementation to use for scanning.</param>
	public MrpDeviceManager (CredentialStore? credentialStore = null, IAirPlayDiscovery? airPlayDiscovery = null)
		{
		this._credentialStore = credentialStore ?? new CredentialStore ();
		this._airPlayDiscovery = airPlayDiscovery ?? new MulticastAirPlayDiscovery ();
		}

	/// <summary>Gets the connected <see cref="MrpRemoteControl"/> instance, if connected.</summary>
	public MrpRemoteControl? RemoteControl => this._remoteControl;

	/// <summary>Gets the connected <see cref="MrpProtocol"/> instance, if connected.</summary>
	public MrpProtocol? Protocol => this._protocol;

	/// <summary>Gets the connected <see cref="MrpPlayerStateManager"/> instance, if connected.</summary>
	public MrpPlayerStateManager? PlayerStateManager => this._playerStateManager;

	/// <summary>Gets a value indicating whether a device is currently connected.</summary>
	public bool IsConnected => this._remoteControl is not null;

	/// <summary>
	/// Raised when the connection to the device is closed or lost. The library does not
	/// implement automatic reconnection; consumers wanting to reconnect must do so themselves.
	/// </summary>
	public event EventHandler<Exception?>? ConnectionClosed;

	/// <summary>Scans the network for AirPlay devices that can tunnel MRP.</summary>
	/// <param name="timeout">How long to scan for.</param>
	/// <param name="cancellationToken">A token to cancel the scan.</param>
	public Task<IReadOnlyList<AirPlayDiscoveryResult>> ScanAirPlayAsync (TimeSpan timeout, CancellationToken cancellationToken = default)
		{
		return this._airPlayDiscovery.ScanAsync (timeout, cancellationToken);
		}

	/// <summary>Loads previously saved credentials for the given device, if any.</summary>
	/// <param name="uniqueId">The device's stable unique id.</param>
	public StoredDevice? LoadStoredDevice (string uniqueId) => this._credentialStore.Load (uniqueId);

	/// <summary>
	/// Loads the stored device currently marked for automatic connection at startup, if any.
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
	/// Starts AirPlay pair-setup against the given device, causing it to display an on-screen PIN.
	/// Used for devices whose MRP service is only reachable by tunneling over an AirPlay 2 data
	/// channel, rather than a direct TCP MRP connection.
	/// </summary>
	/// <param name="device">The discovered AirPlay device to pair with.</param>
	/// <param name="cancellationToken">A token to cancel the connection/pairing attempt.</param>
	// pyatv/protocols/airplay/auth/hap.py (AirPlayHapPairSetupProcedure.start_pairing) — line 43-58 as of pyatv 0.18.0
	public async Task BeginPairAirPlayAsync (AirPlayDiscoveryResult device, CancellationToken cancellationToken = default)
		{
		if (device.Address is null)
			{
			throw new InvalidOperationException ("Device has no resolved address");
			}

		this._airPlayPairingSrp = new SrpAuthHandler ();
		this._airPlayPairingConnection = await HttpConnection.ConnectAsync (
			device.Address.ToString (), device.Port, cancellationToken).ConfigureAwait (false);

		this._airPlayPairingProcedure = new AirPlayHapPairSetupProcedure (this._airPlayPairingConnection, this._airPlayPairingSrp);

		this._airPlayPairingUniqueId = device.UniqueId;
		this._airPlayPairingName = AirPlayServiceInfo.RemoveNameCollisionSuffix (device.Name);
		this._airPlayPairingAddress = device.Address.ToString ();
		this._airPlayPairingPort = device.Port;

		await this._airPlayPairingProcedure.StartPairingAsync ().ConfigureAwait (false);
		}

	/// <summary>
	/// Finishes AirPlay pair-setup using the PIN shown on the device, and persists the resulting
	/// AirPlay-specific credentials.
	/// </summary>
	/// <param name="pin">The PIN code entered by the user.</param>
	/// <returns>The stored device record, now with AirPlay credentials populated.</returns>
	public async Task<StoredDevice> CompletePairAirPlayAsync (int pin)
		{
		if (this._airPlayPairingProcedure is null || this._airPlayPairingUniqueId is null)
			{
			throw new InvalidOperationException ("BeginPairAirPlayAsync must be called first");
			}

		HapCredentials credentials = await this._airPlayPairingProcedure.FinishPairingAsync (pin).ConfigureAwait (false);

		StoredDevice stored = this._credentialStore.Load (this._airPlayPairingUniqueId) ?? new StoredDevice
			{
			UniqueId = this._airPlayPairingUniqueId,
			};

		stored.Name = this._airPlayPairingName ?? stored.Name;
		stored.Address = this._airPlayPairingAddress ?? stored.Address;
		stored.Port = this._airPlayPairingPort;
		stored.SetCredentials (credentials);

		this._credentialStore.Save (stored);

		this.DisposeAirPlayPairingResources ();

		return stored;
		}

	/// <summary>Connects to a previously paired device using its stored credentials.</summary>
	/// <param name="device">The stored device to connect to.</param>
	/// <param name="cancellationToken">A token to cancel the connection attempt.</param>
	// pyatv/protocols/mrp/protocol.py (start) — line 123-172 as of pyatv 0.18.0
	public async Task ConnectAsync (StoredDevice device, CancellationToken cancellationToken = default)
		{
		if (this.IsConnected)
			{
			throw new InvalidOperationException ("Already connected");
			}

		await this.ConnectAirPlayAsync (device, cancellationToken).ConfigureAwait (false);
		}

	/// <summary>
	/// Connects to a previously paired device by tunneling MRP over an AirPlay 2 data-stream
	/// channel: opens the AirPlay control connection, runs pair-verify, brings up the
	/// remote-control channel, then drives the transport-agnostic <see cref="MrpProtocol"/> via
	/// the <see cref="AirPlayMrpConnection"/> adapter.
	/// </summary>
	/// <param name="device">The stored AirPlay device to connect to.</param>
	/// <param name="cancellationToken">A token to cancel the connection attempt.</param>
	// pyatv/protocols/airplay/mrp_connection.py (AirPlayMrpConnection) — line 16-75 as of pyatv 0.18.0
	private async Task ConnectAirPlayAsync (StoredDevice device, CancellationToken cancellationToken)
		{
		HapCredentials credentials = device.ToCredentials ();

		Ap2Session session = new Ap2Session (device.Address, device.Port, credentials);
		try
			{
			await session.ConnectAsync (cancellationToken).ConfigureAwait (false);
			await session.SetupRemoteControlAsync (cancellationToken).ConfigureAwait (false);

			// pyatv/protocols/airplay/__init__.py — line 272 as of pyatv 0.18.0: start_keep_alive is
			// invoked right after setup_remote_control() so the receiver does not close the control
			// (and therefore data-stream) connection a few seconds later.
			session.KeepAliveFinished += () => this.OnConnectionFaulted (null);
			session.KeepAliveFailed += exception => this.OnConnectionFaulted (exception);
			session.StartKeepAlive ();

			AirPlayMrpConnection airPlayConnection = new AirPlayMrpConnection (session);
			airPlayConnection.ConnectionLost += exception => this.OnConnectionFaulted (exception);
			airPlayConnection.Connect ();

			// pyatv/protocols/mrp/protocol.py — line 137-140 as of pyatv 0.18.0: reuse the client
			// identifier the device originally paired with, rather than a freshly generated one.
			SrpAuthHandler srp = new SrpAuthHandler
				{
				PairingId = credentials.ClientId,
				};

			// pyatv/protocols/airplay/mrp_connection.py — the AirPlay data-stream channel is
			// already HAP-encrypted end-to-end (see AirPlayMrpConnection.EnableEncryption, a
			// deliberate no-op), so MrpProtocol.Credentials is intentionally left unset here:
			// setting it would make EnableEncryptionAsync attempt a second, MRP-level pair-verify
			// handshake that the AirPlay-tunneled device never expects.
			MrpProtocol protocol = new MrpProtocol (airPlayConnection, srp, new MrpInfoSettings ())
				{
				// pyatv/protocols/airplay/mrp_connection.py (send) — line 55-59 as of pyatv 0.18.0:
				// AirPlayMrpConnection does its own AirPlay-specific framing, so route AsyncSender
				// through it directly.
				AsyncSender = airPlayConnection.SendAsync,
				};

			MrpPlayerStateManager playerStateManager = new MrpPlayerStateManager ();
			protocol.Listener = playerStateManager;

			await protocol.StartAsync (cancellationToken: cancellationToken).ConfigureAwait (false);

			this._airPlaySession = session;
			this._airPlayConnection = airPlayConnection;
			this._protocol = protocol;
			this._playerStateManager = playerStateManager;
			this._remoteControl = new MrpRemoteControl (protocol, playerStateManager);
			}
		catch
			{
			session.Dispose ();
			throw;
			}
		}

	/// <summary>Disconnects from the currently connected device, if any.</summary>
	public void Disconnect ()
		{
		this._protocol?.Stop ();
		this._airPlayConnection?.Dispose ();
		this._airPlaySession?.Dispose ();

		this._airPlaySession = null;
		this._airPlayConnection = null;
		this._protocol = null;
		this._playerStateManager = null;
		this._remoteControl = null;
		}

	private void OnConnectionFaulted (Exception? exception)
		{
		this.Disconnect ();
		this.ConnectionClosed?.Invoke (this, exception);
		}

	private void DisposeAirPlayPairingResources ()
		{
		this._airPlayPairingConnection?.Dispose ();
		this._airPlayPairingConnection = null;
		this._airPlayPairingSrp = null;
		this._airPlayPairingProcedure = null;
		this._airPlayPairingUniqueId = null;
		this._airPlayPairingName = null;
		this._airPlayPairingAddress = null;
		this._airPlayPairingPort = 0;
		}

	/// <inheritdoc/>
	public void Dispose ()
		{
		this.Disconnect ();
		this.DisposeAirPlayPairingResources ();
		}
	}
