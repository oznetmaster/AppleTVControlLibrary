// Copyright (c) 2026 Neil Colvin. Licensed under the MIT License.
// See LICENSE file in the repository root for full license text.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

using AppleTvControlLibrary.Discovery.Companion;
using AppleTvControlLibrary.Protocol;
using AppleTvControlLibrary.Remote.Wpf.Services;
using AppleTvControlLibrary.Remote.Wpf.Storage;

namespace AppleTvControlLibrary.Remote.Wpf.ViewModels;

/// <summary>Main window view model: device discovery, pairing/connection state, and remote commands.</summary>
public sealed class MainViewModel : ViewModelBase, IDisposable
	{
	private readonly AppleTvDeviceManager _deviceManager;
	private DeviceListItem? _selectedDevice;
	private string _statusMessage = "Not connected.";
	private bool _isBusy;
	private bool _isMuted;
	private bool _isAwake;
	private bool _isPowerStateKnown;
	private bool _autoConnectSelected;
	private SelectableItem? _selectedApp;
	private SelectableItem? _selectedAccount;
	private bool _isPopulatingAppsOrAccounts;
	private bool _isCurrentAccountKnown;
	private StoredDevice? _lastConnectedDevice;
	private CancellationTokenSource? _reconnectCts;

	// pyatv itself implements no automatic reconnection for Companion (see
	// AppleTvDeviceManager.OnConnectionClosed remarks); this bounded retry/backoff is a WPF-app-level
	// UX affordance layered on top, not a protocol requirement.
	private static readonly TimeSpan[] ReconnectDelays =
		[
		TimeSpan.FromSeconds (2),
		TimeSpan.FromSeconds (5),
		TimeSpan.FromSeconds (10),
		TimeSpan.FromSeconds (20),
		TimeSpan.FromSeconds (30),
		];

	/// <summary>Initializes a new instance of the <see cref="MainViewModel"/> class.</summary>
	public MainViewModel ()
		{
		_deviceManager = new AppleTvDeviceManager ();

		ScanCommand = new RelayCommand (async () => await ScanAsync ().ConfigureAwait (true), () => !IsBusy);
		PairCommand = new RelayCommand (async () => await PairAsync ().ConfigureAwait (true), () => !IsBusy && SelectedDevice is not null);
		ConnectCommand = new RelayCommand (async () => await ConnectAsync ().ConfigureAwait (true), () => !IsBusy && SelectedDevice is { IsPaired: true });
		DisconnectCommand = new RelayCommand (Disconnect, () => IsConnected);

		UpButton = CreateHidCommand (HidCommand.Up);
		DownButton = CreateHidCommand (HidCommand.Down);
		LeftButton = CreateHidCommand (HidCommand.Left);
		RightButton = CreateHidCommand (HidCommand.Right);
		SelectButton = CreateHidCommand (HidCommand.Select);
		MenuButton = CreateHidCommand (HidCommand.Menu);
		HomeButton = CreateHidCommand (HidCommand.Home);
		PlayPauseButton = CreateHidCommand (HidCommand.PlayPause);
		VolumeUpButton = CreateHidCommand (HidCommand.VolumeUp, () => IsConnected && IsVolumeControlSupported);
		VolumeDownButton = CreateHidCommand (HidCommand.VolumeDown, () => IsConnected && IsVolumeControlSupported);
		SiriButton = CreateHidCommand (HidCommand.Siri);

		MuteButton = new RelayCommand (async () => await ToggleMuteAsync ().ConfigureAwait (true), () => IsConnected && IsVolumeControlSupported);
		PowerButton = new RelayCommand (async () => await TogglePowerAsync ().ConfigureAwait (true), () => IsConnected);

		_deviceManager.MediaControlCapabilitiesChanged += (_, _) =>
			{
			Application.Current?.Dispatcher.Invoke (RaiseRemoteButtonStates);
			};

		// pyatv/protocols/companion/__init__.py (_handle_system_status_update) — line 249-256 as of pyatv 0.18.0: power
		// state is tracked from pushed SystemStatus/TVSystemStatus events, not by polling.
		_deviceManager.SystemStatusChanged += (_, _) =>
			{
			Application.Current?.Dispatcher.Invoke (() =>
				{
				ApplySystemStatus (_deviceManager.CurrentSystemStatus);
				});
			};

		// pyatv/protocols/companion/__init__.py (CompanionKeyboard, listen_to "_tiStarted"/"_tiStopped") —
		// line 494-497 as of pyatv 0.18.0: the on-screen keyboard's focus state is pushed by the device,
		// so the text-input dialog must be shown/hidden reactively rather than on user request.
		//
		// This event is raised synchronously on the connection's background receive thread (from
		// CompanionConnection.ReceiveData -> FrameReceived -> HandleOpack -> Listener.EventReceived).
		// ApplyTextFocusState calls back into the device (TextGet, which round-trips _tiStop/_tiStart),
		// and that round trip can only complete once the receive thread is free to process the
		// response frame. Using a blocking Dispatcher.Invoke here would therefore deadlock: the
		// receive thread would block on the UI thread, which would in turn block waiting for a
		// response only the receive thread can deliver. BeginInvoke lets the receive thread return
		// immediately while the UI thread handles the round trip asynchronously.
		_deviceManager.TextFocusStateChanged += (_, _) =>
			{
			Application.Current?.Dispatcher.BeginInvoke (new Action (async () => await ApplyTextFocusStateAsync ().ConfigureAwait (true)));
			};

		// The library does not attempt automatic reconnection; an unexpected disconnect (or the
		// remote end cleanly closing the socket) is surfaced here so the UI can reset to a
		// disconnected state instead of silently going stale (dead buttons, frozen status text).
		_deviceManager.ConnectionClosed += (_, e) =>
			{
			Application.Current?.Dispatcher.Invoke (() => HandleConnectionClosed (e));
			};
		}

	private async Task ApplyTextFocusStateAsync ()
		{
		if (_deviceManager.TextFocusState == KeyboardFocusState.Focused)
			{
			string? currentText = null;
			try
				{
				currentText = await _deviceManager.TextGetAsync ().ConfigureAwait (true);
				}
			catch (Exception ex)
				{
				System.Diagnostics.Debug.WriteLine ($"[AppleTv.Remote.Wpf] TextGet failed: {ex}");
				}

			ShowTextInput?.Invoke (currentText);
			}
		else
			{
			HideTextInput?.Invoke ();
			}
		}

	// pyatv/protocols/companion/__init__.py (self._power_state = PowerState.Unknown) — line 213 as of pyatv 0.18.0:
	// SystemStatus.Unknown means no fetch or pushed event has been observed yet, so IsAwake must not
	// be presented as a confident true/false until IsPowerStateKnown is true.
	private void ApplySystemStatus (SystemStatus status)
		{
		IsPowerStateKnown = status != SystemStatus.Unknown;
		IsAwake = status is not (SystemStatus.Asleep or SystemStatus.Unknown);

		// The transient "Waking..."/"Sleeping..." message set by TogglePower() must be replaced once a
		// confirming pushed status arrives, otherwise it is left showing forever even after the real
		// state (IsAwake/IsPowerStateKnown) has updated correctly.
		StatusMessage = status switch
			{
			SystemStatus.Unknown => StatusMessage,
			SystemStatus.Asleep => "Asleep.",
			_ => "Awake.",
			};
		}

	// Populates Apps/Accounts from the values fetched (best-effort) during
	// AppleTvDeviceManager.ConnectAsync. Devices that don't support a given feature return an
	// empty dictionary, in which case the corresponding dropdown stays empty and hidden (see
	// IsAppListSupported/IsAccountListSupported).
	private void PopulateAppsAndAccounts ()
		{
		_isPopulatingAppsOrAccounts = true;
		try
			{
			Apps.Clear ();
			foreach (var kvp in _deviceManager.Apps)
				{
				Apps.Add (new SelectableItem (kvp.Key, kvp.Value));
				}

			Accounts.Clear ();
			foreach (var kvp in _deviceManager.Accounts)
				{
				Accounts.Add (new SelectableItem (kvp.Key, kvp.Value));
				}

			// The device does not report which account is currently active - FetchUserAccountsEvent
			// only returns the switchable list (pyatv/protocols/companion/api.py:301-303), with no
			// current-account marker anywhere on the wire, and nothing in _systemInfo or _iMC fills
			// the gap either. So "current account" is a tri-state, same pattern as power state:
			// unknown at connect/reconnect, and only becomes known once this session has issued a
			// successful SwitchUserAccountEvent. A switch made from the physical remote or another
			// client is invisible to us - there is no pushed event for it - so IsCurrentAccountKnown
			// must revert to false on every (re)connect rather than trusting a stale cached value.
			_selectedApp = null;
			_selectedAccount = null;
			_isCurrentAccountKnown = false;
			}
		finally
			{
			_isPopulatingAppsOrAccounts = false;
			}

		OnPropertyChanged (nameof (IsAppListSupported));
		OnPropertyChanged (nameof (IsAccountListSupported));
		OnPropertyChanged (nameof (SelectedApp));
		OnPropertyChanged (nameof (SelectedAccount));
		OnPropertyChanged (nameof (IsCurrentAccountKnown));
		}

	private async Task LaunchSelectedAppAsync (SelectableItem app)
		{
		try
			{
			await _deviceManager.LaunchAppAsync (app.Id).ConfigureAwait (true);
			StatusMessage = $"Launched {app.DisplayName}.";
			}
		catch (Exception ex)
			{
			System.Diagnostics.Debug.WriteLine ($"[AppleTv.Remote.Wpf] LaunchApp failed: {ex}");
			StatusMessage = $"Launch failed: {ex.Message}";
			}
		}

	private async Task SwitchToSelectedAccountAsync (SelectableItem account)
		{
		try
			{
			await _deviceManager.SwitchAccountAsync (account.Id).ConfigureAwait (true);
			StatusMessage = $"Switched to {account.DisplayName}.";

			// Only now do we actually know the active account - a successful SwitchUserAccountEvent
			// is the one and only signal the protocol gives us (see PopulateAppsAndAccounts).
			_isCurrentAccountKnown = true;
			OnPropertyChanged (nameof (IsCurrentAccountKnown));
			}
		catch (Exception ex)
			{
			System.Diagnostics.Debug.WriteLine ($"[AppleTv.Remote.Wpf] SwitchAccount failed: {ex}");
			StatusMessage = $"Switch account failed: {ex.Message}";

			// The switch may or may not have taken effect on the device; treat the account as unknown
			// again rather than risk displaying a selection that doesn't match reality.
			_isCurrentAccountKnown = false;
			OnPropertyChanged (nameof (IsCurrentAccountKnown));
			}
		}

	/// <summary>Gets the discovered devices from the most recent scan.</summary>
	public ObservableCollection<DeviceListItem> Devices
		{
		get;
		} = [];

	/// <summary>
	/// Gets the launchable apps on the connected device. Empty when not connected or when the
	/// device does not support app listing (see <see cref="IsAppListSupported"/>).
	/// </summary>
	public ObservableCollection<SelectableItem> Apps
		{
		get;
		} = [];

	/// <summary>
	/// Gets a value indicating whether the connected device supports app listing/launching
	/// (i.e. <see cref="Apps"/> is non-empty). The app dropdown should only be shown when this
	/// is <see langword="true"/>.
	/// </summary>
	public bool IsAppListSupported => Apps.Count > 0;

	/// <summary>Gets or sets the app selected in the app dropdown, launching it on selection.</summary>
	public SelectableItem? SelectedApp
		{
		get => _selectedApp;
		set
			{
			if (SetProperty (ref _selectedApp, value) && !_isPopulatingAppsOrAccounts && value is not null)
				{
				_ = LaunchSelectedAppAsync (value);
				}
			}
		}

	/// <summary>
	/// Gets the user accounts switchable on the connected device. Empty when not connected or
	/// when the device does not support account switching (see <see cref="IsAccountListSupported"/>).
	/// </summary>
	public ObservableCollection<SelectableItem> Accounts
		{
		get;
		} = [];

	/// <summary>
	/// Gets a value indicating whether the connected device supports account listing/switching
	/// (i.e. <see cref="Accounts"/> is non-empty). The account dropdown should only be shown when
	/// this is <see langword="true"/>.
	/// </summary>
	public bool IsAccountListSupported => Accounts.Count > 0;

	/// <summary>
	/// Gets a value indicating whether <see cref="SelectedAccount"/> is known to reflect the
	/// device's actual active account, as opposed to simply being unset. The Companion protocol
	/// has no query for the current account - only <c>FetchUserAccountsEvent</c> (the switchable
	/// list) and <c>SwitchUserAccountEvent</c> (switch by id) exist - so this only becomes
	/// <see langword="true"/> once this session has issued a successful switch itself, mirroring
	/// the tri-state pattern used for power state. A switch made from the physical remote, the
	/// TV Remote app, or Control Center on the device is invisible to us (no event is pushed for
	/// it), so this reverts to <see langword="false"/> on every reconnect rather than trusting a
	/// stale cached value.
	/// </summary>
	public bool IsCurrentAccountKnown => _isCurrentAccountKnown;

	/// <summary>
	/// Gets or sets the account selected in the account dropdown, switching to it on selection.
	/// Always reflects the most recently selected/switched-to account; the device does not
	/// report which account is currently active (pyatv 0.18.0 has no such field), so this starts
	/// unselected on connect.
	/// </summary>
	public SelectableItem? SelectedAccount
		{
		get => _selectedAccount;
		set
			{
			if (SetProperty (ref _selectedAccount, value) && !_isPopulatingAppsOrAccounts && value is not null)
				{
				_ = SwitchToSelectedAccountAsync (value);
				}
			}
		}

	/// <summary>Gets or sets the currently selected device.</summary>
	public DeviceListItem? SelectedDevice
		{
		get => _selectedDevice;
		set
			{
			if (SetProperty (ref _selectedDevice, value))
				{
				RaiseCommandStates ();
				UpdateAutoConnectCheckboxFromSelection ();
				}
			}
		}

	/// <summary>Gets or sets a user-facing status message.</summary>
	public string StatusMessage
		{
		get => _statusMessage;
		set
			{
			if (SetProperty (ref _statusMessage, value))
				{
				System.Diagnostics.Debug.WriteLine ($"[AppleTv.Remote.Wpf] {value}");
				}
			}
		}

	/// <summary>Gets or sets a value indicating whether a scan/pair/connect operation is in progress.</summary>
	public bool IsBusy
		{
		get => _isBusy;
		set
			{
			if (SetProperty (ref _isBusy, value))
				{
				RaiseCommandStates ();
				}
			}
		}

	/// <summary>Gets a value indicating whether a device is currently connected.</summary>
	public bool IsConnected => _deviceManager.IsConnected;

	/// <summary>
	/// Gets a value indicating whether the connected device currently advertises volume control
	/// support. When <see langword="false"/>, audio is managed outside Companion (e.g. HDMI-CEC)
	/// and the volume/mute commands are disabled rather than sent.
	/// </summary>
	public bool IsVolumeControlSupported => _deviceManager.IsVolumeControlSupported;

	/// <summary>Gets the command that scans the network for devices.</summary>
	public RelayCommand ScanCommand
		{
		get;
		}

	/// <summary>Gets the command that pairs with <see cref="SelectedDevice"/>.</summary>
	public RelayCommand PairCommand
		{
		get;
		}

	/// <summary>Gets the command that connects to <see cref="SelectedDevice"/> using stored credentials.</summary>
	public RelayCommand ConnectCommand
		{
		get;
		}

	/// <summary>Gets the command that disconnects the current session.</summary>
	public RelayCommand DisconnectCommand
		{
		get;
		}

	/// <summary>Gets the Up remote button command.</summary>
	public RelayCommand UpButton
		{
		get;
		}

	/// <summary>Gets the Down remote button command.</summary>
	public RelayCommand DownButton
		{
		get;
		}

	/// <summary>Gets the Left remote button command.</summary>
	public RelayCommand LeftButton
		{
		get;
		}

	/// <summary>Gets the Right remote button command.</summary>
	public RelayCommand RightButton
		{
		get;
		}

	/// <summary>Gets the Select remote button command.</summary>
	public RelayCommand SelectButton
		{
		get;
		}

	/// <summary>Gets the Menu remote button command.</summary>
	public RelayCommand MenuButton
		{
		get;
		}

	/// <summary>Gets the Home remote button command.</summary>
	public RelayCommand HomeButton
		{
		get;
		}

	/// <summary>Gets the Play/Pause remote button command.</summary>
	public RelayCommand PlayPauseButton
		{
		get;
		}

	/// <summary>Gets the Volume Up remote button command.</summary>
	public RelayCommand VolumeUpButton
		{
		get;
		}

	/// <summary>Gets the Volume Down remote button command.</summary>
	public RelayCommand VolumeDownButton
		{
		get;
		}

	/// <summary>Gets the Siri remote button command.</summary>
	public RelayCommand SiriButton
		{
		get;
		}

	/// <summary>Gets the Mute remote button command.</summary>
	public RelayCommand MuteButton
		{
		get;
		}

	/// <summary>Gets the Power remote button command.</summary>
	public RelayCommand PowerButton
		{
		get;
		}

	/// <summary>Gets a value indicating whether the device is currently considered muted.</summary>
	public bool IsMuted
		{
		get => _isMuted;
		private set => SetProperty (ref _isMuted, value);
		}

	/// <summary>
	/// Gets a value indicating whether the device is currently considered awake, based on the
	/// most recent <see cref="AppleTvDeviceManager.TogglePowerAsync"/> result.
	/// </summary>
	public bool IsAwake
		{
		get => _isAwake;
		private set => SetProperty (ref _isAwake, value);
		}

	/// <summary>
	/// Gets a value indicating whether the device's power state has actually been observed (via
	/// the initial <see cref="AppleTvControlLibrary.Protocol.CompanionApi.Connect"/> fetch or a
	/// pushed status event), as opposed to still being <see cref="SystemStatus.Unknown"/>. The UI
	/// should not present a confident awake/asleep color until this is <see langword="true"/>.
	/// </summary>
	public bool IsPowerStateKnown
		{
		get => _isPowerStateKnown;
		private set => SetProperty (ref _isPowerStateKnown, value);
		}

	/// <summary>
	/// Gets or sets a value indicating whether <see cref="SelectedDevice"/> should be
	/// automatically connected to on the next application startup, for ease of testing.
	/// Setting this persists the choice immediately via
	/// <see cref="AppleTvDeviceManager.SetAutoConnect"/>; only one stored device can have this
	/// set at a time.
	/// </summary>
	public bool AutoConnectSelected
		{
		get => _autoConnectSelected;
		set
			{
			if (SetProperty (ref _autoConnectSelected, value))
				{
				string? uniqueId = value ? SelectedDevice?.Device.UniqueId : null;
				_deviceManager.SetAutoConnect (uniqueId);
				}
			}
		}

	/// <summary>
	/// Invoked when pairing is required and a PIN must be collected from the user. The WPF view
	/// wires this to show <c>PinEntryDialog</c> and return the entered PIN, or <see langword="null"/>
	/// if the user cancelled.
	/// </summary>
	public Func<CompanionDiscoveryResult, int?>? RequestPin
		{
		get;
		set;
		}

	/// <summary>
	/// Invoked when the connected device's on-screen keyboard gains focus and text input should
	/// be presented to the user. The argument is the device's current keyboard text, if any.
	/// The WPF view wires this to show a non-modal <c>TextInputDialog</c>.
	/// </summary>
	public Action<string?>? ShowTextInput
		{
		get;
		set;
		}

	/// <summary>
	/// Invoked when the connected device's on-screen keyboard loses focus and any open text
	/// input dialog should be dismissed.
	/// </summary>
	public Action? HideTextInput
		{
		get;
		set;
		}

	/// <summary>
	/// Forwards a user edit made in the text-input dialog to the connected device, replacing
	/// its virtual keyboard text so the on-screen keyboard mirrors what the user is typing.
	/// </summary>
	/// <param name="text">The dialog's current full text.</param>
	public void OnTextInputChanged (string text)
		{
		_ = SetTextAsync (text);
		}

	private async Task SetTextAsync (string text)
		{
		if (!IsConnected)
			{
			return;
			}

		try
			{
			await _deviceManager.SetTextAsync (text).ConfigureAwait (true);
			}
		catch (Exception ex)
			{
			System.Diagnostics.Debug.WriteLine ($"[AppleTv.Remote.Wpf] SetText failed: {ex}");
			StatusMessage = $"Text input failed: {ex.Message}";
			}
		}

	private async Task ScanAsync ()
		{
		IsBusy = true;
		StatusMessage = "Scanning...";
		try
			{
			var results = await _deviceManager.ScanAsync (TimeSpan.FromSeconds (5)).ConfigureAwait (true);
			Devices.Clear ();
			foreach (CompanionDiscoveryResult device in results)
				{
				bool isPaired = device.UniqueId is not null
					&& _deviceManager.LoadStoredDevice (device.UniqueId) is not null;
				Devices.Add (new DeviceListItem (device, isPaired));
				}

			StatusMessage = $"Found {Devices.Count} device(s).";
			}
		catch (Exception ex)
			{
			System.Diagnostics.Debug.WriteLine ($"[AppleTv.Remote.Wpf] Scan failed: {ex}");
			StatusMessage = $"Scan failed: {ex.Message}";
			}
		finally
			{
			IsBusy = false;
			}
		}

	private async Task PairAsync ()
		{
		if (SelectedDevice is null)
			{
			StatusMessage = "Select a device from the list before pairing.";
			return;
			}

		IsBusy = true;
		StatusMessage = "Starting pairing - waiting for the TV to display a PIN...";
		PairingSession? session = null;
		try
			{
			// M1 must be sent before the Apple TV will display a PIN, so the pairing session is
			// started before the user is ever prompted for one.
			session = await _deviceManager.BeginPairAsync (SelectedDevice.Device).ConfigureAwait (true);

			int? pin = RequestPin?.Invoke (SelectedDevice.Device);
			if (pin is null)
				{
				StatusMessage = "Pairing cancelled.";
				return;
				}

			StatusMessage = "Pairing...";
			StoredDevice stored = await _deviceManager.CompletePairAsync (session, pin.Value).ConfigureAwait (true);
			session = null;
			SelectedDevice.IsPaired = true;
			RaiseCommandStates ();
			StatusMessage = $"Paired with {stored.Name}.";
			}
		catch (Exception ex)
			{
			System.Diagnostics.Debug.WriteLine ($"[AppleTv.Remote.Wpf] Pairing failed: {ex}");
			StatusMessage = $"Pairing failed: {ex.Message}";
			}
		finally
			{
			session?.Transport.Dispose ();
			IsBusy = false;
			}
		}

	private async Task ConnectAsync ()
		{
		if (SelectedDevice?.Device.UniqueId is null)
			{
			StatusMessage = "Selected device has no unique id.";
			return;
			}

		StoredDevice? stored = _deviceManager.LoadStoredDevice (SelectedDevice.Device.UniqueId);
		if (stored is null)
			{
			StatusMessage = "Device is not paired yet.";
			return;
			}

		await ConnectToStoredDeviceAsync (stored).ConfigureAwait (true);
		}

	private async Task ConnectToStoredDeviceAsync (StoredDevice stored)
		{
		IsBusy = true;
		StatusMessage = "Connecting...";
		try
			{
			await _deviceManager.ConnectAsync (stored).ConfigureAwait (true);
			_lastConnectedDevice = stored;
			StatusMessage = $"Connected to {stored.Name}.";
			OnPropertyChanged (nameof (IsConnected));
			DisconnectCommand.RaiseCanExecuteChanged ();
			RaiseRemoteButtonStates ();
			ApplySystemStatus (_deviceManager.CurrentSystemStatus);
			PopulateAppsAndAccounts ();
			}
		catch (Exception ex)
			{
			System.Diagnostics.Debug.WriteLine ($"[AppleTv.Remote.Wpf] Connect failed: {ex}");
			StatusMessage = $"Connect failed: {ex.Message}";
			}
		finally
			{
			IsBusy = false;
			}
		}

	/// <summary>
	/// Performs one-time startup work: if a stored device is marked for auto-connect (see
	/// <see cref="AutoConnectSelected"/>), populates <see cref="Devices"/> and
	/// <see cref="SelectedDevice"/> with it and connects immediately, without requiring a scan
	/// first. Scan/Pair/Connect/Disconnect remain fully usable afterward.
	/// </summary>
	public async Task InitializeAsync ()
		{
		StoredDevice? autoConnect = _deviceManager.LoadAutoConnectDevice ();
		if (autoConnect is null)
			{
			return;
			}

		CompanionDiscoveryResult device = new (
			autoConnect.Name,
			IPAddress.TryParse (autoConnect.Address, out IPAddress? address) ? address : null,
			autoConnect.Port,
			autoConnect.UniqueId,
			CompanionPairingRequirement.Mandatory,
			new Dictionary<string, string> ());

		DeviceListItem item = new (device, isPaired: true);
		Devices.Add (item);

		// Set the backing field directly (rather than the SelectedDevice setter) so the
		// auto-connect flag isn't clobbered by UpdateAutoConnectCheckboxFromSelection before
		// AutoConnectSelected is set to reflect the already-persisted choice below.
		_selectedDevice = item;
		OnPropertyChanged (nameof (SelectedDevice));
		RaiseCommandStates ();
		_autoConnectSelected = true;
		OnPropertyChanged (nameof (AutoConnectSelected));

		await AutoConnectToStoredDeviceAsync (autoConnect).ConfigureAwait (true);
		}

	private async Task AutoConnectToStoredDeviceAsync (StoredDevice stored)
		{
		try
			{
			await ConnectToStoredDeviceAsync (stored).ConfigureAwait (true);
			if (IsConnected)
				{
				return;
				}

			StatusMessage = $"Saved address failed. Looking for {stored.Name}...";
			if (!await _deviceManager.RefreshStoredEndpointAsync (stored, TimeSpan.FromSeconds (5)).ConfigureAwait (true))
				{
				StatusMessage = $"Could not verify a new address for {stored.Name}. Scan and connect manually.";
				return;
				}

			StatusMessage = $"Device found at a new address. Reconnecting...";
			await ConnectToStoredDeviceAsync (stored).ConfigureAwait (true);
			}
		catch (Exception ex)
			{
			System.Diagnostics.Debug.WriteLine ($"[AppleTv.Remote.Wpf] Auto-connect recovery failed: {ex}");
			StatusMessage = $"Auto-connect failed: {ex.Message}";
			}
		}

	// Reflects the persisted AutoConnect flag for whichever stored device is newly selected,
	// without re-persisting anything (selecting a device should not silently change which
	// device auto-connects; only the checkbox itself does that).
	private void UpdateAutoConnectCheckboxFromSelection ()
		{
		string? uniqueId = SelectedDevice?.Device.UniqueId;
		bool isAutoConnect = uniqueId is not null
			&& _deviceManager.LoadStoredDevice (uniqueId) is { AutoConnect: true };
		_autoConnectSelected = isAutoConnect;
		OnPropertyChanged (nameof (AutoConnectSelected));
		}

	private void Disconnect ()
		{
		// A deliberate, user-initiated disconnect cancels any pending auto-reconnect and forgets
		// the last-connected device, so the app doesn't spring back to life on its own afterward.
		CancelReconnect ();
		_lastConnectedDevice = null;

		_deviceManager.Disconnect ();
		StatusMessage = "Disconnected.";
		IsMuted = false;
		IsAwake = false;
		IsPowerStateKnown = false;
		HideTextInput?.Invoke ();
		OnPropertyChanged (nameof (IsConnected));
		DisconnectCommand.RaiseCanExecuteChanged ();
		RaiseRemoteButtonStates ();
		PopulateAppsAndAccounts ();
		}

	// The AppleTvDeviceManager has already torn down its own connection state (transport/api) by
	// the time this fires - see AppleTvDeviceManager.OnConnectionClosed - so this only needs to
	// reset the view-model-level UI state, same as a user-initiated Disconnect().
	private void HandleConnectionClosed (ConnectionClosedEventArgs e)
		{
		StatusMessage = e.Exception is null
			? "Disconnected."
			: $"Connection lost: {e.Exception.Message}. Reconnecting...";
		IsMuted = false;
		IsAwake = false;
		IsPowerStateKnown = false;
		HideTextInput?.Invoke ();
		OnPropertyChanged (nameof (IsConnected));
		DisconnectCommand.RaiseCanExecuteChanged ();
		RaiseRemoteButtonStates ();
		PopulateAppsAndAccounts ();

		// Only an unexpected fault is worth auto-reconnecting for; a clean close (e.g. the user's
		// own Disconnect(), which already cleared _lastConnectedDevice) must not trigger one.
		if (e.Exception is not null && _lastConnectedDevice is StoredDevice device)
			{
			StartReconnectLoop (device);
			}
		}

	private void CancelReconnect ()
		{
		_reconnectCts?.Cancel ();
		_reconnectCts?.Dispose ();
		_reconnectCts = null;
		}

	private void StartReconnectLoop (StoredDevice stored)
		{
		CancelReconnect ();
		CancellationTokenSource cts = new ();
		_reconnectCts = cts;
		_ = ReconnectLoopAsync (stored, cts.Token);
		}

	private async Task ReconnectLoopAsync (StoredDevice stored, CancellationToken cancellationToken)
		{
		for (int attempt = 0; attempt < ReconnectDelays.Length && !cancellationToken.IsCancellationRequested; attempt++)
			{
			TimeSpan delay = ReconnectDelays[attempt];
			StatusMessage = $"Reconnecting to {stored.Name} in {delay.TotalSeconds:0}s (attempt {attempt + 1}/{ReconnectDelays.Length})...";
			try
				{
				await Task.Delay (delay, cancellationToken).ConfigureAwait (true);
				}
			catch (OperationCanceledException)
				{
				return;
				}

			if (cancellationToken.IsCancellationRequested)
				{
				return;
				}

			StatusMessage = $"Reconnecting to {stored.Name}...";
			try
				{
				await ConnectToStoredDeviceAsync (stored).ConfigureAwait (true);
				if (IsConnected)
					{
					return;
					}
				}
			catch (Exception ex)
				{
				System.Diagnostics.Debug.WriteLine ($"[AppleTv.Remote.Wpf] Reconnect attempt {attempt + 1} failed: {ex}");
				}
			}

		if (!cancellationToken.IsCancellationRequested)
			{
			StatusMessage = $"Could not reconnect to {stored.Name}. Connect manually.";
			}
		}

	private async Task ToggleMuteAsync ()
		{
		try
			{
			IsMuted = await _deviceManager.ToggleMuteAsync ().ConfigureAwait (true);
			StatusMessage = IsMuted ? "Muted." : "Unmuted.";
			}
		catch (Exception ex)
			{
			System.Diagnostics.Debug.WriteLine ($"[AppleTv.Remote.Wpf] Mute failed: {ex}");
			StatusMessage = $"Mute failed: {ex.Message}";
			}
		}

	/// <summary>
	/// Forwards a touchpad interaction to the connected device. <paramref name="x"/> and
	/// <paramref name="y"/> are expected to already be normalized to the Companion touch
	/// surface's [0, 1000] coordinate space (see <see cref="TranslateTouchCoordinate"/>).
	/// </summary>
	/// <param name="x">The x coordinate, in the range [0, 1000].</param>
	/// <param name="y">The y coordinate, in the range [0, 1000].</param>
	/// <param name="action">The touch phase.</param>
	public void SendTouchEvent (int x, int y, TouchAction action)
		{
		_ = SendTouchEventAsync (x, y, action);
		}

	private async Task SendTouchEventAsync (int x, int y, TouchAction action)
		{
		if (!IsConnected)
			{
			return;
			}

		try
			{
			await _deviceManager.SendTouchEventAsync (x, y, action).ConfigureAwait (true);
			}
		catch (Exception ex)
			{
			System.Diagnostics.Debug.WriteLine ($"[AppleTv.Remote.Wpf] Touch event failed: {ex}");
			StatusMessage = $"Touch failed: {ex.Message}";
			}
		}

	/// <summary>
	/// Forwards a touchpad tap (click) gesture to the connected device. Unlike
	/// <see cref="SendTouchEvent"/>, this sends the <see cref="HidCommand.Select"/> button
	/// press/release that actually causes tvOS to act on the tap - a touch Press/Release pair
	/// alone does not.
	/// </summary>
	/// <param name="action">The click gesture: single tap, double tap, or press-and-hold.</param>
	// SendTouchClick ultimately sends a "_hidC" command via CompanionApi.SendClick, which blocks on
	// CompanionProtocol.ExchangeOpack (see the remark on TogglePowerAsync above), so this is run on a
	// background thread rather than directly on the calling (UI) thread.
	public async void SendTouchClick (InputAction action)
		{
		if (!IsConnected)
			{
			return;
			}

		try
			{
			await _deviceManager.SendTouchClickAsync (action).ConfigureAwait (true);
			}
		catch (Exception ex)
			{
			System.Diagnostics.Debug.WriteLine ($"[AppleTv.Remote.Wpf] Touch click failed: {ex}");
			StatusMessage = $"Touch click failed: {ex.Message}";
			}
		}

	/// <summary>
	/// Translates a coordinate within a control of size <paramref name="controlWidth"/> x
	/// <paramref name="controlHeight"/> to the Companion touch surface's [0, 1000] space.
	/// </summary>
	/// <param name="position">The offset within the control, in device-independent pixels.</param>
	/// <param name="controlWidth">The control's width, in device-independent pixels.</param>
	/// <param name="controlHeight">The control's height, in device-independent pixels.</param>
	/// <returns>The translated (x, y) coordinate, clamped to [0, 1000].</returns>
	// pyatv/protocols/companion/api.py (TOUCHPAD_WIDTH/TOUCHPAD_HEIGHT) — line 88-89 as of pyatv 0.18.0
	public static (int X, int Y) TranslateTouchCoordinate (Point position, double controlWidth, double controlHeight)
		{
		const double touchpadWidth = 1000.0;
		const double touchpadHeight = 1000.0;

		int x = controlWidth > 0 ? (int)Math.Round (position.X / controlWidth * touchpadWidth) : 0;
		int y = controlHeight > 0 ? (int)Math.Round (position.Y / controlHeight * touchpadHeight) : 0;

		x = Clamp (x, 0, (int)touchpadWidth);
		y = Clamp (y, 0, (int)touchpadHeight);

		return (x, y);
		}

	// Math.Clamp(int, int, int) is not available on net472; Compat.Clamp polyfills it there.
	private static int Clamp (int value, int min, int max)
		{
#if NET472
		return Compat.Clamp (value, min, max);
#else
		return Math.Clamp (value, min, max);
#endif
		}

	// pyatv/protocols/companion/__init__.py (_handle_system_status_update) — line 249-256 as of pyatv 0.18.0: Wake/Sleep
	// HID commands are single fire-and-forget events with no ack, so the real power state must come from the pushed
	// SystemStatus/TVSystemStatus event via ApplySystemStatus(...), not from the command that was just sent. Showing
	// "Waking..."/IsAwake optimistically here left the UI stuck if the device never pushed a confirming status.
	//
	// CompanionApi.SendHidCommand/TogglePower ultimately block on CompanionProtocol.ExchangeOpack, which waits
	// (synchronously) up to ResponseTimeout for a reply. Calling that directly from a RelayCommand blocked the WPF
	// dispatcher thread for the whole timeout whenever the device was slow to respond (e.g. while waking), which
	// froze every other button in the UI, not just the power button. Running the send on a background thread keeps
	// the UI responsive regardless of how long the device takes to answer.
	private async Task TogglePowerAsync ()
		{
		try
			{
			bool requestedWake = await _deviceManager.TogglePowerAsync ().ConfigureAwait (true);
			StatusMessage = requestedWake ? "Waking..." : "Sleeping...";
			}
		catch (Exception ex)
			{
			System.Diagnostics.Debug.WriteLine ($"[AppleTv.Remote.Wpf] Power toggle failed: {ex}");
			StatusMessage = $"Power toggle failed: {ex.Message}";
			}
		}

	private RelayCommand CreateHidCommand (HidCommand command, Func<bool>? canExecute = null)
		{
		return new RelayCommand (
			async () =>
				{
				try
					{
					await _deviceManager.SendHidCommandAsync (command).ConfigureAwait (true);
					}
				catch (Exception ex)
					{
					System.Diagnostics.Debug.WriteLine ($"[AppleTv.Remote.Wpf] Command failed: {ex}");
					StatusMessage = $"Command failed: {ex.Message}";
					}
				},
			canExecute ?? (() => IsConnected));
		}

	private void RaiseCommandStates ()
		{
		ScanCommand.RaiseCanExecuteChanged ();
		PairCommand.RaiseCanExecuteChanged ();
		ConnectCommand.RaiseCanExecuteChanged ();
		}

	private void RaiseRemoteButtonStates ()
		{
		OnPropertyChanged (nameof (IsVolumeControlSupported));
		UpButton.RaiseCanExecuteChanged ();
		DownButton.RaiseCanExecuteChanged ();
		LeftButton.RaiseCanExecuteChanged ();
		RightButton.RaiseCanExecuteChanged ();
		SelectButton.RaiseCanExecuteChanged ();
		MenuButton.RaiseCanExecuteChanged ();
		HomeButton.RaiseCanExecuteChanged ();
		PlayPauseButton.RaiseCanExecuteChanged ();
		VolumeUpButton.RaiseCanExecuteChanged ();
		VolumeDownButton.RaiseCanExecuteChanged ();
		SiriButton.RaiseCanExecuteChanged ();
		MuteButton.RaiseCanExecuteChanged ();
		PowerButton.RaiseCanExecuteChanged ();
		}

	/// <inheritdoc/>
	public void Dispose ()
		{
		CancelReconnect ();
		_deviceManager.Dispose ();
		}
	}
