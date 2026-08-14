// Copyright (c) 2026 Neil Colvin. Licensed under the MIT License.
// See LICENSE file in the repository root for full license text.

namespace AppleTvControlLibrary.Remote.Mrp.Wpf.ViewModels;

/// <summary>
/// A simple id/display-name pair, reserved for future use (e.g. if MRP output-device selection
/// for <c>SetVolume(deviceUid, ...)</c> is exposed as a dropdown).
/// </summary>
public sealed class SelectableItem
	{
	/// <summary>Initializes a new instance of the <see cref="SelectableItem"/> class.</summary>
	/// <param name="id">The identifier.</param>
	/// <param name="displayName">The user-facing display name.</param>
	public SelectableItem (string id, string displayName)
		{
		this.Id = id;
		this.DisplayName = displayName;
		}

	/// <summary>Gets the identifier.</summary>
	public string Id
		{
		get;
		}

	/// <summary>Gets the user-facing display name.</summary>
	public string DisplayName
		{
		get;
		}
	}
