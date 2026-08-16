// Copyright (c) 2026 Neil Colvin. Licensed under the MIT License.
// See LICENSE file in the repository root for full license text.

namespace AppleTvControlLibrary.Remote.Wpf.ViewModels;

/// <summary>
/// A simple id/display-name pair used to populate the app-list and account-list dropdowns
/// (<c>AppList()</c>/<c>AccountList()</c> both return an id-to-display-name mapping).
/// </summary>
/// <remarks>Initializes a new instance of the <see cref="SelectableItem"/> class.</remarks>
/// <param name="id">The bundle identifier or account identifier.</param>
/// <param name="displayName">The user-facing display name.</param>
public sealed class SelectableItem (string id, string displayName)
	{

	/// <summary>Gets the bundle identifier or account identifier.</summary>
	public string Id
		{
		get;
		} = id;

	/// <summary>Gets the user-facing display name.</summary>
	public string DisplayName
		{
		get;
		} = displayName;
	}
