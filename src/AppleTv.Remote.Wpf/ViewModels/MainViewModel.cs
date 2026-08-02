// Copyright (c) 2026 Neil Colvin. Licensed under the MIT License.
// See LICENSE file in the repository root for full license text.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Net;
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

	/// <summary>Initializes a new instance of the <see cref="MainViewModel"/> class.</summary>
	public MainViewModel ()
		{
		this._deviceManager = new AppleTvDeviceManager ();

		this.ScanCommand = new RelayCommand (async () => await this.ScanAsync ().ConfigureAwait (true), () => !this.IsBusy);
		this.PairCommand = new RelayCommand (async () => await this.PairAsync ().ConfigureAwait (true), () => !this.IsBusy && this.SelectedDevice is not null);
		this.ConnectCommand = new RelayCommand (async () => await this.ConnectAsync ().ConfigureAwait (true), () => !this.IsBusy && this.SelectedDevice is { IsPaired: true });
		this.DisconnectCommand = new RelayCommand (this.Disconnect, () => this.IsConnected);

		this.UpButton = this.CreateHidCommand (HidCommand.Up);
		this.DownButton = this.CreateHidCommand (HidCommand.Down);
		this.LeftButton = this.CreateHidCommand (HidCommand.Left);
		this.RightButton = this.CreateHidCommand (HidCommand.Right);
		this.SelectButton = this.CreateHidCommand (HidCommand.Select);
		this.MenuButton = this.CreateHidCommand (HidCommand.Menu);
		this.HomeButton = this.CreateHidCommand (HidCommand.Home);
		this.PlayPauseButton = this.CreateHidCommand (HidCommand.PlayPause);
		this.VolumeUpButton = this.CreateHidCommand (HidCommand.VolumeUp, () => this.IsConnected && this.IsVolumeControlSupported);
		this.VolumeDownButton = this.CreateHidCommand (HidCommand.VolumeDown, () => this.IsConnected && this.IsVolumeControlSupported);
		this.SiriButton = this.CreateHidCommand (HidCommand.Siri);

		this.MuteButton = new RelayCommand (async () => await this.ToggleMuteAsync ().ConfigureAwait (true), () => this.IsConnected && this.IsVolumeControlSupported);
		this.PowerButton = new RelayCommand (async () => await this.TogglePowerAsync ().ConfigureAwait (true), () => this.IsConnected);

		this._deviceManager.MediaControlCapabilitiesChanged += (_, _) =>
			{
			Application.Current?.Dispatcher.Invoke (this.RaiseRemoteButtonStates);
			};

		// pyatv/protocols/companion/__init__.py (_handle_system_status_update) — line 249-256 as of pyatv 0.18.0: power
		// state is tracked from pushed SystemStatus/TVSystemStatus events, not by polling.
		this._deviceManager.SystemStatusChanged += (_, _) =>
			{
			Application.Current?.Dispatcher.Invoke (() =>
				{
				this.ApplySystemStatus (this._deviceManager.CurrentSystemStatus);
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
		this._deviceManager.TextFocusStateChanged += (_, _) =>
			{
			Application.Current?.Dispatcher.BeginInvoke (this.ApplyTextFocusState);
			};
		}

	private void ApplyTextFocusState ()
		{
		if (this._deviceManager.TextFocusState == KeyboardFocusState.Focused)
			{
			string? currentText = null;
			try
				{
				currentText = this._deviceManager.TextGet ();
				}
			catch (Exception ex)
				{
				System.Diagnostics.Debug.WriteLine ($"[AppleTv.Remote.Wpf] TextGet failed: {ex}");
				}

			this.ShowTextInput?.Invoke (currentText);
			}
		else
			{
			this.HideTextInput?.Invoke ();
			}
		}

	// pyatv/protocols/companion/__init__.py (self._power_state = PowerState.Unknown) — line 213 as of pyatv 0.18.0:
	// SystemStatus.Unknown means no fetch or pushed event has been observed yet, so IsAwake must not
	// be presented as a confident true/false until IsPowerStateKnown is true.
	private void ApplySystemStatus (SystemStatus status)
		{
		this.IsPowerStateKnown = status != SystemStatus.Unknown;
		this.IsAwake = status is not (SystemStatus.Asleep or SystemStatus.Unknown);

		// The transient "Waking..."/"Sleeping..." message set by TogglePower() must be replaced once a
		// confirming pushed status arrives, otherwise it is left showing forever even after the real
		// state (IsAwake/IsPowerStateKnown) has updated correctly.
		this.StatusMessage = status switch
			{
			SystemStatus.Unknown => this.StatusMessage,
			SystemStatus.Asleep => "Asleep.",
			_ => "Awake.",
			};
		}

	/// <summary>Gets the discovered devices from the most recent scan.</summary>
	public ObservableCollection<DeviceListItem> Devices
		{
		get;
		} = new ();

	/// <summary>Gets or sets the currently selected device.</summary>
	public DeviceListItem? SelectedDevice
		{
		get => this._selectedDevice;
		set
			{
			if (this.SetProperty (ref this._selectedDevice, value))
				{
				this.RaiseCommandStates ();
				this.UpdateAutoConnectCheckboxFromSelection ();
				}
			}
		}

	/// <summary>Gets or sets a user-facing status message.</summary>
	public string StatusMessage
		{
		get => this._statusMessage;
		set
			{
			if (this.SetProperty (ref this._statusMessage, value))
				{
				System.Diagnostics.Debug.WriteLine ($"[AppleTv.Remote.Wpf] {value}");
				}
			}
		}

	/// <summary>Gets or sets a value indicating whether a scan/pair/connect operation is in progress.</summary>
	public bool IsBusy
		{
		get => this._isBusy;
		set
			{
			if (this.SetProperty (ref this._isBusy, value))
				{
				this.RaiseCommandStates ();
				}
			}
		}

	/// <summary>Gets a value indicating whether a device is currently connected.</summary>
	public bool IsConnected => this._deviceManager.IsConnected;

	/// <summary>
	/// Gets a value indicating whether the connected device currently advertises volume control
	/// support. When <see langword="false"/>, audio is managed outside Companion (e.g. HDMI-CEC)
	/// and the volume/mute commands are disabled rather than sent.
	/// </summary>
	public bool IsVolumeControlSupported => this._deviceManager.IsVolumeControlSupported;

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
		get => this._isMuted;
		private set => this.SetProperty (ref this._isMuted, value);
		}

	/// <summary>
	/// Gets a value indicating whether the device is currently considered awake, based on the
	/// most recent <see cref="AppleTvDeviceManager.TogglePower"/> result.
	/// </summary>
	public bool IsAwake
		{
		get => this._isAwake;
		private set => this.SetProperty (ref this._isAwake, value);
		}

	/// <summary>
	/// Gets a value indicating whether the device's power state has actually been observed (via
	/// the initial <see cref="AppleTvControlLibrary.Protocol.CompanionApi.Connect"/> fetch or a
	/// pushed status event), as opposed to still being <see cref="SystemStatus.Unknown"/>. The UI
	/// should not present a confident awake/asleep color until this is <see langword="true"/>.
	/// </summary>
	public bool IsPowerStateKnown
		{
		get => this._isPowerStateKnown;
		private set => this.SetProperty (ref this._isPowerStateKnown, value);
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
		get => this._autoConnectSelected;
		set
			{
			if (this.SetProperty (ref this._autoConnectSelected, value))
				{
				string? uniqueId = value ? this.SelectedDevice?.Device.UniqueId : null;
				this._deviceManager.SetAutoConnect (uniqueId);
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
		if (!this.IsConnected)
			{
			return;
			}

		try
			{
			this._deviceManager.SetText (text);
			}
		catch (Exception ex)
			{
			System.Diagnostics.Debug.WriteLine ($"[AppleTv.Remote.Wpf] SetText failed: {ex}");
			this.StatusMessage = $"Text input failed: {ex.Message}";
			}
		}

	private async Task ScanAsync ()
		{
		this.IsBusy = true;
		this.StatusMessage = "Scanning...";
		try
			{
			var results = await this._deviceManager.ScanAsync (TimeSpan.FromSeconds (5)).ConfigureAwait (true);
			this.Devices.Clear ();
			foreach (CompanionDiscoveryResult device in results)
				{
				bool isPaired = device.UniqueId is not null
					&& this._deviceManager.LoadStoredDevice (device.UniqueId) is not null;
				this.Devices.Add (new DeviceListItem (device, isPaired));
				}

			this.StatusMessage = $"Found {this.Devices.Count} device(s).";
			}
		catch (Exception ex)
			{
			System.Diagnostics.Debug.WriteLine ($"[AppleTv.Remote.Wpf] Scan failed: {ex}");
			this.StatusMessage = $"Scan failed: {ex.Message}";
			}
		finally
			{
			this.IsBusy = false;
			}
		}

	private async Task PairAsync ()
		{
		if (this.SelectedDevice is null)
			{
			this.StatusMessage = "Select a device from the list before pairing.";
			return;
			}

		this.IsBusy = true;
		this.StatusMessage = "Starting pairing - waiting for the TV to display a PIN...";
		PairingSession? session = null;
		try
			{
			// M1 must be sent before the Apple TV will display a PIN, so the pairing session is
			// started before the user is ever prompted for one.
			session = await this._deviceManager.BeginPairAsync (this.SelectedDevice.Device).ConfigureAwait (true);

			int? pin = this.RequestPin?.Invoke (this.SelectedDevice.Device);
			if (pin is null)
				{
				this.StatusMessage = "Pairing cancelled.";
				return;
				}

			this.StatusMessage = "Pairing...";
			StoredDevice stored = await this._deviceManager.CompletePairAsync (session, pin.Value).ConfigureAwait (true);
			session = null;
			this.SelectedDevice.IsPaired = true;
			this.RaiseCommandStates ();
			this.StatusMessage = $"Paired with {stored.Name}.";
			}
		catch (Exception ex)
			{
			System.Diagnostics.Debug.WriteLine ($"[AppleTv.Remote.Wpf] Pairing failed: {ex}");
			this.StatusMessage = $"Pairing failed: {ex.Message}";
			}
		finally
			{
			session?.Transport.Dispose ();
			this.IsBusy = false;
			}
		}

	private async Task ConnectAsync ()
		{
		if (this.SelectedDevice?.Device.UniqueId is null)
			{
			this.StatusMessage = "Selected device has no unique id.";
			return;
			}

		StoredDevice? stored = this._deviceManager.LoadStoredDevice (this.SelectedDevice.Device.UniqueId);
		if (stored is null)
			{
			this.StatusMessage = "Device is not paired yet.";
			return;
			}

		await this.ConnectToStoredDeviceAsync (stored).ConfigureAwait (true);
		}

	private async Task ConnectToStoredDeviceAsync (StoredDevice stored)
		{
		this.IsBusy = true;
		this.StatusMessage = "Connecting...";
		try
			{
			await this._deviceManager.ConnectAsync (stored).ConfigureAwait (true);
			this.StatusMessage = $"Connected to {stored.Name}.";
			this.OnPropertyChanged (nameof (this.IsConnected));
			this.DisconnectCommand.RaiseCanExecuteChanged ();
			this.RaiseRemoteButtonStates ();
			this.ApplySystemStatus (this._deviceManager.CurrentSystemStatus);
			}
		catch (Exception ex)
			{
			System.Diagnostics.Debug.WriteLine ($"[AppleTv.Remote.Wpf] Connect failed: {ex}");
			this.StatusMessage = $"Connect failed: {ex.Message}";
			}
		finally
			{
			this.IsBusy = false;
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
		StoredDevice? autoConnect = this._deviceManager.LoadAutoConnectDevice ();
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
		this.Devices.Add (item);

		// Set the backing field directly (rather than the SelectedDevice setter) so the
		// auto-connect flag isn't clobbered by UpdateAutoConnectCheckboxFromSelection before
		// AutoConnectSelected is set to reflect the already-persisted choice below.
		this._selectedDevice = item;
		this.OnPropertyChanged (nameof (this.SelectedDevice));
		this.RaiseCommandStates ();
		this._autoConnectSelected = true;
		this.OnPropertyChanged (nameof (this.AutoConnectSelected));

		await this.ConnectToStoredDeviceAsync (autoConnect).ConfigureAwait (true);
		}

	// Reflects the persisted AutoConnect flag for whichever stored device is newly selected,
	// without re-persisting anything (selecting a device should not silently change which
	// device auto-connects; only the checkbox itself does that).
	private void UpdateAutoConnectCheckboxFromSelection ()
		{
		string? uniqueId = this.SelectedDevice?.Device.UniqueId;
		bool isAutoConnect = uniqueId is not null
			&& this._deviceManager.LoadStoredDevice (uniqueId) is { AutoConnect: true };
		this._autoConnectSelected = isAutoConnect;
		this.OnPropertyChanged (nameof (this.AutoConnectSelected));
		}

	private void Disconnect ()
		{
		this._deviceManager.Disconnect ();
		this.StatusMessage = "Disconnected.";
		this.IsMuted = false;
		this.IsAwake = false;
		this.IsPowerStateKnown = false;
		this.HideTextInput?.Invoke ();
		this.OnPropertyChanged (nameof (this.IsConnected));
		this.DisconnectCommand.RaiseCanExecuteChanged ();
		this.RaiseRemoteButtonStates ();
		}

	private async Task ToggleMuteAsync ()
		{
		try
			{
			this.IsMuted = await Task.Run (() => this._deviceManager.ToggleMute ()).ConfigureAwait (true);
			this.StatusMessage = this.IsMuted ? "Muted." : "Unmuted.";
			}
		catch (Exception ex)
			{
			System.Diagnostics.Debug.WriteLine ($"[AppleTv.Remote.Wpf] Mute failed: {ex}");
			this.StatusMessage = $"Mute failed: {ex.Message}";
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
		if (!this.IsConnected)
			{
			return;
			}

		try
			{
			this._deviceManager.SendTouchEvent (x, y, action);
			}
		catch (Exception ex)
			{
			System.Diagnostics.Debug.WriteLine ($"[AppleTv.Remote.Wpf] Touch event failed: {ex}");
			this.StatusMessage = $"Touch failed: {ex.Message}";
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
		if (!this.IsConnected)
			{
			return;
			}

		try
			{
			await Task.Run (() => this._deviceManager.SendTouchClick (action)).ConfigureAwait (true);
			}
		catch (Exception ex)
			{
			System.Diagnostics.Debug.WriteLine ($"[AppleTv.Remote.Wpf] Touch click failed: {ex}");
			this.StatusMessage = $"Touch click failed: {ex.Message}";
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
			bool requestedWake = await Task.Run (() => this._deviceManager.TogglePower ()).ConfigureAwait (true);
			this.StatusMessage = requestedWake ? "Waking..." : "Sleeping...";
			}
		catch (Exception ex)
			{
			System.Diagnostics.Debug.WriteLine ($"[AppleTv.Remote.Wpf] Power toggle failed: {ex}");
			this.StatusMessage = $"Power toggle failed: {ex.Message}";
			}
		}

	private RelayCommand CreateHidCommand (HidCommand command, Func<bool>? canExecute = null)
		{
		return new RelayCommand (
			async () =>
				{
				try
					{
					await Task.Run (() => this._deviceManager.SendHidCommand (command)).ConfigureAwait (true);
					}
				catch (Exception ex)
					{
					System.Diagnostics.Debug.WriteLine ($"[AppleTv.Remote.Wpf] Command failed: {ex}");
					this.StatusMessage = $"Command failed: {ex.Message}";
					}
				},
			canExecute ?? (() => this.IsConnected));
		}

	private void RaiseCommandStates ()
		{
		this.ScanCommand.RaiseCanExecuteChanged ();
		this.PairCommand.RaiseCanExecuteChanged ();
		this.ConnectCommand.RaiseCanExecuteChanged ();
		}

	private void RaiseRemoteButtonStates ()
		{
		this.OnPropertyChanged (nameof (this.IsVolumeControlSupported));
		this.UpButton.RaiseCanExecuteChanged ();
		this.DownButton.RaiseCanExecuteChanged ();
		this.LeftButton.RaiseCanExecuteChanged ();
		this.RightButton.RaiseCanExecuteChanged ();
		this.SelectButton.RaiseCanExecuteChanged ();
		this.MenuButton.RaiseCanExecuteChanged ();
		this.HomeButton.RaiseCanExecuteChanged ();
		this.PlayPauseButton.RaiseCanExecuteChanged ();
		this.VolumeUpButton.RaiseCanExecuteChanged ();
		this.VolumeDownButton.RaiseCanExecuteChanged ();
		this.SiriButton.RaiseCanExecuteChanged ();
		this.MuteButton.RaiseCanExecuteChanged ();
		this.PowerButton.RaiseCanExecuteChanged ();
		}

	/// <inheritdoc/>
	public void Dispose ()
		{
		this._deviceManager.Dispose ();
		}
	}
