// Copyright (c) 2026 Neil Colvin. Licensed under the MIT License.
// See LICENSE file in the repository root for full license text.

using System;
using System.Collections.ObjectModel;
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

		this.MuteButton = new RelayCommand (this.ToggleMute, () => this.IsConnected && this.IsVolumeControlSupported);
		this.PowerButton = new RelayCommand (this.TogglePower, () => this.IsConnected);

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
		}

	// pyatv/protocols/companion/__init__.py (self._power_state = PowerState.Unknown) — line 213 as of pyatv 0.18.0:
	// SystemStatus.Unknown means no fetch or pushed event has been observed yet, so IsAwake must not
	// be presented as a confident true/false until IsPowerStateKnown is true.
	private void ApplySystemStatus (SystemStatus status)
		{
		this.IsPowerStateKnown = status != SystemStatus.Unknown;
		this.IsAwake = status is not (SystemStatus.Asleep or SystemStatus.Unknown);
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
	/// Invoked when pairing is required and a PIN must be collected from the user. The WPF view
	/// wires this to show <c>PinEntryDialog</c> and return the entered PIN, or <see langword="null"/>
	/// if the user cancelled.
	/// </summary>
	public Func<CompanionDiscoveryResult, int?>? RequestPin
		{
		get;
		set;
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

	private void Disconnect ()
		{
		this._deviceManager.Disconnect ();
		this.StatusMessage = "Disconnected.";
		this.IsMuted = false;
		this.IsAwake = false;
		this.IsPowerStateKnown = false;
		this.OnPropertyChanged (nameof (this.IsConnected));
		this.DisconnectCommand.RaiseCanExecuteChanged ();
		this.RaiseRemoteButtonStates ();
		}

	private void ToggleMute ()
		{
		try
			{
			this.IsMuted = this._deviceManager.ToggleMute ();
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
	public void SendTouchClick (InputAction action)
		{
		if (!this.IsConnected)
			{
			return;
			}

		try
			{
			this._deviceManager.SendTouchClick (action);
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

		x = Math.Clamp (x, 0, (int)touchpadWidth);
		y = Math.Clamp (y, 0, (int)touchpadHeight);

		return (x, y);
		}

	private void TogglePower ()
		{
		try
			{
			this.IsAwake = this._deviceManager.TogglePower ();
			this.IsPowerStateKnown = true;
			this.StatusMessage = this.IsAwake ? "Waking..." : "Sleeping...";
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
			() =>
				{
				try
					{
					this._deviceManager.SendHidCommand (command);
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
