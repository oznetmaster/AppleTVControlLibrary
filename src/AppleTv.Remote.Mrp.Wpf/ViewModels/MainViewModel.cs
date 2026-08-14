// Copyright (c) 2026 Neil Colvin. Licensed under the MIT License.
// See LICENSE file in the repository root for full license text.

using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

using AppleTvControlLibrary.Discovery.AirPlay;
using AppleTvControlLibrary.Discovery.Mrp;
using AppleTvControlLibrary.Mrp.PlayerState;
using AppleTvControlLibrary.Mrp.Protobuf;
using AppleTvControlLibrary.Mrp.RemoteControl;
using AppleTvControlLibrary.Remote.Mrp.Wpf.Services;
using AppleTvControlLibrary.Remote.Mrp.Wpf.Storage;

namespace AppleTvControlLibrary.Remote.Mrp.Wpf.ViewModels;

/// <summary>Main window view model: MRP device discovery, pairing/connection state, and remote commands.</summary>
public sealed class MainViewModel : ViewModelBase, IDisposable, IMrpPlayerStateListener
	{
	private readonly MrpDeviceManager _deviceManager;
	private static readonly HttpClient s_iconHttpClient = new ();
	private DeviceListItem? _selectedDevice;
	private string _statusMessage = "Not connected.";
	private bool _isBusy;
	private bool _autoConnectSelected;
	private StoredDevice? _lastConnectedDevice;
	private string? _nowPlayingTitle;
	private string? _nowPlayingSubtitle;
	private string? _nowPlayingTimeline;
	private string? _currentAppName;
	private ImageSource? _artwork;
	private ImageSource? _appIcon;
	private bool _isVolumeControlAvailable;
	private PlaybackState.Types.Enum? _playbackState;
	private readonly DispatcherTimer _timelineTimer;
	private int? _timelineBasePosition;
	private int? _timelineTotalTime;
	private DateTime _timelineBaseTime;
	private int _timelineTicksSinceResync;

	/// <summary>Initializes a new instance of the <see cref="MainViewModel"/> class.</summary>
	public MainViewModel ()
		{
		this._deviceManager = new MrpDeviceManager ();

		this.ScanCommand = new RelayCommand (async () => await this.ScanAsync ().ConfigureAwait (true), () => !this.IsBusy);
		this.PairCommand = new RelayCommand (async () => await this.PairAsync ().ConfigureAwait (true), () => !this.IsBusy && this.SelectedDevice is not null);
		this.ConnectCommand = new RelayCommand (async () => await this.ConnectAsync ().ConfigureAwait (true), () => !this.IsBusy && this.SelectedDevice is { IsPaired: true });
		this.DisconnectCommand = new RelayCommand (this.Disconnect, () => this.IsConnected);

		this.UpButton = this.CreateRemoteCommand (rc => rc.Up ());
		this.DownButton = this.CreateRemoteCommand (rc => rc.Down ());
		this.LeftButton = this.CreateRemoteCommand (rc => rc.Left ());
		this.RightButton = this.CreateRemoteCommand (rc => rc.Right ());
		this.SelectButton = this.CreateRemoteCommand (rc => rc.Select ());
		this.MenuButton = this.CreateRemoteCommand (rc => rc.Menu ());
		this.HomeButton = this.CreateRemoteCommand (rc => rc.Home ());
		this.PlayPauseButton = this.CreateRemoteCommand (rc => rc.PlayPause ());
		this.VolumeUpButton = this.CreateRemoteCommand (rc => rc.VolumeUp ());
		this.VolumeDownButton = this.CreateRemoteCommand (rc => rc.VolumeDown ());
		this.SkipBackwardButton = this.CreateRemoteCommand (rc => rc.SkipBackward ());
		this.RewindButton = this.CreateRemoteCommand (rc => rc.BeginRewind ());
		this.FastForwardButton = this.CreateRemoteCommand (rc => rc.BeginFastForward ());
		this.SkipForwardButton = this.CreateRemoteCommand (rc => rc.SkipForward ());

		this._deviceManager.ConnectionClosed += (_, ex) =>
			{
			Application.Current?.Dispatcher.Invoke (() => this.HandleConnectionClosed (ex));
			};

		// The device only pushes SET_STATE_MESSAGE/UPDATE_CONTENT_ITEM_MESSAGE updates on state
		// transitions (e.g. play/pause, track change). Rather than re-reading MrpPlayerState.Position
		// (and its underlying metadata) every second, advance the displayed timeline locally and only
		// resync from device-reported state on real pushes or periodically (see ResyncTimelineIntervalTicks).
		this._timelineTimer = new DispatcherTimer (DispatcherPriority.Background)
			{
			Interval = TimeSpan.FromSeconds (1),
			};
		this._timelineTimer.Tick += (_, _) => this.TickTimeline ();
		}

	// How often (in 1-second ticks) to resync the local timeline against the device-reported position,
	// to correct for drift without requiring a full state push.
	private const int ResyncTimelineIntervalTicks = 60;

	private void TickTimeline ()
		{
		if (++this._timelineTicksSinceResync >= ResyncTimelineIntervalTicks)
			{
			this._timelineTicksSinceResync = 0;
			MrpPlayerState? playing = this._playerStateManagerSnapshot?.Playing;
			if (playing is not null)
				{
				this.ResyncTimelineBase (playing.Position, playing.TotalTime);
				}
			}

		if (this._timelineBasePosition is not int basePosition)
			{
			return;
			}

		int elapsed = (int)(DateTime.UtcNow - this._timelineBaseTime).TotalSeconds;
		int position = basePosition + elapsed;
		if (this._timelineTotalTime is int totalTime && position > totalTime)
			{
			position = totalTime;
			}

		this.NowPlayingTimeline = FormatTimeline (position, this._timelineTotalTime);
		}

	private void ResyncTimelineBase (int? position, int? totalTime)
		{
		this._timelineBasePosition = position;
		this._timelineTotalTime = totalTime;
		this._timelineBaseTime = DateTime.UtcNow;
		this._timelineTicksSinceResync = 0;
		}

	private void HandleConnectionClosed (Exception? ex)
		{
		this._remoteControlSnapshot = null;
		this.DetachPlayerStateManager ();
		this.StatusMessage = ex is null ? "Connection closed." : $"Connection lost: {ex.Message}";
		this.RaiseCommandStates ();
		this.RaiseRemoteButtonStates ();
		}

	// Cached purely so RaiseRemoteButtonStates can flip CanExecute without re-touching the
	// device manager on every call.
	private MrpRemoteControl? _remoteControlSnapshot;
	private MrpPlayerStateManager? _playerStateManagerSnapshot;
	private bool _initialized;

	/// <summary>Performs one-time startup work: loads the auto-connect device, if any, and connects to it.</summary>
	public async Task InitializeAsync ()
		{
		// Guards against InitializeAsync being invoked more than once (e.g. a duplicate
		// Window.Loaded raise), which would otherwise race a second ConnectAsync against the
		// first and clobber the "Connected" status with an "Already connected" failure.
		if (this._initialized)
			{
			return;
			}

		this._initialized = true;

		StoredDevice? autoConnect = this._deviceManager.LoadAutoConnectDevice ();
		if (autoConnect is null)
			{
			return;
			}

		this.StatusMessage = $"Auto-connecting to {autoConnect.Name}...";
		this.IsBusy = true;
		try
			{
			await this._deviceManager.ConnectAsync (autoConnect).ConfigureAwait (true);
			this._lastConnectedDevice = autoConnect;
			this._remoteControlSnapshot = this._deviceManager.RemoteControl;
			this.AttachPlayerStateManager ();
			this.StatusMessage = $"Connected to {autoConnect.Name}.";
			}
		catch (Exception ex)
			{
			System.Diagnostics.Debug.WriteLine ($"[AppleTv.Remote.Mrp.Wpf] Auto-connect failed: {ex}");
			this.StatusMessage = $"Auto-connect failed: {ex.Message}";
			}
		finally
			{
			this.IsBusy = false;
			this.RaiseCommandStates ();
			this.RaiseRemoteButtonStates ();
			}
		}

	private async Task ScanAsync ()
		{
		this.IsBusy = true;
		this.StatusMessage = "Scanning...";
		try
			{
			IReadOnlyList<AirPlayDiscoveryResult> airPlayScan = await this._deviceManager.ScanAirPlayAsync (TimeSpan.FromSeconds (5)).ConfigureAwait (true);

			this.Devices.Clear ();
			foreach (AirPlayDiscoveryResult device in airPlayScan)
				{
				// pyatv/support/device_info.py (_MODEL_LIST, lookup_model) — only devices whose "model"
				// TXT record matches the Apple TV identifier pattern are Apple TVs; other AirPlay
				// endpoints (e.g. AV receivers, speakers) are not relevant to this application.
				if (!AirPlayServiceInfo.IsAppleTv (device.Properties))
					{
					System.Diagnostics.Debug.WriteLine (
						$"[AppleTv.Remote.Mrp.Wpf] Scan result skipped (not an Apple TV): Name='{device.Name}' " +
						$"Model='{(device.Properties.TryGetValue ("model", out string? m) ? m : "(none)")}'");
					continue;
					}

				StoredDevice? matched = device.UniqueId is not null ? this._deviceManager.LoadStoredDevice (device.UniqueId) : null;
				bool isPaired = matched is not null;
				System.Diagnostics.Debug.WriteLine (
					$"[AppleTv.Remote.Mrp.Wpf] Scan result: Name='{device.Name}' UniqueId='{device.UniqueId ?? "(null)"}' " +
					$"IsPaired={isPaired} StoredMatchName='{matched?.Name ?? "(none)"}'");
				this.Devices.Add (DeviceListItem.FromAirPlay (device, isPaired));
				}

			this.StatusMessage = $"Found {this.Devices.Count} device(s).";
			}
		catch (Exception ex)
			{
			System.Diagnostics.Debug.WriteLine ($"[AppleTv.Remote.Mrp.Wpf] Scan failed: {ex}");
			this.StatusMessage = $"Scan failed: {ex.Message}";
			}
		finally
			{
			this.IsBusy = false;
			this.RaiseCommandStates ();
			}
		}

	private async Task PairAsync ()
		{
		if (this.SelectedDevice is null)
			{
			return;
			}

		DeviceListItem selected = this.SelectedDevice;
		if (selected.UniqueId is null)
			{
			this.StatusMessage = "Device has no unique id; cannot pair.";
			return;
			}

		this.IsBusy = true;
		this.StatusMessage = $"Pairing with {selected.DisplayName}...";
		try
			{
			StoredDevice stored;
			await this._deviceManager.BeginPairAirPlayAsync (selected.AirPlayDevice).ConfigureAwait (true);

			int? airPlayPin = this.RequestPin?.Invoke (selected.DisplayName);
			if (airPlayPin is null)
				{
				this.StatusMessage = "Pairing cancelled.";
				return;
				}

			stored = await this._deviceManager.CompletePairAirPlayAsync (airPlayPin.Value).ConfigureAwait (true);

			selected.IsPaired = true;
			this.StatusMessage = $"Paired with {stored.Name}.";
			}
		catch (Exception ex)
			{
			System.Diagnostics.Debug.WriteLine ($"[AppleTv.Remote.Mrp.Wpf] Pairing failed: {ex}");
			this.StatusMessage = $"Pairing failed: {ex.Message}";
			}
		finally
			{
			this.IsBusy = false;
			this.RaiseCommandStates ();
			}
		}

	private async Task ConnectAsync ()
		{
		if (this.SelectedDevice?.UniqueId is not string uniqueId)
			{
			return;
			}

		StoredDevice? stored = this._deviceManager.LoadStoredDevice (uniqueId);
		if (stored is null)
			{
			this.StatusMessage = "No stored credentials for this device; pair first.";
			return;
			}

		this.IsBusy = true;
		this.StatusMessage = $"Connecting to {stored.Name}...";
		try
			{
			await this._deviceManager.ConnectAsync (stored).ConfigureAwait (true);
			this._lastConnectedDevice = stored;
			this._remoteControlSnapshot = this._deviceManager.RemoteControl;
			this.AttachPlayerStateManager ();
			this.StatusMessage = $"Connected to {stored.Name}.";
			}
		catch (Exception ex)
			{
			System.Diagnostics.Debug.WriteLine ($"[AppleTv.Remote.Mrp.Wpf] Connect failed: {ex}");
			this.StatusMessage = $"Connect failed: {ex.Message}";
			}
		finally
			{
			this.IsBusy = false;
			this.RaiseCommandStates ();
			this.RaiseRemoteButtonStates ();
			}
		}

	private void Disconnect ()
		{
		this._deviceManager.Disconnect ();
		this._remoteControlSnapshot = null;
		this.DetachPlayerStateManager ();
		this.StatusMessage = "Disconnected.";
		this.RaiseCommandStates ();
		this.RaiseRemoteButtonStates ();
		}

	private void AttachPlayerStateManager ()
		{
		this._playerStateManagerSnapshot = this._deviceManager.PlayerStateManager;
		if (this._playerStateManagerSnapshot is not null)
			{
			this._playerStateManagerSnapshot.Listener = this;
			}

		this.UpdateNowPlaying ();
		}

	private void DetachPlayerStateManager ()
		{
		if (this._playerStateManagerSnapshot is not null)
			{
			this._playerStateManagerSnapshot.Listener = null;
			}

		this._playerStateManagerSnapshot = null;
		this._timelineTimer.Stop ();
		this._timelineBasePosition = null;
		this._timelineTotalTime = null;
		this._timelineTicksSinceResync = 0;
		this.NowPlayingTitle = null;
		this.NowPlayingSubtitle = null;
		this.NowPlayingTimeline = null;
		this.Artwork = null;
		this._currentArtworkItemIdentifier = null;
		this.CurrentAppName = null;
		this.AppIcon = null;
		this._currentIconUrl = null;
		this.IsVolumeControlAvailable = false;
		}

	/// <inheritdoc/>
	// pyatv/protocols/mrp/player_state.py (PlayerStateManager.listener) — line 202-216 as of pyatv 0.18.0:
	// invoked whenever relevant player state changes; this is the WPF now-playing/artwork trigger.
	public void StateUpdated ()
		{
		Application.Current?.Dispatcher.Invoke (this.UpdateNowPlaying);
		}

	private int _artworkRequestGeneration;
	private int _iconRequestGeneration;
	private string? _currentArtworkItemIdentifier;

	private void UpdateNowPlaying ()
		{
		MrpPlayerState? playing = this._playerStateManagerSnapshot?.Playing;
		ContentItemMetadata? metadata = playing?.Metadata;

		// pyatv/protocols/mrp/__init__.py (MrpMetadata.app) — line 616-621 as of pyatv 0.18.0: the current
		// app's name comes from the active client's display_name; there is no icon/logo in the MRP protocol
		// at the client level, only bundle_identifier and display_name.
		this.CurrentAppName = this._playerStateManagerSnapshot?.Client?.DisplayName;

		// NowPlayingPlayer.proto (field 7: iconURL), which pyatv 0.18.0 receives but never reads. There is
		// no reference fetch/decode behavior to port, so the URL is fetched directly here. Confirmed via
		// live capture that many devices/apps never populate this field at all; when that is the case the
		// icon slot in the UI is expected to stay empty rather than indicating a pipeline failure.
		string? iconUrl = playing?.Parent?.ActivePlayer.IconUrl;
		if (!string.Equals (iconUrl, this._currentIconUrl, StringComparison.Ordinal))
			{
			System.Diagnostics.Debug.WriteLine (
				$"[AppleTv.Remote.Mrp.Wpf] App icon URL changed: \"{this._currentIconUrl ?? "(none)"}\" -> \"{iconUrl ?? "(none)"}\"");

			this._currentIconUrl = iconUrl;
			this.AppIcon = null;
			if (iconUrl is { Length: > 0 })
				{
				this.RequestAppIconAsync (iconUrl);
				}
			}

		this.NowPlayingTitle = metadata?.Title;
		this.NowPlayingSubtitle = metadata is null
			? null
			: string.Join (" — ", new[] { metadata.TrackArtistName, metadata.AlbumName }.Where (s => !string.IsNullOrEmpty (s)));

		// pyatv/protocols/mrp/__init__.py (build_playing_instance) — line 158-227 as of pyatv 0.18.0:
		// device_state / position / total_time drive the play-state UI and the timeline text.
		this.PlaybackStateValue = playing?.PlaybackStateValue;

		int? position = playing?.Position;
		int? totalTime = playing?.TotalTime;
		this.NowPlayingTimeline = FormatTimeline (position, totalTime);
		this.ResyncTimelineBase (position, totalTime);

		// Only tick locally while actually playing; paused/stopped positions don't advance and
		// are already kept in sync by push updates (e.g. the pause itself, or a seek).
		if (playing?.PlaybackStateValue == PlaybackState.Types.Enum.Playing)
			{
			if (!this._timelineTimer.IsEnabled)
				{
				this._timelineTimer.Start ();
				}
			}
		else
			{
			this._timelineTimer.Stop ();
			}

		ContentItem? currentItem = playing is not null && playing.Items.Count > playing.Location
			? playing.Items[playing.Location]
			: null;
		byte[]? artworkData = currentItem?.ArtworkData?.ToByteArray ();
		string? currentItemIdentifier = currentItem?.Identifier;

		if (artworkData is { Length: > 0 })
			{
			// New artwork bytes were pushed (e.g. a genuine track/app change); decode and display them.
			this.Artwork = DecodeArtwork (artworkData);
			this._currentArtworkItemIdentifier = currentItemIdentifier;
			}
		else if (!string.Equals (currentItemIdentifier, this._currentArtworkItemIdentifier, StringComparison.Ordinal))
			{
			// The content item actually changed (or there is no longer a current item) and no artwork
			// bytes were included in this push; clear the stale artwork until a fetch repopulates it.
			this.Artwork = null;
			this._currentArtworkItemIdentifier = currentItemIdentifier;
			}

		// Otherwise, the same item is still current but this particular push (e.g. a pause/play
		// state update) simply did not carry artwork bytes with it — keep showing the artwork that
		// is already displayed instead of flickering it out and back in.

		// pyatv/protocols/mrp/__init__.py (MrpAudio.is_available) — line 772-775 as of pyatv 0.18.0:
		// hide volume controls when the device reports volume control as unavailable, mirroring the
		// Companion app's capability-gated volume UI.
		this.IsVolumeControlAvailable = this._playerStateManagerSnapshot?.VolumeControlAvailable ?? false;

		// pyatv/protocols/mrp/__init__.py (MrpMetadata._fetch_local_artwork) — line 578-591 as of pyatv 0.18.0:
		// artwork is not always included in passively-pushed content items, so explicitly request it
		// via PLAYBACK_QUEUE_REQUEST_MESSAGE when not already present.
		if (this.Artwork is null && metadata is { ArtworkAvailable: true } && this._remoteControlSnapshot is not null)
			{
			this.RequestArtworkAsync (this._remoteControlSnapshot);
			}
		}

	private async void RequestArtworkAsync (MrpRemoteControl remoteControl)
		{
		int generation = ++this._artworkRequestGeneration;
		try
			{
			(byte[] Data, string? MimeType)? artwork = await remoteControl.GetArtworkAsync ().ConfigureAwait (true);
			if (artwork is null || generation != this._artworkRequestGeneration)
				{
				return;
				}

			this.Artwork = DecodeArtwork (artwork.Value.Data);
			}
		catch (Exception ex)
			{
			System.Diagnostics.Debug.WriteLine ($"[AppleTv.Remote.Mrp.Wpf] Failed to fetch artwork: {ex}");
			}
		}

	private string? _currentIconUrl;

	private async void RequestAppIconAsync (string iconUrl)
		{
		int generation = ++this._iconRequestGeneration;
		System.Diagnostics.Debug.WriteLine ($"[AppleTv.Remote.Mrp.Wpf] Fetching app icon from \"{iconUrl}\" (generation {generation})...");
		try
			{
			byte[] data = await s_iconHttpClient.GetByteArrayAsync (iconUrl).ConfigureAwait (true);
			if (generation != this._iconRequestGeneration)
				{
				System.Diagnostics.Debug.WriteLine (
					$"[AppleTv.Remote.Mrp.Wpf] App icon fetch for \"{iconUrl}\" (generation {generation}) completed but was superseded; discarding.");
				return;
				}

			this.AppIcon = DecodeArtwork (data);
			System.Diagnostics.Debug.WriteLine (
				$"[AppleTv.Remote.Mrp.Wpf] App icon fetch for \"{iconUrl}\" succeeded ({data.Length} bytes); decoded: {this.AppIcon is not null}.");
			}
		catch (Exception ex)
			{
			System.Diagnostics.Debug.WriteLine ($"[AppleTv.Remote.Mrp.Wpf] Failed to fetch app icon from \"{iconUrl}\": {ex}");
			}
		}

	private static string? FormatTimeline (int? position, int? totalTime)
		{
		if (position is null && totalTime is null)
			{
			return null;
			}

		static string FormatSeconds (int seconds)
			{
			seconds = Math.Max (seconds, 0);
			TimeSpan span = TimeSpan.FromSeconds (seconds);
			return span.Hours > 0
				? span.ToString (@"h\:mm\:ss")
				: span.ToString (@"m\:ss");
			}

		if (position is not null && totalTime is not null)
			{
			return $"{FormatSeconds (position.Value)} / {FormatSeconds (totalTime.Value)}";
			}

		if (position is not null)
			{
			return FormatSeconds (position.Value);
			}

		return FormatSeconds (totalTime!.Value);
		}

	private static ImageSource? DecodeArtwork (byte[] jpegData)
		{
		try
			{
			using MemoryStream stream = new MemoryStream (jpegData);
			BitmapImage image = new BitmapImage ();
			image.BeginInit ();
			image.CacheOption = BitmapCacheOption.OnLoad;
			image.StreamSource = stream;
			image.EndInit ();
			image.Freeze ();
			return image;
			}
		catch (Exception ex)
			{
			System.Diagnostics.Debug.WriteLine ($"[AppleTv.Remote.Mrp.Wpf] Failed to decode artwork: {ex}");
			return null;
			}
		}

	private RelayCommand CreateRemoteCommand (Func<MrpRemoteControl, Task> action)
		{
		return new RelayCommand (
			async () =>
				{
				MrpRemoteControl? rc = this._deviceManager.RemoteControl;
				if (rc is null)
					{
					return;
					}

				try
					{
					await action (rc).ConfigureAwait (true);
					}
				catch (Exception ex)
					{
					System.Diagnostics.Debug.WriteLine ($"[AppleTv.Remote.Mrp.Wpf] Remote command failed: {ex}");
					this.StatusMessage = $"Command failed: {ex.Message}";
					}
				},
			() => this.IsConnected);
		}

	private void RaiseCommandStates ()
		{
		((RelayCommand)this.ScanCommand).RaiseCanExecuteChanged ();
		((RelayCommand)this.PairCommand).RaiseCanExecuteChanged ();
		((RelayCommand)this.ConnectCommand).RaiseCanExecuteChanged ();
		((RelayCommand)this.DisconnectCommand).RaiseCanExecuteChanged ();
		this.OnPropertyChanged (nameof (this.IsConnected));
		}

	private void RaiseRemoteButtonStates ()
		{
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
		this.SkipBackwardButton.RaiseCanExecuteChanged ();
		this.RewindButton.RaiseCanExecuteChanged ();
		this.FastForwardButton.RaiseCanExecuteChanged ();
		this.SkipForwardButton.RaiseCanExecuteChanged ();
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

	/// <summary>Gets or sets the status message shown to the user.</summary>
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

	/// <summary>Gets or sets whether the currently selected device should auto-connect on startup.</summary>
	public bool AutoConnectSelected
		{
		get => this._autoConnectSelected;
		set
			{
			if (this.SetProperty (ref this._autoConnectSelected, value))
				{
				this._deviceManager.SetAutoConnect (value ? this._lastConnectedDevice?.UniqueId ?? this.SelectedDevice?.UniqueId : null);
				}
			}
		}

	/// <summary>Invoked to request a PIN from the user during pairing, given the device's display name. Set by the view.</summary>
	public Func<string, int?>? RequestPin
		{
		get;
		set;
		}

	/// <summary>Gets the command that scans for devices.</summary>
	public RelayCommand ScanCommand
		{
		get;
		}

	/// <summary>Gets the command that pairs with the selected device.</summary>
	public RelayCommand PairCommand
		{
		get;
		}

	/// <summary>Gets the command that connects to the selected (already-paired) device.</summary>
	public RelayCommand ConnectCommand
		{
		get;
		}

	/// <summary>Gets the command that disconnects from the connected device.</summary>
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

	/// <summary>Gets the skip-backward remote button command.</summary>
	public RelayCommand SkipBackwardButton
		{
		get;
		}

	/// <summary>Gets the rewind remote button command.</summary>
	public RelayCommand RewindButton
		{
		get;
		}

	/// <summary>Gets the fast-forward remote button command.</summary>
	public RelayCommand FastForwardButton
		{
		get;
		}

	/// <summary>Gets the skip-forward remote button command.</summary>
	public RelayCommand SkipForwardButton
		{
		get;
		}

	/// <summary>Gets the title of the currently playing item, if any.</summary>
	public string? NowPlayingTitle
		{
		get => this._nowPlayingTitle;
		private set => this.SetProperty (ref this._nowPlayingTitle, value);
		}

	/// <summary>Gets the display name of the app currently driving now-playing, if any.</summary>
	// pyatv/protocols/mrp/__init__.py (MrpMetadata.app) — line 616-621 as of pyatv 0.18.0: App only exposes
	// name/identifier (bundle_identifier); MRP has no icon/logo asset for the running app.
	public string? CurrentAppName
		{
		get => this._currentAppName;
		private set => this.SetProperty (ref this._currentAppName, value);
		}

	/// <summary>Gets the artist/album subtitle of the currently playing item, if any.</summary>
	public string? NowPlayingSubtitle
		{
		get => this._nowPlayingSubtitle;
		private set => this.SetProperty (ref this._nowPlayingSubtitle, value);
		}

	/// <summary>Gets the formatted position / total-time text for the currently playing item, if any.</summary>
	public string? NowPlayingTimeline
		{
		get => this._nowPlayingTimeline;
		private set => this.SetProperty (ref this._nowPlayingTimeline, value);
		}

	/// <summary>Gets the current playback state reported by the device, if any.</summary>
	// pyatv/protocols/mrp/protobuf/Common.proto — PlaybackState: Playing = 1, Paused = 2, Stopped = 3, Seeking = 5.
	public PlaybackState.Types.Enum? PlaybackStateValue
		{
		get => this._playbackState;
		private set
			{
			if (this.SetProperty (ref this._playbackState, value))
				{
				this.OnPropertyChanged (nameof (this.IsPlaying));
				this.OnPropertyChanged (nameof (this.IsSeeking));
				}
			}
		}

	/// <summary>Gets a value indicating whether the device is currently playing, for play/pause button styling.</summary>
	public bool IsPlaying => this._playbackState == PlaybackState.Types.Enum.Playing;

	/// <summary>Gets a value indicating whether the device is currently seeking, for rewind/fast-forward button styling.</summary>
	// pyatv/protocols/mrp/__init__.py (device_state) — line 174-183 as of pyatv 0.18.0: PlaybackState.Seeking
	// maps to DeviceState.Seeking, which is the only signal MRP gives that a rewind/fast-forward is in progress
	// (it does not report which direction, so both buttons share this state).
	public bool IsSeeking => this._playbackState == PlaybackState.Types.Enum.Seeking;

	/// <summary>Gets the decoded artwork of the currently playing item, if any.</summary>
	public ImageSource? Artwork
		{
		get => this._artwork;
		private set => this.SetProperty (ref this._artwork, value);
		}

	/// <summary>
	/// Gets the decoded icon for the currently active player, fetched from
	/// <see cref="MrpPlayerState.IconUrl"/>, if any.
	/// </summary>
	public ImageSource? AppIcon
		{
		get => this._appIcon;
		private set => this.SetProperty (ref this._appIcon, value);
		}

	/// <summary>Gets a value indicating whether the connected device supports volume control.</summary>
	public bool IsVolumeControlAvailable
		{
		get => this._isVolumeControlAvailable;
		private set => this.SetProperty (ref this._isVolumeControlAvailable, value);
		}

	/// <inheritdoc/>
	public void Dispose ()
		{
		this._timelineTimer.Stop ();
		this._deviceManager.Dispose ();
		}
	}
