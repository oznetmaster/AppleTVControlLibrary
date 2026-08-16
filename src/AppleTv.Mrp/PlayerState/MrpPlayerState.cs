// Copyright (c) 2026 Neil Colvin. Licensed under the MIT License.
// See LICENSE file in the repository root for full license text.

using System;
using System.Collections.Generic;
using System.Linq;

using AppleTvControlLibrary.Mrp.Protobuf;
using AppleTvControlLibrary.Mrp.Protocol;

namespace AppleTvControlLibrary.Mrp.PlayerState;

/// <summary>
/// Listener interface notified whenever the state relevant to the currently active client/player
/// changes (or, for the "always" case in pyatv, on every update regardless of relevance).
/// </summary>
// pyatv/protocols/mrp/player_state.py (PlayerStateManager.listener) — line 202-216 as of pyatv 0.18.0
public interface IMrpPlayerStateListener
	{
	/// <summary>Called when relevant player state has changed.</summary>
	void StateUpdated ();
	}

/// <summary>
/// Represents what is currently playing for a single player.
/// </summary>
// pyatv/protocols/mrp/player_state.py (PlayerState) — line 17-119 as of pyatv 0.18.0
public sealed class MrpPlayerState
	{

	/// <summary>Initializes a new instance of the <see cref="MrpPlayerState"/> class.</summary>
	/// <param name="parent">The client that owns this player.</param>
	/// <param name="player">The player identity to initialize from.</param>
	// pyatv/protocols/mrp/player_state.py (PlayerState.__init__) — line 20-29 as of pyatv 0.18.0
	public MrpPlayerState (MrpClient parent, NowPlayingPlayer player)
		{
		Parent = parent;
		Identifier = player.Identifier;
		Update (player);
		}

	/// <summary>Gets the supported commands reported directly on this player.</summary>
	// pyatv/protocols/mrp/player_state.py (self.supported_commands) — line 21 as of pyatv 0.18.0
	public List<CommandInfo> SupportedCommands
		{
		get;
		} = [];

	/// <summary>Gets the current playback queue items.</summary>
	// pyatv/protocols/mrp/player_state.py (self.items) — line 22 as of pyatv 0.18.0
	public List<ContentItem> Items
		{
		get;
		private set;
		} = [];

	/// <summary>Gets the current location (index) within <see cref="Items"/>.</summary>
	// pyatv/protocols/mrp/player_state.py (self.location) — line 23 as of pyatv 0.18.0
	public int Location
		{
		get;
		private set;
		}

	/// <summary>Gets the player identifier.</summary>
	// pyatv/protocols/mrp/player_state.py (self.identifier) — line 25 as of pyatv 0.18.0
	public string? Identifier
		{
		get;
		}

	/// <summary>Gets the player's display name.</summary>
	// pyatv/protocols/mrp/player_state.py (self.display_name) — line 26 as of pyatv 0.18.0
	public string? DisplayName
		{
		get;
		private set;
		}

	/// <summary>
	/// Gets the icon URL reported for this player, if any. This is a raw wire-format field
	/// (NowPlayingPlayer.proto, field 7: iconURL) that pyatv 0.18.0 receives but never reads or
	/// exposes anywhere in its Python source — there is no reference behavior to port for it, so
	/// it is surfaced here unprocessed rather than approximated.
	/// </summary>
	public string? IconUrl
		{
		get;
		private set;
		}

	/// <summary>Gets the client that owns this player.</summary>
	// pyatv/protocols/mrp/player_state.py (self.parent) — line 27 as of pyatv 0.18.0
	public MrpClient? Parent
		{
		get;
		internal set;
		}

	/// <summary>Gets a value indicating whether this player has a valid (non-empty) identifier.</summary>
	// pyatv/protocols/mrp/player_state.py (is_valid) — line 31-34 as of pyatv 0.18.0
	public bool IsValid => !string.IsNullOrEmpty (Identifier);

	/// <summary>Updates player metadata from a <see cref="NowPlayingPlayer"/> payload.</summary>
	/// <param name="player">The player identity to update from.</param>
	// pyatv/protocols/mrp/player_state.py (PlayerState.update) — line 36-38 as of pyatv 0.18.0
	public void Update (NowPlayingPlayer player)
		{
		if (!string.IsNullOrEmpty (player.DisplayName))
			{
			DisplayName = player.DisplayName;
			}

		if (!string.IsNullOrEmpty (player.IconURL))
			{
			IconUrl = player.IconURL;
			}
		}

	/// <summary>Gets the playback state of the device, applying pyatv's playback-rate disambiguation.</summary>
	// pyatv/protocols/mrp/player_state.py (playback_state) — line 40-64 as of pyatv 0.18.0
	public PlaybackState.Types.Enum? PlaybackStateValue
		{
		get
			{
			// If playback state has not been received, assume player is not playing anything (i.e. idle).
			if (field is null)
				{
				return null;
				}

			// If player is considered paused, no content is playing...
			if (field == PlaybackState.Types.Enum.Paused)
				{
				// ...unless something is in the queue.
				return Metadata is not null ? PlaybackState.Types.Enum.Paused : null;
				}

			// All other states than playing (and paused) should pass through.
			if (field != PlaybackState.Types.Enum.Playing)
				{
				return field;
				}

			float? playbackRate = MetadataField<float?> ("playbackRate");
			return playbackRate is null
				? (field)
				: IsClose (playbackRate.Value, 0.0f)
				? field == PlaybackState.Types.Enum.Playing
					? PlaybackState.Types.Enum.Playing
					: PlaybackState.Types.Enum.Paused
				: IsClose (playbackRate.Value, 1.0f) ? PlaybackState.Types.Enum.Playing : PlaybackState.Types.Enum.Seeking;
			}

		private set;
		}

	/// <summary>Gets the metadata of the currently playing item, if any.</summary>
	// pyatv/protocols/mrp/player_state.py (metadata) — line 66-70 as of pyatv 0.18.0
	public ContentItemMetadata? Metadata => Items.Count >= Location + 1 ? Items[Location].Metadata : null;

	/// <summary>Gets the identifier of the current item in the queue.</summary>
	// pyatv/protocols/mrp/player_state.py (item_identifier) — line 72-76 as of pyatv 0.18.0
	public string? ItemIdentifier => Items.Count >= Location + 1 ? Items[Location].Identifier : null;

	// pyatv/protocols/mrp/__init__.py (_cocoa_to_timestamp) — line 152-156 as of pyatv 0.18.0: cocoa
	// epoch (2001-01-01) offset in seconds from the Unix epoch.
	private static readonly DateTime CocoaEpoch = new DateTime (2001, 1, 1, 0, 0, 0, DateTimeKind.Utc);

	/// <summary>Gets the total play time in seconds, or <see langword="null"/> if unknown.</summary>
	// pyatv/protocols/mrp/__init__.py (total_time) — line 200-206 as of pyatv 0.18.0
	public int? TotalTime
		{
		get
			{
			double? duration = MetadataField<double?> ("duration");
			return duration is null || double.IsNaN (duration.Value) ? null : (int)duration.Value;
			}
		}

	/// <summary>Gets the current position in the playing media in seconds, or <see langword="null"/> if unknown.</summary>
	// pyatv/protocols/mrp/__init__.py (position) — line 208-226 as of pyatv 0.18.0
	public int? Position
		{
		get
			{
			double? elapsedTimestamp = MetadataField<double?> ("elapsedTimeTimestamp");
			if (elapsedTimestamp is null or 0)
				{
				return null;
				}

			double elapsedTime = MetadataField<double?> ("elapsedTime") ?? 0;
			DateTime referenceTime = CocoaEpoch.AddSeconds (elapsedTimestamp.Value);
			double diff = (DateTime.UtcNow - referenceTime).TotalSeconds;

			float playbackRate = MetadataField<float?> ("playbackRate") ?? 0.0f;
			return PlaybackStateValue == PlaybackState.Types.Enum.Playing && !IsClose (playbackRate, 0.0f)
				? (int)(elapsedTime + diff)
				: (int)elapsedTime;
			}
		}

	/// <summary>Returns a specific metadata field, or <see langword="null"/> if missing.</summary>
	/// <typeparam name="T">The expected field type.</typeparam>
	/// <param name="field">The metadata field name.</param>
	// pyatv/protocols/mrp/player_state.py (metadata_field) — line 78-83 as of pyatv 0.18.0
	public T? MetadataField<T> (string field)
		{
		ContentItemMetadata? metadata = Metadata;
		if (metadata is null)
			{
			return default;
			}

		Google.Protobuf.Reflection.FieldDescriptor? descriptor = ContentItemMetadata.Descriptor.FindFieldByName (field);
		if (descriptor is null || !descriptor.Accessor.HasValue (metadata))
			{
			return default;
			}

		object? value = descriptor.Accessor.GetValue (metadata);
		return value is T typed ? typed : default;
		}

	/// <summary>Returns supported command info for the given command, checking this player and then the parent client.</summary>
	/// <param name="command">The command to look up.</param>
	// pyatv/protocols/mrp/player_state.py (command_info) — line 85-90 as of pyatv 0.18.0
	public CommandInfo? CommandInfoFor (Command command)
		{
		foreach (CommandInfo info in SupportedCommands.Concat (Parent?.SupportedCommands ?? []))
			{
			if (info.Command == command)
				{
				return info;
				}
			}

		return null;
		}

	/// <summary>Updates current state with new data from a SET_STATE_MESSAGE.</summary>
	/// <param name="setState">The decoded SetStateMessage.</param>
	// pyatv/protocols/mrp/player_state.py (handle_set_state) — line 92-103 as of pyatv 0.18.0
	public void HandleSetState (SetStateMessage setState)
		{
		if (setState.HasPlaybackState)
			{
			PlaybackStateValue = setState.PlaybackState;
			}

		if (setState.SupportedCommands is not null)
			{
			SupportedCommands.Clear ();
			SupportedCommands.AddRange (setState.SupportedCommands.SupportedCommands_);
			}

		if (setState.PlaybackQueue is not null)
			{
			PlaybackQueue queue = setState.PlaybackQueue;
			Items = [.. queue.ContentItems];
			Location = queue.Location;
			}
		}

	/// <summary>Updates current state with new data from an UPDATE_CONTENT_ITEM_MESSAGE.</summary>
	/// <param name="itemUpdate">The decoded UpdateContentItemMessage.</param>
	// pyatv/protocols/mrp/player_state.py (handle_content_item_update) — line 105-115 as of pyatv 0.18.0
	public void HandleContentItemUpdate (UpdateContentItemMessage itemUpdate)
		{
		foreach (ContentItem updatedItem in itemUpdate.ContentItems)
			{
			foreach (ContentItem existing in Items)
				{
				if (updatedItem.Identifier == existing.Identifier)
					{
					// Other parts of the ContentItem should be merged as well, but those are not
					// used right now so will do that when needed.
					if (updatedItem.Metadata is not null)
						{
						existing.Metadata ??= new ContentItemMetadata ();
						existing.Metadata.MergeFrom (updatedItem.Metadata);
						}
					}
				}
			}
		}

	private static bool IsClose (float a, float b) => Math.Abs (a - b) <= 1e-6f;
	}

/// <summary>
/// Represents a single MRP media player client (identified by bundle identifier).
/// </summary>
// pyatv/protocols/mrp/player_state.py (Client) — line 122-171 as of pyatv 0.18.0
public sealed class MrpClient
	{

	/// <summary>Initializes a new instance of the <see cref="MrpClient"/> class.</summary>
	/// <param name="client">The client identity to initialize from.</param>
	// pyatv/protocols/mrp/player_state.py (Client.__init__) — line 125-132 as of pyatv 0.18.0
	public MrpClient (NowPlayingClient client)
		{
		BundleIdentifier = client.BundleIdentifier;
		Update (client);
		}

	/// <summary>Gets the client's bundle identifier.</summary>
	// pyatv/protocols/mrp/player_state.py (self.bundle_identifier) — line 127 as of pyatv 0.18.0
	public string? BundleIdentifier
		{
		get;
		}

	/// <summary>Gets the client's display name.</summary>
	// pyatv/protocols/mrp/player_state.py (self.display_name) — line 128 as of pyatv 0.18.0
	public string? DisplayName
		{
		get;
		private set;
		}

	/// <summary>Gets the players known for this client, keyed by player identifier.</summary>
	// pyatv/protocols/mrp/player_state.py (self.players) — line 130 as of pyatv 0.18.0
	public Dictionary<string, MrpPlayerState> Players
		{
		get;
		} = [];

	/// <summary>Gets the default supported commands for this client.</summary>
	// pyatv/protocols/mrp/player_state.py (self.supported_commands) — line 131 as of pyatv 0.18.0
	public List<CommandInfo> SupportedCommands
		{
		get;
		} = [];

	/// <summary>Gets or sets the currently active player for this client.</summary>
	// pyatv/protocols/mrp/player_state.py (active_player property/setter) — line 134-146 as of pyatv 0.18.0
	public MrpPlayerState ActivePlayer
		{
		get
			{
			return field is null
				? Players.TryGetValue (MrpPlayerStateManager.DefaultPlayerId, out MrpPlayerState? defaultPlayer)
					? defaultPlayer
					: new MrpPlayerState (this, new NowPlayingPlayer ())
				: field;
			}
		set;
		}

	/// <summary>Gets state for a player, creating it if it does not already exist.</summary>
	/// <param name="player">The player identity to look up.</param>
	// pyatv/protocols/mrp/player_state.py (get_player) — line 148-152 as of pyatv 0.18.0
	public MrpPlayerState GetPlayer (NowPlayingPlayer player)
		{
		string key = player.Identifier ?? string.Empty;
		if (!Players.TryGetValue (key, out MrpPlayerState? state))
			{
			state = new MrpPlayerState (this, player);
			Players[key] = state;
			}

		return state;
		}

	/// <summary>Updates the default supported commands for this client.</summary>
	/// <param name="supportedCommands">The decoded SetDefaultSupportedCommandsMessage.</param>
	// pyatv/protocols/mrp/player_state.py (handle_set_default_supported_commands) — line 154-156 as of pyatv 0.18.0
	public void HandleSetDefaultSupportedCommands (SetDefaultSupportedCommandsMessage supportedCommands)
		{
		SupportedCommands.Clear ();

		// The device can send this message with no SupportedCommands set at all (e.g. while no
		// foreground app has default commands to report, such as during screensaver), in which
		// case the nested message field is null rather than an empty collection. Mirror the same
		// null-check HandleSetState already applies to the analogous field.
		if (supportedCommands.SupportedCommands is not null)
			{
			SupportedCommands.AddRange (supportedCommands.SupportedCommands.SupportedCommands_);
			}
		}

	/// <summary>Handles a change of now-playing player for this client.</summary>
	/// <param name="player">The player identity that is now active.</param>
	// pyatv/protocols/mrp/player_state.py (handle_set_now_playing_player) — line 158-167 as of pyatv 0.18.0
	public void HandleSetNowPlayingPlayer (NowPlayingPlayer player) => ActivePlayer = GetPlayer (player);

	/// <summary>Updates client metadata from a <see cref="NowPlayingClient"/> payload.</summary>
	/// <param name="client">The client identity to update from.</param>
	// pyatv/protocols/mrp/player_state.py (Client.update) — line 169-171 as of pyatv 0.18.0
	public void Update (NowPlayingClient client)
		{
		if (!string.IsNullOrEmpty (client.DisplayName))
			{
			DisplayName = client.DisplayName;
			}
		}
	}

/// <summary>
/// Manages state of all media players, dispatching MRP state-update messages onto
/// <see cref="MrpClient"/>/<see cref="MrpPlayerState"/> instances and notifying a listener
/// whenever state relevant to the active client/player changes.
/// </summary>
/// <remarks>
/// Implements <see cref="IMrpProtocolListener"/> directly and should be assigned to
/// <see cref="MrpProtocol.Listener"/> (or chained via a multiplexing listener), since
/// <see cref="MrpProtocol"/> — unlike pyatv's <c>MessageDispatcher</c> — exposes a single
/// unsolicited-message sink rather than per-type registration.
/// </remarks>
// pyatv/protocols/mrp/player_state.py (PlayerStateManager) — line 174-297 as of pyatv 0.18.0
public sealed class MrpPlayerStateManager : IMrpProtocolListener
	{
	/// <summary>The identifier used by MediaRemote for the default (queue-less) player.</summary>
	// pyatv/protocols/mrp/player_state.py (DEFAULT_PLAYER_ID) — line 14 as of pyatv 0.18.0
	public const string DefaultPlayerId = "MediaRemote-DefaultPlayer";

	private readonly Dictionary<string, MrpClient> _clients = [];

	/// <summary>Gets or sets the listener notified when relevant player state changes.</summary>
	// pyatv/protocols/mrp/player_state.py (listener property/setter) — line 202-216 as of pyatv 0.18.0
	public IMrpPlayerStateListener? Listener
		{
		get;
		set;
		}

	/// <summary>Gets the currently active client, or <see langword="null"/> if none is active.</summary>
	// pyatv/protocols/mrp/player_state.py (client property) — line 218-221 as of pyatv 0.18.0
	public MrpClient? Client
		{
		get;
		private set;
		}

	/// <summary>Gets the player state for the active media player.</summary>
	// pyatv/protocols/mrp/player_state.py (playing property) — line 223-227 as of pyatv 0.18.0
	public MrpPlayerState Playing => Client?.ActivePlayer
		?? new MrpPlayerState (new MrpClient (new NowPlayingClient ()), new NowPlayingPlayer ());

	/// <summary>Gets a value indicating whether the device currently reports volume control as available.</summary>
	// pyatv/protocols/mrp/__init__.py (MrpAudio.is_available) — line 772-775 as of pyatv 0.18.0
	public bool VolumeControlAvailable
		{
		get;
		private set;
		}

	/// <summary>Gets a value indicating whether absolute volume control is available.</summary>
	// pyatv/protocols/mrp/__init__.py (MrpAudio.is_volume_absolute) — line 777-780 as of pyatv 0.18.0
	public bool VolumeControlAbsolute
		{
		get;
		private set;
		}

	/// <summary>Gets a value indicating whether relative volume control is available.</summary>
	// pyatv/protocols/mrp/__init__.py (MrpAudio.is_volume_relative) — line 782-785 as of pyatv 0.18.0
	public bool VolumeControlRelative
		{
		get;
		private set;
		}

	/// <summary>Returns the client for a given <see cref="NowPlayingClient"/>, creating it if needed.</summary>
	/// <param name="client">The client identity to look up.</param>
	// pyatv/protocols/mrp/player_state.py (get_client) — line 187-191 as of pyatv 0.18.0
	public MrpClient GetClient (NowPlayingClient client)
		{
		string bundle = client.BundleIdentifier ?? string.Empty;
		if (!_clients.TryGetValue (bundle, out MrpClient? existing))
			{
			existing = new MrpClient (client);
			_clients[bundle] = existing;
			}

		return existing;
		}

	/// <summary>Returns the player for a given <see cref="PlayerPath"/>.</summary>
	/// <param name="playerPath">The player path to resolve.</param>
	// pyatv/protocols/mrp/player_state.py (get_player) — line 193-195 as of pyatv 0.18.0
	public MrpPlayerState GetPlayer (PlayerPath playerPath) => GetClient (playerPath.Client).GetPlayer (playerPath.Player);

	/// <inheritdoc/>
	// pyatv/protocols/mrp/player_state.py (PlayerStateManager._add_listeners dispatch table) — line 178-186 as of pyatv 0.18.0
	public void MessageReceived (ProtocolMessage message)
		{
		switch (message.Type)
			{
			case ProtocolMessage.Types.Type.SetStateMessage:
				HandleSetState (message.GetExtension (SetStateMessageExtensions.SetStateMessage));
				break;
			case ProtocolMessage.Types.Type.UpdateContentItemMessage:
				HandleContentItemUpdate (message.GetExtension (UpdateContentItemMessageExtensions.UpdateContentItemMessage));
				break;
			case ProtocolMessage.Types.Type.SetNowPlayingClientMessage:
				HandleSetNowPlayingClient (message.GetExtension (SetNowPlayingClientMessageExtensions.SetNowPlayingClientMessage));
				break;
			case ProtocolMessage.Types.Type.SetNowPlayingPlayerMessage:
				HandleSetNowPlayingPlayer (message.GetExtension (SetNowPlayingPlayerMessageExtensions.SetNowPlayingPlayerMessage));
				break;
			case ProtocolMessage.Types.Type.UpdateClientMessage:
				HandleUpdateClient (message.GetExtension (UpdateClientMessageExtensions.UpdateClientMessage));
				break;
			case ProtocolMessage.Types.Type.RemoveClientMessage:
				HandleRemoveClient (message.GetExtension (RemoveClientMessageExtensions.RemoveClientMessage));
				break;
			case ProtocolMessage.Types.Type.RemovePlayerMessage:
				HandleRemovePlayer (message.GetExtension (RemovePlayerMessageExtensions.RemovePlayerMessage));
				break;
			case ProtocolMessage.Types.Type.SetDefaultSupportedCommandsMessage:
				HandleSetDefaultSupportedCommands (message.GetExtension (SetDefaultSupportedCommandsMessageExtensions.SetDefaultSupportedCommandsMessage));
				break;
			case ProtocolMessage.Types.Type.VolumeControlAvailabilityMessage:
				HandleVolumeControlAvailability (message.GetExtension (VolumeControlAvailabilityMessageExtensions.VolumeControlAvailabilityMessage));
				break;
			case ProtocolMessage.Types.Type.VolumeControlCapabilitiesDidChangeMessage:
				HandleVolumeControlCapabilitiesDidChange (message.GetExtension (VolumeControlCapabilitiesDidChangeMessageExtensions.VolumeControlCapabilitiesDidChangeMessage));
				break;
			}
		}

	// pyatv/protocols/mrp/__init__.py (MrpAudio._volume_control_availability) — line 787-796 as of pyatv 0.18.0
	private void HandleVolumeControlAvailability (VolumeControlAvailabilityMessage message) => UpdateVolumeControls (message);

	// pyatv/protocols/mrp/__init__.py (MrpAudio._volume_control_changed) — line 798-808 as of pyatv 0.18.0
	private void HandleVolumeControlCapabilitiesDidChange (VolumeControlCapabilitiesDidChangeMessage message) => UpdateVolumeControls (message.Capabilities);

	// pyatv/protocols/mrp/__init__.py (MrpAudio._update_volume_controls) — line 810-819 as of pyatv 0.18.0
	private void UpdateVolumeControls (VolumeControlAvailabilityMessage? capabilities)
		{
		VolumeControlAvailable = capabilities?.VolumeControlAvailable ?? false;
		VolumeControlAbsolute = VolumeControlAvailable
			&& capabilities is not null
			&& (capabilities.VolumeCapabilities == VolumeCapabilities.Types.Enum.Absolute || capabilities.VolumeCapabilities == VolumeCapabilities.Types.Enum.Both);
		VolumeControlRelative = VolumeControlAvailable
			&& capabilities is not null
			&& (capabilities.VolumeCapabilities == VolumeCapabilities.Types.Enum.Relative || capabilities.VolumeCapabilities == VolumeCapabilities.Types.Enum.Both);

		Listener?.StateUpdated ();
		}

	// pyatv/protocols/mrp/player_state.py (_handle_set_state) — line 229-234 as of pyatv 0.18.0
	private void HandleSetState (SetStateMessage setState)
		{
		// playerPath is an optional message field (SetStateMessage.proto field 9). pyatv's
		// generated Python protobuf code auto-vivifies an unset optional message field to a
		// default instance on access, so player_path.client/.player are always usable there;
		// Google.Protobuf for C# instead returns null for an unset optional message field, which
		// GetPlayer would otherwise dereference unconditionally. The device sends SetStateMessage
		// with no playerPath at all while there is no active player (e.g. during screensaver).
		if (setState.PlayerPath is null)
			{
			return;
			}

		MrpPlayerState player = GetPlayer (setState.PlayerPath);
		player.HandleSetState (setState);

		StateUpdated (player: player);
		}

	// pyatv/protocols/mrp/player_state.py (_handle_content_item_update) — line 236-241 as of pyatv 0.18.0
	private void HandleContentItemUpdate (UpdateContentItemMessage itemUpdate)
		{
		// See the comment in HandleSetState: playerPath is an optional message field
		// (UpdateContentItemMessage.proto field 2) that can be null in C# when unset.
		if (itemUpdate.PlayerPath is null)
			{
			return;
			}

		MrpPlayerState player = GetPlayer (itemUpdate.PlayerPath);
		player.HandleContentItemUpdate (itemUpdate);

		StateUpdated (player: player);
		}

	// pyatv/protocols/mrp/player_state.py (_handle_set_now_playing_client) — line 243-247 as of pyatv 0.18.0
	private void HandleSetNowPlayingClient (SetNowPlayingClientMessage message)
		{
		Client = GetClient (message.Client);

		StateUpdated ();
		}

	// pyatv/protocols/mrp/player_state.py (_handle_set_now_playing_player) — line 249-255 as of pyatv 0.18.0
	private void HandleSetNowPlayingPlayer (SetNowPlayingPlayerMessage message)
		{
		// See the comment in HandleSetState: playerPath is an optional message field
		// (SetNowPlayingPlayerMessage.proto field 1) that can be null in C# when unset.
		if (message.PlayerPath is null)
			{
			return;
			}

		MrpClient client = GetClient (message.PlayerPath.Client);
		client.HandleSetNowPlayingPlayer (message.PlayerPath.Player);

		StateUpdated (client: client);
		}

	// pyatv/protocols/mrp/player_state.py (_handle_remove_client) — line 257-264 as of pyatv 0.18.0
	private void HandleRemoveClient (RemoveClientMessage message)
		{
		string bundle = message.Client.BundleIdentifier ?? string.Empty;
		if (_clients.TryGetValue (bundle, out MrpClient? clientToRemove))
			{
			_ = _clients.Remove (bundle);

			if (ReferenceEquals (clientToRemove, Client))
				{
				Client = null;
				StateUpdated ();
				}
			}
		}

	// pyatv/protocols/mrp/player_state.py (_handle_remove_player) — line 266-274 as of pyatv 0.18.0
	private void HandleRemovePlayer (RemovePlayerMessage message)
		{
		// See the comment in HandleSetState: playerPath is an optional message field
		// (RemovePlayerMessage.proto field 1) that can be null in C# when unset.
		if (message.PlayerPath is null)
			{
			return;
			}

		MrpPlayerState player = GetPlayer (message.PlayerPath);
		if (player.IsValid)
			{
			MrpClient client = GetClient (message.PlayerPath.Client);
			_ = client.Players.Remove (player.Identifier!);
			player.Parent = null;

			if (ReferenceEquals (player, client.ActivePlayer) || player.Identifier == client.ActivePlayer.Identifier)
				{
				client.ActivePlayer = null!;
				StateUpdated (client: client);
				}
			}
		}

	// pyatv/protocols/mrp/player_state.py (_handle_set_default_supported_commands) — line 276-281 as of pyatv 0.18.0
	private void HandleSetDefaultSupportedCommands (SetDefaultSupportedCommandsMessage message)
		{
		// See the comment in HandleSetState: playerPath is an optional message field
		// (SetDefaultSupportedCommandsMessage.proto field 9) that can be null in C# when unset.
		if (message.PlayerPath is null)
			{
			return;
			}

		MrpClient client = GetClient (message.PlayerPath.Client);
		client.HandleSetDefaultSupportedCommands (message);

		StateUpdated ();
		}

	// pyatv/protocols/mrp/player_state.py (_handle_update_client) — line 283-288 as of pyatv 0.18.0
	private void HandleUpdateClient (UpdateClientMessage message)
		{
		MrpClient client = GetClient (message.Client);
		client.Update (message.Client);

		StateUpdated (client: client);
		}

	// pyatv/protocols/mrp/player_state.py (_state_updated) — line 290-297 as of pyatv 0.18.0
	private void StateUpdated (MrpClient? client = null, MrpPlayerState? player = null)
		{
		// pyatv's self.playing is never None (a dummy PlayerState is constructed when there is
		// no active client), and PlayerState.__eq__ compares by identifier — so comparing an
		// unpassed (null) player against it always evaluates to false, unlike a reference/null
		// comparison would. Mirror that explicitly rather than doing a null-aware ReferenceEquals.
		bool isActiveClient = client is null ? Client is null : ReferenceEquals (client, Client);
		bool isActivePlayer = player is not null && player.Identifier == Playing.Identifier;
		bool isAlways = client is null && player is null;

		if (isActiveClient || isActivePlayer || isAlways)
			{
			Listener?.StateUpdated ();
			}
		}
	}
