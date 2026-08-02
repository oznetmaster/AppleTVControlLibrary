// Copyright (c) 2026 Neil Colvin. Licensed under the MIT License.
// See LICENSE file in the repository root for full license text.

namespace AppleTvControlLibrary.Remote.Wpf.ViewModels;

/// <summary>
/// A simple id/display-name pair used to populate the app-list and account-list dropdowns
/// (<c>AppList()</c>/<c>AccountList()</c> both return an id-to-display-name mapping).
/// </summary>
public sealed class SelectableItem
	{
	/// <summary>Initializes a new instance of the <see cref="SelectableItem"/> class.</summary>
	/// <param name="id">The bundle identifier or account identifier.</param>
	/// <param name="displayName">The user-facing display name.</param>
	public SelectableItem (string id, string displayName)
		{
		this.Id = id;
		this.DisplayName = displayName;
		}

	/// <summary>Gets the bundle identifier or account identifier.</summary>
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
