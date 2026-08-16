// Copyright (c) 2026 Neil Colvin. Licensed under the MIT License.
// See LICENSE file in the repository root for full license text.

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

using AppleTvControlLibrary.Mrp.Auth;
using AppleTvControlLibrary.Mrp.PlayerState;
using AppleTvControlLibrary.Mrp.Protobuf;
using AppleTvControlLibrary.Mrp.Protocol;

namespace AppleTvControlLibrary.Mrp.RemoteControl;

/// <summary>
/// Repeat mode, mirroring pyatv's <c>RepeatState</c> constant.
/// </summary>
// pyatv/const.py (RepeatState) — line 75-85 as of pyatv 0.18.0
public enum RepeatState
	{
	/// <summary>Repeat is off. pyatv/const.py — line 78 as of pyatv 0.18.0</summary>
	Off = 0,
	/// <summary>Repeat current track or item. pyatv/const.py — line 81 as of pyatv 0.18.0</summary>
	Track = 1,
	/// <summary>Repeat all tracks or items. pyatv/const.py — line 84 as of pyatv 0.18.0</summary>
	All = 2,
	}

/// <summary>
/// Shuffle mode, mirroring pyatv's <c>ShuffleState</c> constant.
/// </summary>
// pyatv/const.py (ShuffleState) — line 88-98 as of pyatv 0.18.0
public enum ShuffleState
	{
	/// <summary>Shuffle is off. pyatv/const.py — line 91 as of pyatv 0.18.0</summary>
	Off = 0,
	/// <summary>Shuffle on album level. pyatv/const.py — line 94 as of pyatv 0.18.0</summary>
	Albums = 1,
	/// <summary>Shuffle on song level. pyatv/const.py — line 97 as of pyatv 0.18.0</summary>
	Songs = 2,
	}

/// <summary>
/// Type of input when pressing a button, mirroring pyatv's <c>InputAction</c> constant.
/// </summary>
// pyatv/const.py (InputAction) — line 200-210 as of pyatv 0.18.0
public enum InputAction
	{
	/// <summary>Press and release quickly. pyatv/const.py — line 203 as of pyatv 0.18.0</summary>
	SingleTap = 0,
	/// <summary>Press and release twice quickly. pyatv/const.py — line 206 as of pyatv 0.18.0</summary>
	DoubleTap = 1,
	/// <summary>Press and hold for one second before releasing. pyatv/const.py — line 209 as of pyatv 0.18.0</summary>
	Hold = 2,
	}

/// <summary>
/// Raised when a SEND_COMMAND_MESSAGE exchange completes with a SendError other than NoError.
/// </summary>
/// <remarks>Initializes a new instance of the <see cref="MrpCommandException"/> class.</remarks>
/// <param name="message">The exception message.</param>
// pyatv/exceptions.py (CommandError) — line 36-38 as of pyatv 0.18.0
public class MrpCommandException (string message) : Exception(message)
	{
	}

/// <summary>
/// Implementation of the MRP remote-control command surface: directional/menu/select HID keys,
/// playback transport commands, seek, repeat/shuffle, and absolute volume.
/// </summary>
/// <remarks>Initializes a new instance of the <see cref="MrpRemoteControl"/> class.</remarks>
/// <param name="protocol">The MRP protocol instance used to send/receive messages.</param>
/// <param name="playerStateManager">The player state manager used to look up command capabilities.</param>
/// <param name="httpClient">
/// The HTTP client used to fetch remote artwork (e.g. iTunes-hosted artwork referenced by
/// <c>artworkIdentifier</c>/<c>artworkURL</c>). If not supplied, a default instance is created.
/// </param>
// pyatv/protocols/mrp/__init__.py (MrpRemoteControl) — line 328-479 as of pyatv 0.18.0
public sealed class MrpRemoteControl (MrpProtocol protocol, MrpPlayerStateManager playerStateManager, HttpClient? httpClient = null)
	{
	// pyatv/protocols/mrp/__init__.py (_DEFAULT_SKIP_TIME) — line 75 as of pyatv 0.18.0
	private const int DEFAULT_SKIP_TIME = 15;

	// pyatv/protocols/mrp/__init__.py (_KEY_LOOKUP) — line 78-96 as of pyatv 0.18.0
	private static readonly Dictionary<string, (int UsePage, int Usage)> _keyLookup = new ()
		{
		{ "up", (1, 0x8C) },
		{ "down", (1, 0x8D) },
		{ "left", (1, 0x8B) },
		{ "right", (1, 0x8A) },
		{ "stop", (12, 0xB7) },
		{ "next", (12, 0xB5) },
		{ "previous", (12, 0xB6) },
		{ "select", (1, 0x89) },
		{ "menu", (1, 0x86) },
		{ "topmenu", (12, 0x60) },
		{ "home", (12, 0x40) },
		{ "suspend", (1, 0x82) },
		{ "wakeup", (1, 0x83) },
		{ "volume_up", (12, 0xE9) },
		{ "volume_down", (12, 0xEA) },
		};

	private readonly MrpProtocol _protocol = protocol;
	private readonly MrpPlayerStateManager _playerStateManager = playerStateManager;
	private readonly HttpClient _httpClient = httpClient ?? new HttpClient ();

	// pyatv/protocols/mrp/__init__.py (MrpMetadata.__init__ / self.artwork_cache = Cache(limit=4)) — line 485-497 as of pyatv 0.18.0
	private const int ARTWORK_CACHE_LIMIT = 4;
	private readonly Dictionary<string, (byte[] Data, string? MimeType)?> _artworkCache = [];
	private readonly List<string> _artworkCacheOrder = [];

	/// <summary>Press key up.</summary>
	/// <param name="action">The type of press to perform.</param>
	/// <param name="cancellationToken">A token that cancels the operation.</param>
	// pyatv/protocols/mrp/__init__.py (up) — line 356-358 as of pyatv 0.18.0
	public Task Up (InputAction action = InputAction.SingleTap, CancellationToken cancellationToken = default) => SendHidKeyAsync ("up", action, cancellationToken);

	/// <summary>Press key down.</summary>
	/// <param name="action">The type of press to perform.</param>
	/// <param name="cancellationToken">A token that cancels the operation.</param>
	// pyatv/protocols/mrp/__init__.py (down) — line 360-362 as of pyatv 0.18.0
	public Task Down (InputAction action = InputAction.SingleTap, CancellationToken cancellationToken = default) => SendHidKeyAsync ("down", action, cancellationToken);

	/// <summary>Press key left.</summary>
	/// <param name="action">The type of press to perform.</param>
	/// <param name="cancellationToken">A token that cancels the operation.</param>
	// pyatv/protocols/mrp/__init__.py (left) — line 364-366 as of pyatv 0.18.0
	public Task Left (InputAction action = InputAction.SingleTap, CancellationToken cancellationToken = default) => SendHidKeyAsync ("left", action, cancellationToken);

	/// <summary>Press key right.</summary>
	/// <param name="action">The type of press to perform.</param>
	/// <param name="cancellationToken">A token that cancels the operation.</param>
	// pyatv/protocols/mrp/__init__.py (right) — line 368-370 as of pyatv 0.18.0
	public Task Right (InputAction action = InputAction.SingleTap, CancellationToken cancellationToken = default) => SendHidKeyAsync ("right", action, cancellationToken);

	/// <summary>Press key select.</summary>
	/// <param name="action">The type of press to perform.</param>
	/// <param name="cancellationToken">A token that cancels the operation.</param>
	// pyatv/protocols/mrp/__init__.py (select) — line 405-407 as of pyatv 0.18.0
	public Task Select (InputAction action = InputAction.SingleTap, CancellationToken cancellationToken = default) => SendHidKeyAsync ("select", action, cancellationToken);

	/// <summary>Press key menu.</summary>
	/// <param name="action">The type of press to perform.</param>
	/// <param name="cancellationToken">A token that cancels the operation.</param>
	// pyatv/protocols/mrp/__init__.py (menu) — line 409-411 as of pyatv 0.18.0
	public Task Menu (InputAction action = InputAction.SingleTap, CancellationToken cancellationToken = default) => SendHidKeyAsync ("menu", action, cancellationToken);

	/// <summary>Press key home.</summary>
	/// <param name="action">The type of press to perform.</param>
	/// <param name="cancellationToken">A token that cancels the operation.</param>
	// pyatv/protocols/mrp/__init__.py (home) — line 421-423 as of pyatv 0.18.0
	public Task Home (InputAction action = InputAction.SingleTap, CancellationToken cancellationToken = default) => SendHidKeyAsync ("home", action, cancellationToken);

	/// <summary>Hold key home.</summary>
	/// <param name="cancellationToken">A token that cancels the operation.</param>
	// pyatv/protocols/mrp/__init__.py (home_hold) — line 425-427 as of pyatv 0.18.0
	public Task HomeHold (CancellationToken cancellationToken = default) => SendHidKeyAsync ("home", InputAction.Hold, cancellationToken);

	/// <summary>Go to main menu (long press menu).</summary>
	/// <param name="cancellationToken">A token that cancels the operation.</param>
	// pyatv/protocols/mrp/__init__.py (top_menu) — line 429-431 as of pyatv 0.18.0
	public Task TopMenu (CancellationToken cancellationToken = default) => SendHidKeyAsync ("topmenu", InputAction.SingleTap, cancellationToken);

	/// <summary>Suspend the device.</summary>
	/// <param name="cancellationToken">A token that cancels the operation.</param>
	// pyatv/protocols/mrp/__init__.py (suspend) — line 433-435 as of pyatv 0.18.0
	public Task Suspend (CancellationToken cancellationToken = default) => SendHidKeyAsync ("suspend", InputAction.SingleTap, cancellationToken);

	/// <summary>Wake up the device.</summary>
	/// <param name="cancellationToken">A token that cancels the operation.</param>
	// pyatv/protocols/mrp/__init__.py (wakeup) — line 437-439 as of pyatv 0.18.0
	public Task Wakeup (CancellationToken cancellationToken = default) => SendHidKeyAsync ("wakeup", InputAction.SingleTap, cancellationToken);

	/// <summary>
	/// Turn the device on. Unlike <see cref="Wakeup"/>, which sends a <c>"wakeup"</c>
	/// <c>SEND_HID_EVENT_MESSAGE</c> (HID usage-page/usage pair (1, 0x83), not a HID-protocol
	/// transport), this sends the dedicated <c>WAKE_DEVICE_MESSAGE</c> protobuf message directly.
	/// </summary>
	/// <param name="cancellationToken">A token that cancels the operation.</param>
	// pyatv/protocols/mrp/__init__.py (turn_on) — line 657-659 as of pyatv 0.18.0:
	// "await self.protocol.send(messages.wake_device())". This is distinct from RemoteControl.wakeup(),
	// which instead sends a SEND_HID_EVENT_MESSAGE for the "wakeup" key (_send_hid_key, line 296-319,
	// via messages.send_hid_event, line 437-439).
	public Task TurnOn (CancellationToken cancellationToken = default) => _protocol.SendAsync (MrpMessages.WakeDevice (), cancellationToken);

	/// <summary>
	/// Turns the device "off": on tvOS this dismisses to the screensaver rather than a true power
	/// off (MRP has no dedicated power-off message; see the DeviceInfoMessage.logicalDeviceCount
	/// == 0 check pyatv uses to detect the resulting Off power state).
	/// </summary>
	/// <param name="cancellationToken">A token that cancels the operation.</param>
	// pyatv/protocols/mrp/__init__.py (MrpPower.turn_off) — line 664-668 as of pyatv 0.18.0:
	// "await self.remote.home(InputAction.Hold)" then "await asyncio.sleep(DELAY_BETWEEN_COMMANDS)"
	// then "await self.remote.select()". DELAY_BETWEEN_COMMANDS = 0.1 (line 149).
	public async Task TurnOff (CancellationToken cancellationToken = default)
		{
		await Home (InputAction.Hold, cancellationToken).ConfigureAwait (false);
		await Task.Delay (TimeSpan.FromSeconds (0.1), cancellationToken).ConfigureAwait (false);
		await Select (InputAction.SingleTap, cancellationToken).ConfigureAwait (false);
		}

	/// <summary>Press key volume up.</summary>
	/// <param name="cancellationToken">A token that cancels the operation.</param>
	// pyatv/protocols/mrp/__init__.py (volume_up) — line 413-415 as of pyatv 0.18.0
	public Task VolumeUp (CancellationToken cancellationToken = default) => SendHidKeyAsync ("volume_up", InputAction.SingleTap, cancellationToken);

	/// <summary>Press key volume down.</summary>
	/// <param name="cancellationToken">A token that cancels the operation.</param>
	// pyatv/protocols/mrp/__init__.py (volume_down) — line 417-419 as of pyatv 0.18.0
	public Task VolumeDown (CancellationToken cancellationToken = default) => SendHidKeyAsync ("volume_down", InputAction.SingleTap, cancellationToken);

	/// <summary>Press key play.</summary>
	/// <param name="cancellationToken">A token that cancels the operation.</param>
	// pyatv/protocols/mrp/__init__.py (play) — line 372-374 as of pyatv 0.18.0
	public Task Play (CancellationToken cancellationToken = default) => SendCommandAsync (Command.Play, cancellationToken: cancellationToken);

	/// <summary>Press key play.</summary>
	/// <param name="cancellationToken">A token that cancels the operation.</param>
	// pyatv/protocols/mrp/__init__.py (pause) — line 389-391 as of pyatv 0.18.0
	public Task Pause (CancellationToken cancellationToken = default) => SendCommandAsync (Command.Pause, cancellationToken: cancellationToken);

	/// <summary>Toggle between play and pause.</summary>
	/// <param name="cancellationToken">A token that cancels the operation.</param>
	// pyatv/protocols/mrp/__init__.py (play_pause) — line 376-387 as of pyatv 0.18.0
	public async Task PlayPause (CancellationToken cancellationToken = default)
		{
		// Cannot use the feature interface here since it emulates the feature state.
		CommandInfo? info = _playerStateManager.Playing.CommandInfoFor (Command.TogglePlayPause);
		if (info is { Enabled: true })
			{
			await SendCommandAsync (Command.TogglePlayPause, cancellationToken: cancellationToken).ConfigureAwait (false);
			return;
			}

		AppleTvControlLibrary.Mrp.Protobuf.PlaybackState.Types.Enum? state = _playerStateManager.Playing.PlaybackStateValue;
		if (state == AppleTvControlLibrary.Mrp.Protobuf.PlaybackState.Types.Enum.Playing)
			{
			await Pause (cancellationToken).ConfigureAwait (false);
			}
		else if (state == AppleTvControlLibrary.Mrp.Protobuf.PlaybackState.Types.Enum.Paused)
			{
			await Play (cancellationToken).ConfigureAwait (false);
			}
		}

	/// <summary>Press key stop.</summary>
	/// <param name="cancellationToken">A token that cancels the operation.</param>
	// pyatv/protocols/mrp/__init__.py (stop) — line 393-395 as of pyatv 0.18.0
	public Task Stop (CancellationToken cancellationToken = default) => SendCommandAsync (Command.Stop, cancellationToken: cancellationToken);

	/// <summary>Begin fast-forwarding through the current media.</summary>
	/// <param name="cancellationToken">A token that cancels the operation.</param>
	// pyatv/protocols/mrp/protobuf/CommandInfo.proto — BeginFastForward = 9
	public Task BeginFastForward (CancellationToken cancellationToken = default) => SendCommandAsync (Command.BeginFastForward, cancellationToken: cancellationToken);

	/// <summary>Ends a previously started fast-forward.</summary>
	/// <param name="cancellationToken">A token that cancels the operation.</param>
	// pyatv/protocols/mrp/protobuf/CommandInfo.proto — EndFastForward = 10
	public Task EndFastForward (CancellationToken cancellationToken = default) => SendCommandAsync (Command.EndFastForward, cancellationToken: cancellationToken);

	/// <summary>Begins rewinding through the current media.</summary>
	/// <param name="cancellationToken">A token that cancels the operation.</param>
	// pyatv/protocols/mrp/protobuf/CommandInfo.proto — BeginRewind = 11
	public Task BeginRewind (CancellationToken cancellationToken = default) => SendCommandAsync (Command.BeginRewind, cancellationToken: cancellationToken);

	/// <summary>Ends a previously started rewind.</summary>
	/// <param name="cancellationToken">A token that cancels the operation.</param>
	// pyatv/protocols/mrp/protobuf/CommandInfo.proto — EndRewind = 12
	public Task EndRewind (CancellationToken cancellationToken = default) => SendCommandAsync (Command.EndRewind, cancellationToken: cancellationToken);

	/// <summary>Press key next.</summary>
	/// <param name="cancellationToken">A token that cancels the operation.</param>
	// pyatv/protocols/mrp/__init__.py (next) — line 397-399 as of pyatv 0.18.0
	public Task Next (CancellationToken cancellationToken = default) => SendCommandAsync (Command.NextTrack, cancellationToken: cancellationToken);

	/// <summary>Press key previous.</summary>
	/// <param name="cancellationToken">A token that cancels the operation.</param>
	// pyatv/protocols/mrp/__init__.py (previous) — line 401-403 as of pyatv 0.18.0
	public Task Previous (CancellationToken cancellationToken = default) => SendCommandAsync (Command.PreviousTrack, cancellationToken: cancellationToken);

	/// <summary>Skip forward a time interval. Skip interval is typically 15-30s, but is decided by the app.</summary>
	/// <param name="timeInterval">The number of seconds to skip, or 0 to use the app's preferred interval.</param>
	/// <param name="cancellationToken">A token that cancels the operation.</param>
	// pyatv/protocols/mrp/__init__.py (skip_forward) — line 441-446 as of pyatv 0.18.0
	public Task SkipForward (double timeInterval = 0.0, CancellationToken cancellationToken = default) => SkipCommandAsync (Command.SkipForward, timeInterval, cancellationToken);

	/// <summary>Skip backwards a time interval. Skip interval is typically 15-30s, but is decided by the app.</summary>
	/// <param name="timeInterval">The number of seconds to skip, or 0 to use the app's preferred interval.</param>
	/// <param name="cancellationToken">A token that cancels the operation.</param>
	// pyatv/protocols/mrp/__init__.py (skip_backward) — line 448-453 as of pyatv 0.18.0
	public Task SkipBackward (double timeInterval = 0.0, CancellationToken cancellationToken = default) => SkipCommandAsync (Command.SkipBackward, timeInterval, cancellationToken);

	/// <summary>Seek in the current playing media.</summary>
	/// <param name="position">The absolute position, in seconds.</param>
	/// <param name="cancellationToken">A token that cancels the operation.</param>
	// pyatv/protocols/mrp/__init__.py (set_position) — line 469-471 as of pyatv 0.18.0
	public Task SetPosition (int position, CancellationToken cancellationToken = default) => _protocol.SendAndReceiveAsync (MrpMessages.SeekToPosition (position), cancellationToken: cancellationToken);

	/// <summary>Change shuffle mode to on or off.</summary>
	/// <param name="shuffleState">The shuffle mode to set.</param>
	/// <param name="cancellationToken">A token that cancels the operation.</param>
	// pyatv/protocols/mrp/__init__.py (set_shuffle) — line 473-475 as of pyatv 0.18.0
	public Task SetShuffle (ShuffleState shuffleState, CancellationToken cancellationToken = default)
		{
		// pyatv/protocols/mrp/messages.py (shuffle) — line 184-195 as of pyatv 0.18.0
		ShuffleMode.Types.Enum mode = shuffleState switch
			{
			ShuffleState.Off => ShuffleMode.Types.Enum.Off,
			ShuffleState.Albums => ShuffleMode.Types.Enum.Albums,
			_ => ShuffleMode.Types.Enum.Songs,
			};

		return _protocol.SendAndReceiveAsync (MrpMessages.Shuffle (mode), cancellationToken: cancellationToken);
		}

	/// <summary>Change repeat state.</summary>
	/// <param name="repeatState">The repeat mode to set.</param>
	/// <param name="cancellationToken">A token that cancels the operation.</param>
	// pyatv/protocols/mrp/__init__.py (set_repeat) — line 477-479 as of pyatv 0.18.0
	public Task SetRepeat (RepeatState repeatState, CancellationToken cancellationToken = default)
		{
		// pyatv/protocols/mrp/messages.py (repeat) — line 170-181 as of pyatv 0.18.0
		RepeatMode.Types.Enum mode = repeatState switch
			{
			RepeatState.Off => RepeatMode.Types.Enum.Off,
			RepeatState.Track => RepeatMode.Types.Enum.One,
			_ => RepeatMode.Types.Enum.All,
			};

		return _protocol.SendAndReceiveAsync (MrpMessages.Repeat (mode), cancellationToken: cancellationToken);
		}

	/// <summary>Change volume on a device.</summary>
	/// <param name="deviceUid">The output device identifier.</param>
	/// <param name="volume">The volume level, in the range 0.0-1.0.</param>
	/// <param name="cancellationToken">A token that cancels the operation.</param>
	// pyatv/protocols/mrp/messages.py (set_volume) — line 206-212 as of pyatv 0.18.0
	public Task SetVolume (string deviceUid, float volume, CancellationToken cancellationToken = default) => _protocol.SendAsync (MrpMessages.SetVolume (deviceUid, volume), cancellationToken);

	// pyatv/protocols/mrp/__init__.py (_skip_command) — line 455-467 as of pyatv 0.18.0
	private Task SkipCommandAsync (Command command, double timeInterval, CancellationToken cancellationToken)
		{
		CommandInfo? info = _playerStateManager.Playing.CommandInfoFor (command);

		int skipInterval;
		if (timeInterval > 0)
			{
			skipInterval = (int)timeInterval;
			}
		else if (info is not null && info.PreferredIntervals.Count > 0)
			{
			// Pick the first preferred interval for simplicity.
			skipInterval = (int)info.PreferredIntervals[0];
			}
		else
			{
			skipInterval = DEFAULT_SKIP_TIME;
			}

		return SendCommandAsync (command, options => options.SkipInterval = skipInterval, cancellationToken);
		}

	// pyatv/protocols/mrp/__init__.py (_send_command) — line 342-354 as of pyatv 0.18.0
	private async Task SendCommandAsync (Command command, Action<CommandOptions>? configureOptions = null, CancellationToken cancellationToken = default)
		{
		ProtocolMessage response = await _protocol.SendAndReceiveAsync (MrpMessages.Command (command, configureOptions), cancellationToken: cancellationToken).ConfigureAwait (false);
		SendCommandResultMessage inner = response.GetExtension (SendCommandResultMessageExtensions.SendCommandResultMessage);

		if (inner.SendError == SendError.Types.Enum.NoError)
			{
			return;
			}

		throw new MrpCommandException (
			$"{command} failed: SendError={inner.SendError}, HandlerReturnStatus={inner.HandlerReturnStatus}");
		}

	/// <summary>
	/// Fetches artwork for the currently playing item, mirroring pyatv's <c>MrpMetadata.artwork()</c>:
	/// checks a small identifier-keyed cache first, then attempts a remote (HTTP) fetch using
	/// <c>artworkIdentifier</c>/<c>artworkURL</c> metadata, and falls back to an in-band MRP fetch
	/// (<see cref="GetLocalArtworkAsync"/>) if the remote fetch does not produce a result. All
	/// fetch failures (HTTP errors, malformed URL templates, cancelled/failed MRP round-trips) are
	/// valid outcomes — not every item has artwork, and a device may reject the request — so they
	/// are handled by returning <see langword="null"/> rather than propagating.
	/// </summary>
	/// <param name="width">The requested artwork width, or -1 for no preference.</param>
	/// <param name="height">The requested artwork height, or -1 for no preference.</param>
	/// <param name="cancellationToken">A token that cancels the operation.</param>
	/// <returns>The artwork bytes and MIME type, or <see langword="null"/> if no artwork is available.</returns>
	// pyatv/protocols/mrp/__init__.py (MrpMetadata.artwork) — line 504-532 as of pyatv 0.18.0
	public async Task<(byte[] Data, string? MimeType)?> GetArtworkAsync (int width = -1, int height = 400, CancellationToken cancellationToken = default)
		{
		string? identifier = GetArtworkId ();
		if (identifier is null)
			{
			return null;
			}

		if (_artworkCache.TryGetValue (identifier, out (byte[] Data, string? MimeType)? cached))
			{
			return cached;
			}

		(byte[] Data, string? MimeType)? artwork;
		try
			{
			artwork = await GetRemoteArtworkAsync (width, height, cancellationToken).ConfigureAwait (false);
			artwork ??= await GetLocalArtworkAsync (width, height, cancellationToken).ConfigureAwait (false);
			}
		catch (Exception) when (!cancellationToken.IsCancellationRequested)
			{
			// pyatv/protocols/mrp/__init__.py — line 524-528 as of pyatv 0.18.0: "Artwork not present in
			// response" is logged and swallowed; a failed fetch is a valid outcome, not a caller error.
			artwork = null;
			}

		CacheArtwork (identifier, artwork);
		return artwork;
		}

	// pyatv/protocols/mrp/__init__.py (MrpMetadata.artwork_id) — line 600-610 as of pyatv 0.18.0
	private string? GetArtworkId ()
		{
		MrpPlayerState playing = _playerStateManager.Playing;
		ContentItemMetadata? metadata = playing.Metadata;
		return metadata is null || !((metadata.HasArtworkAvailable && metadata.ArtworkAvailable) || metadata.HasArtworkURL)
			? null
			: metadata.HasArtworkIdentifier
			? metadata.ArtworkIdentifier
			: metadata.HasContentIdentifier ? metadata.ContentIdentifier : playing.ItemIdentifier;
		}

	// pyatv/protocols/mrp/__init__.py (MrpMetadata._fetch_remote_artwork) — line 539-581 as of pyatv 0.18.0
	private async Task<(byte[] Data, string? MimeType)?> GetRemoteArtworkAsync (int width, int height, CancellationToken cancellationToken)
		{
		ContentItemMetadata? metadata = _playerStateManager.Playing.Metadata;
		if (metadata is null)
			{
			return null;
			}

		List<string> urls = [];

		if (metadata.HasArtworkIdentifier)
			{
			// The itunes image server preserves aspect ratio; a width/height < 1 requests the
			// largest available size, mirroring pyatv's "999999 if width < 1 else width" fallback.
			string urlTemplate = metadata.ArtworkIdentifier;
			string url = urlTemplate
				.Replace ("{w}", (width < 1 ? 999999 : width).ToString (System.Globalization.CultureInfo.InvariantCulture))
				.Replace ("{h}", (height < 1 ? 999999 : height).ToString (System.Globalization.CultureInfo.InvariantCulture))
				.Replace ("{c}", "bb")
				.Replace ("{f}", "png");

			if (Uri.TryCreate (url, UriKind.Absolute, out Uri? parsedUrl) && (parsedUrl.Scheme == Uri.UriSchemeHttp || parsedUrl.Scheme == Uri.UriSchemeHttps))
				{
				urls.Add (url);
				}
			}

		if (metadata.HasArtworkURL)
			{
			// artworkURL has fixed size and format, use it as a fallback.
			urls.Add (metadata.ArtworkURL);
			}

		foreach (string url in urls)
			{
			try
				{
				using HttpResponseMessage response = await _httpClient.GetAsync (url, cancellationToken).ConfigureAwait (false);
				if (!response.IsSuccessStatusCode)
					{
					continue;
					}

				#pragma warning disable CA2016 // ReadAsByteArrayAsync(CancellationToken) overload unavailable on net472
				byte[] data = await response.Content.ReadAsByteArrayAsync ().ConfigureAwait (false);
#pragma warning restore CA2016
				return (data, response.Content.Headers.ContentType?.MediaType);
				}
			catch (HttpRequestException)
				{
				// A failed remote-artwork fetch for one URL is not fatal; try the next candidate (or
				// fall back to the local MRP fetch).
				}
			}

		return null;
		}

	// pyatv/protocols/mrp/__init__.py (self.artwork_cache = Cache(limit=4)) — line 497 as of pyatv 0.18.0
	private void CacheArtwork (string identifier, (byte[] Data, string? MimeType)? artwork)
		{
		if (_artworkCache.ContainsKey (identifier))
			{
			_ = _artworkCacheOrder.Remove (identifier);
			}
		else if (_artworkCacheOrder.Count >= ARTWORK_CACHE_LIMIT)
			{
			string oldest = _artworkCacheOrder[0];
			_artworkCacheOrder.RemoveAt (0);
			_ = _artworkCache.Remove (oldest);
			}

		_artworkCache[identifier] = artwork;
		_artworkCacheOrder.Add (identifier);
		}

	/// <summary>Explicitly fetches artwork bytes for the currently playing item directly over MRP,
	/// bypassing the remote-URL fetch and cache used by <see cref="GetArtworkAsync"/>.</summary>
	/// <param name="width">The requested artwork width, or -1 for no preference.</param>
	/// <param name="height">The requested artwork height, or -1 for no preference.</param>
	/// <param name="cancellationToken">A token that cancels the operation.</param>
	/// <returns>The artwork bytes and MIME type, or <see langword="null"/> if no artwork is available.</returns>
	// pyatv/protocols/mrp/__init__.py (_fetch_local_artwork) — line 583-598 as of pyatv 0.18.0
	public async Task<(byte[] Data, string? MimeType)?> GetLocalArtworkAsync (int width = -1, int height = 400, CancellationToken cancellationToken = default)
		{
		MrpPlayerState playing = _playerStateManager.Playing;
		if (playing.Items.Count <= playing.Location)
			{
			return null;
			}

		ProtocolMessage response = await _protocol.SendAndReceiveAsync (
			MrpMessages.PlaybackQueueRequest (playing.Location, width, height),
			cancellationToken: cancellationToken).ConfigureAwait (false);

		if (!response.HasType)
			{
			return null;
			}

		SetStateMessage inner = response.GetExtension (SetStateMessageExtensions.SetStateMessage);
		if (inner.PlaybackQueue is null || inner.PlaybackQueue.ContentItems.Count <= playing.Location)
			{
			return null;
			}

		ContentItem item = inner.PlaybackQueue.ContentItems[playing.Location];
		return item.ArtworkData is not { Length: > 0 } ? null : (item.ArtworkData.ToByteArray (), playing.Metadata?.ArtworkMIMEType);
		}

	// pyatv/protocols/mrp/__init__.py (_send_hid_key) — line 296-324 as of pyatv 0.18.0
	private async Task SendHidKeyAsync (string key, InputAction action, CancellationToken cancellationToken)
		{
		if (!_keyLookup.TryGetValue (key, out (int UsePage, int Usage) keycode))
			{
			throw new NotSupportedException ($"unsupported key: {key}");
			}

		switch (action)
			{
			case InputAction.SingleTap:
				await DoPressAsync (keycode, hold: false, cancellationToken).ConfigureAwait (false);
				break;
			case InputAction.DoubleTap:
				await DoPressAsync (keycode, hold: false, cancellationToken).ConfigureAwait (false);
				await DoPressAsync (keycode, hold: false, cancellationToken).ConfigureAwait (false);
				break;
			case InputAction.Hold:
				await DoPressAsync (keycode, hold: true, cancellationToken).ConfigureAwait (false);
				break;
			default:
				throw new NotSupportedException ($"unsupported input action: {action}");
			}
		}

	// pyatv/protocols/mrp/__init__.py (_do_press) — line 299-310 as of pyatv 0.18.0
	private async Task DoPressAsync ((int UsePage, int Usage) keycode, bool hold, CancellationToken cancellationToken)
		{
		await _protocol.SendAsync (MrpMessages.SendHidEvent (keycode.UsePage, keycode.Usage, true), cancellationToken).ConfigureAwait (false);

		if (hold)
			{
			// Hardcoded hold time for one second.
			await Task.Delay (TimeSpan.FromSeconds (1), cancellationToken).ConfigureAwait (false);
			}

		await _protocol.SendAsync (MrpMessages.SendHidEvent (keycode.UsePage, keycode.Usage, false), cancellationToken).ConfigureAwait (false);

		// Send and receive a generic message as some kind of "flush" mechanism.
		_ = await _protocol.SendAndReceiveAsync (MrpMessages.Create (ProtocolMessage.Types.Type.GenericMessage), cancellationToken: cancellationToken).ConfigureAwait (false);
		}
	}
