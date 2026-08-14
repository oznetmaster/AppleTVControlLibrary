// Copyright (c) 2026 Neil Colvin. Licensed under the MIT License.
// See LICENSE file in the repository root for full license text.

using AppleTvControlLibrary.Mrp.Protobuf;

namespace AppleTvControlLibrary.Mrp.FakeDevice;

/// <summary>
/// Mutable now-playing metadata for a single player, tracked by <see cref="FakeMrpDeviceState"/>.
/// </summary>
// pyatv/tests/fake_device/mrp.py (PlayingState) — line 155-216 as of pyatv 0.18.0
public sealed class PlayingState
	{
	/// <summary>Gets or sets the content item identifier.</summary>
	public string? Identifier { get; set; }

	/// <summary>Gets or sets the playback state.</summary>
	public PlaybackState.Types.Enum PlaybackState { get; set; }

	/// <summary>Gets or sets the title.</summary>
	public string? Title { get; set; }

	/// <summary>Gets or sets the series name.</summary>
	public string? SeriesName { get; set; }

	/// <summary>Gets or sets the artist name.</summary>
	public string? Artist { get; set; }

	/// <summary>Gets or sets the album name.</summary>
	public string? Album { get; set; }

	/// <summary>Gets or sets the genre.</summary>
	public string? Genre { get; set; }

	/// <summary>Gets or sets the total duration, in seconds.</summary>
	public double? TotalTime { get; set; }

	/// <summary>Gets or sets the current position, in seconds.</summary>
	public double? Position { get; set; }

	/// <summary>Gets or sets the season number.</summary>
	public int? SeasonNumber { get; set; }

	/// <summary>Gets or sets the episode number.</summary>
	public int? EpisodeNumber { get; set; }

	/// <summary>Gets or sets the repeat state.</summary>
	public RepeatMode.Types.Enum? Repeat { get; set; }

	/// <summary>Gets or sets the shuffle state.</summary>
	public ShuffleMode.Types.Enum? Shuffle { get; set; }

	/// <summary>Gets or sets the media type.</summary>
	public ContentItemMetadata.Types.MediaType? MediaType { get; set; }

	/// <summary>Gets or sets the playback rate.</summary>
	public float? PlaybackRate { get; set; }

	/// <summary>Gets or sets the supported commands for the current player.</summary>
	public Command[]? SupportedCommands { get; set; }

	// Note: Command is a top-level generated enum (from CommandInfo.proto), not nested
	// under a message type, matching pyatv's flat `Command` protobuf enum.

	/// <summary>Gets or sets the raw artwork bytes.</summary>
	public byte[]? Artwork { get; set; }

	/// <summary>Gets or sets the artwork identifier.</summary>
	public string? ArtworkIdentifier { get; set; }

	/// <summary>Gets or sets the artwork MIME type.</summary>
	public string? ArtworkMimetype { get; set; }

	/// <summary>Gets or sets the artwork width.</summary>
	public int? ArtworkWidth { get; set; }

	/// <summary>Gets or sets the artwork height.</summary>
	public int? ArtworkHeight { get; set; }

	/// <summary>Gets or sets the skip interval, in seconds.</summary>
	public float? SkipTime { get; set; }

	/// <summary>Gets or sets the display name of the foreground application.</summary>
	public string? AppName { get; set; }

	/// <summary>Gets or sets the content identifier.</summary>
	public string? ContentIdentifier { get; set; }

	/// <summary>Gets or sets the iTunes Store identifier.</summary>
	public long? ITunesStoreIdentifier { get; set; }
	}
