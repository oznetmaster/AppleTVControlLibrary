// Copyright (c) 2026 Neil Colvin. Licensed under the MIT License.
// See LICENSE file in the repository root for full license text.

using AppleTvControlLibrary.Discovery.Companion;

namespace AppleTvControlLibrary.Remote.Wpf.ViewModels;

/// <summary>
/// Wraps a <see cref="CompanionDiscoveryResult"/> from a scan with whether credentials for it
/// are already persisted, so the device list can indicate devices that do not need to be
/// paired again.
/// </summary>
public sealed class DeviceListItem : ViewModelBase
	{
	private bool _isPaired;

	/// <summary>Initializes a new instance of the <see cref="DeviceListItem"/> class.</summary>
	/// <param name="device">The discovered device.</param>
	/// <param name="isPaired">Whether credentials for this device are already stored.</param>
	public DeviceListItem (CompanionDiscoveryResult device, bool isPaired)
		{
		Device = device;
		_isPaired = isPaired;
		}

	/// <summary>Gets the underlying discovery result.</summary>
	public CompanionDiscoveryResult Device
		{
		get;
		}

	/// <summary>Gets the device's display name.</summary>
	public string Name => Device.Name;

	/// <summary>Gets or sets a value indicating whether credentials for this device are already stored.</summary>
	public bool IsPaired
		{
		get => _isPaired;
		set
			{
			if (SetProperty (ref _isPaired, value))
				{
				OnPropertyChanged (nameof (DisplayName));
				}
			}
		}

	/// <summary>Gets the text shown in the device list.</summary>
	/// <remarks>
	/// Paired status is now indicated via color (see <c>MainWindow.xaml</c>'s <c>ListBox.ItemTemplate</c>)
	/// rather than a textual annotation.
	/// </remarks>
	public string DisplayName => Name;
	}
