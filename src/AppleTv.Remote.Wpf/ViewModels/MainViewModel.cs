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
	private CompanionDiscoveryResult? _selectedDevice;
	private string _statusMessage = "Not connected.";
	private bool _isBusy;

	/// <summary>Initializes a new instance of the <see cref="MainViewModel"/> class.</summary>
	public MainViewModel ()
		{
		this._deviceManager = new AppleTvDeviceManager ();

		this.ScanCommand = new RelayCommand (async () => await this.ScanAsync ().ConfigureAwait (true), () => !this.IsBusy);
		this.PairCommand = new RelayCommand (async () => await this.PairAsync ().ConfigureAwait (true), () => !this.IsBusy && this.SelectedDevice is not null);
		this.ConnectCommand = new RelayCommand (async () => await this.ConnectAsync ().ConfigureAwait (true), () => !this.IsBusy && this.SelectedDevice is not null);
		this.DisconnectCommand = new RelayCommand (this.Disconnect, () => this.IsConnected);

		this.UpButton = this.CreateHidCommand (HidCommand.Up);
		this.DownButton = this.CreateHidCommand (HidCommand.Down);
		this.LeftButton = this.CreateHidCommand (HidCommand.Left);
		this.RightButton = this.CreateHidCommand (HidCommand.Right);
		this.SelectButton = this.CreateHidCommand (HidCommand.Select);
		this.MenuButton = this.CreateHidCommand (HidCommand.Menu);
		this.HomeButton = this.CreateHidCommand (HidCommand.Home);
		this.PlayPauseButton = this.CreateHidCommand (HidCommand.PlayPause);
		this.VolumeUpButton = this.CreateHidCommand (HidCommand.VolumeUp);
		this.VolumeDownButton = this.CreateHidCommand (HidCommand.VolumeDown);
		this.SiriButton = this.CreateHidCommand (HidCommand.Siri);
		}

	/// <summary>Gets the discovered devices from the most recent scan.</summary>
	public ObservableCollection<CompanionDiscoveryResult> Devices
		{
		get;
		} = new ();

	/// <summary>Gets or sets the currently selected device.</summary>
	public CompanionDiscoveryResult? SelectedDevice
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
		set => this.SetProperty (ref this._statusMessage, value);
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
				this.Devices.Add (device);
				}

			this.StatusMessage = $"Found {this.Devices.Count} device(s).";
			}
		catch (Exception ex)
			{
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
			return;
			}

		int? pin = this.RequestPin?.Invoke (this.SelectedDevice);
		if (pin is null)
			{
			return;
			}

		this.IsBusy = true;
		this.StatusMessage = "Pairing...";
		try
			{
			StoredDevice stored = await this._deviceManager.PairAsync (this.SelectedDevice, pin.Value).ConfigureAwait (true);
			this.StatusMessage = $"Paired with {stored.Name}.";
			}
		catch (Exception ex)
			{
			this.StatusMessage = $"Pairing failed: {ex.Message}";
			}
		finally
			{
			this.IsBusy = false;
			}
		}

	private async Task ConnectAsync ()
		{
		if (this.SelectedDevice?.UniqueId is null)
			{
			this.StatusMessage = "Selected device has no unique id.";
			return;
			}

		StoredDevice? stored = this._deviceManager.LoadStoredDevice (this.SelectedDevice.UniqueId);
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
			}
		catch (Exception ex)
			{
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
		this.OnPropertyChanged (nameof (this.IsConnected));
		this.DisconnectCommand.RaiseCanExecuteChanged ();
		}

	private RelayCommand CreateHidCommand (HidCommand command)
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
					this.StatusMessage = $"Command failed: {ex.Message}";
					}
				},
			() => this.IsConnected);
		}

	private void RaiseCommandStates ()
		{
		this.ScanCommand.RaiseCanExecuteChanged ();
		this.PairCommand.RaiseCanExecuteChanged ();
		this.ConnectCommand.RaiseCanExecuteChanged ();
		}

	/// <inheritdoc/>
	public void Dispose ()
		{
		this._deviceManager.Dispose ();
		}
	}
